using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using PoctGateway.Core.Handlers;
using PoctGateway.Core.Protocol.Poct1A;
using PoctGateway.Core.Session;
using PoctGateway.Core.Vendors;

namespace PoctGateway.Core.Engine;

public sealed partial class SessionEngine
{
    private readonly VendorRegistry _vendorRegistry;
    private readonly Func<string, Task> _sendRawAsync;
    private readonly Action<string> _logInfo;
    private readonly Action<string> _logError;
    private readonly IPoctMessageFactory _messageFactory;

    private IVendorDevicePack? _boundVendor;
    private List<HandlerBase>? _handlers;

    // Persistent queue of outbound messages (not reset per inbound message)
    private readonly Queue<OutboundMessage> _pendingOutbound = new();

    // The message currently awaiting acknowledgement (null if none)
    private OutboundMessage? _currentOutbound;

    private int _nextOutboundControlId;

    // Default POCT1A datetime format
    private const string DefaultDateTimeFormat = "yyyy-MM-dd'T'HH:mm:ss.ffK";

    private int GetNextOutboundControlId()
        => Interlocked.Increment(ref _nextOutboundControlId);

    public SessionContext Context { get; }

    public SessionEngine(
        SessionContext context,
        VendorRegistry vendorRegistry,
        Func<string, Task> sendRawAsync,
        Action<string> logInfo,
        Action<string> logError,
        IPoctMessageFactory messageFactory)
    {
        Context = context ?? throw new ArgumentNullException(nameof(context));
        _vendorRegistry = vendorRegistry ?? throw new ArgumentNullException(nameof(vendorRegistry));
        _sendRawAsync = sendRawAsync ?? (_ => Task.CompletedTask);
        _logInfo = logInfo ?? (_ => { });
        _logError = logError ?? (_ => { });
        _messageFactory = messageFactory ?? throw new ArgumentNullException(nameof(messageFactory));
    }

    public async Task ProcessInboundAsync(string rawPayload)
    {
        if (rawPayload is null)
        {
            throw new ArgumentNullException(nameof(rawPayload));
        }

        if (string.IsNullOrWhiteSpace(rawPayload))
        {
            _logInfo($"Session {Context.SessionId}: Ignoring empty payload.");
            return;
        }

        // Reset per-message error state
        Context.ErrorMessage = null;

        // NOTE: We no longer reset _pendingOutbound here - it persists across messages

        if (_boundVendor is null)
        {
            BindVendor(rawPayload);
        }

        Context.CurrentRaw = rawPayload;
        Context.CurrentXDocument = TryParseXml(rawPayload);
        Context.MessageType = Context.CurrentXDocument?.Root?.Name.LocalName
                              ?? DetermineNonXmlMessageType(rawPayload);

        Context.MessageHistory.Add(new SessionMessage
        {
            MessageType = Context.MessageType,
            Direction = MessageDirection.DeviceToServer,
            RawPayload = rawPayload
        });

        // Handle ACK messages from device
        if (Context.MessageType?.StartsWith("ACK", StringComparison.OrdinalIgnoreCase) == true)
        {
            await HandleInboundAckAsync();
            // After handling ACK, try to send next queued message
            await TrySendNextOutboundAsync();
            return;
        }

        var handlers = ResolveHandlers();
        if (handlers.Count == 0)
        {
            _logInfo($"Session {Context.SessionId}: No handlers registered for vendor '{_boundVendor?.VendorKey}'.");
            return;
        }

        Func<Task> next = () => Task.CompletedTask;
        foreach (var handler in handlers.AsEnumerable().Reverse())
        {
            var downstream = next;
            next = () => handler.HandleAsync(Context, downstream);
        }

        await next();

        // First send any acknowledgement
        // Only send ACK if not suppressed
        if (!Context.SuppressAutoAck)
        {
            await SendAcknowledgementIfNeededAsync();
        }

        // Then try to send next outbound message if we're not waiting for an ACK
        await TrySendNextOutboundAsync();
    }

    /// <summary>
    /// Handles an inbound ACK.R01 message from the device.
    /// Correlates with _currentOutbound and notifies the handler.
    /// </summary>
    private Task HandleInboundAckAsync()
    {
        if (Context.CurrentXDocument?.Root is null)
        {
            _logInfo($"[ACK] Session {Context.SessionId}: Received ACK but could not parse XML.");
            return Task.CompletedTask;
        }

        var ackElement = Context.CurrentXDocument.Root.Element("ACK");
        if (ackElement is null)
        {
            _logInfo($"[ACK] Session {Context.SessionId}: Received ACK message without ACK element.");
            return Task.CompletedTask;
        }

        var ackControlIdStr = ackElement.Element("ACK.ack_control_id")?.Attribute("V")?.Value;
        var typeCd = ackElement.Element("ACK.type_cd")?.Attribute("V")?.Value;
        var errorMsg = ackElement.Element("ACK.error_msg")?.Attribute("V")?.Value;

        if (string.IsNullOrWhiteSpace(ackControlIdStr) || !int.TryParse(ackControlIdStr, out var ackControlId))
        {
            _logInfo($"[ACK] Session {Context.SessionId}: Could not parse ACK.ack_control_id.");
            return Task.CompletedTask;
        }

        _logInfo($"[ACK] Session {Context.SessionId}: Received {typeCd ?? "unknown"} for control ID {ackControlId}.");

        if (_currentOutbound is null)
        {
            _logInfo($"[ACK] Session {Context.SessionId}: Received ACK but no message awaiting acknowledgement.");
            return Task.CompletedTask;
        }

        if (_currentOutbound.ControlId != ackControlId)
        {
            _logError(
                $"[ACK] Session {Context.SessionId}: ACK control ID {ackControlId} does not match pending message control ID {_currentOutbound.ControlId}.");
            return Task.CompletedTask;
        }

        // Notify the listener
        var listener = _currentOutbound.AckListener;
        if (listener is not null)
        {
            if (string.Equals(typeCd, "AA", StringComparison.OrdinalIgnoreCase))
            {
                listener.OnOutboundAcknowledged(ackControlId);
            }
            else if (string.Equals(typeCd, "AE", StringComparison.OrdinalIgnoreCase))
            {
                var abort = listener.OnOutboundError(ackControlId, errorMsg);
                if (abort)
                {
                    AbortPendingMessagesForHandler(listener);
                }
            }
            else
            {
                // Unknown type code - treat as error
                listener.OnOutboundError(ackControlId, $"Unknown ACK type: {typeCd}");
            }
        }

        // Clear the current outbound - we can now send the next one
        _currentOutbound = null;

        return Task.CompletedTask;
    }

    /// <summary>
    /// Attempts to send the next outbound message if we're not waiting for an ACK.
    /// </summary>
    private async Task TrySendNextOutboundAsync()
    {
        // Only send if we're not waiting for an ACK
        while (_currentOutbound is null && _pendingOutbound.Count > 0)
        {
            var outbound = _pendingOutbound.Dequeue();

            _logInfo($"[SEND] Session {Context.SessionId}: Sending message with control ID {outbound.ControlId}.");

            // Add to message history
            Context.MessageHistory.Add(new SessionMessage
            {
                MessageType = TryGetMessageType(outbound.Payload),
                Direction = MessageDirection.ServerToDevice,
                RawPayload = outbound.Payload
            });

            await _sendRawAsync(outbound.Payload);

            if (outbound.ExpectsAck)
            {
                // This message blocks until ACK arrives
                _currentOutbound = outbound;
                break;
            }

            // Fire-and-forget: optionally treat as a success immediately
            outbound.AckListener?.OnOutboundAcknowledged(outbound.ControlId);
            // Loop continues and sends the next message immediately
        }
    }
    
    private static string TryGetMessageType(string payload)
    {
        try
        {
            var doc = XDocument.Parse(payload);
            return doc.Root?.Name.LocalName ?? "Unknown";
        }
        catch
        {
            return "Unknown";
        }
    }

    private void BindVendor(string rawPayload)
    {
        var bytes = Encoding.UTF8.GetBytes(rawPayload);
        var packet = new RawInitialPacket(bytes, rawPayload);
        var vendor = _vendorRegistry.DetectVendor(packet, _logInfo);

        if (vendor is null)
        {
            var message = $"Session {Context.SessionId}: Unable to detect vendor.";
            _logError(message);
            throw new InvalidOperationException(message);
        }

        _boundVendor = vendor;
        Context.Items["VendorKey"] = vendor.VendorKey;
        _handlers = BuildHandlers(vendor);
    }

    private List<HandlerBase> BuildHandlers(IVendorDevicePack vendor)
    {
        var handlers = new List<HandlerBase>();

        foreach (var handlerType in vendor.GetHandlerTypes())
        {
            if (!typeof(HandlerBase).IsAssignableFrom(handlerType))
            {
                throw new InvalidOperationException(
                    $"Handler type '{handlerType.FullName}' does not inherit HandlerBase.");
            }

            if (handlerType.GetConstructor(Type.EmptyTypes) is null)
            {
                throw new InvalidOperationException(
                    $"Handler type '{handlerType.FullName}' must have a public parameterless constructor.");
            }

            var handler = (HandlerBase)Activator.CreateInstance(handlerType)!;

            // Instead of sending immediately, queue messages to be sent after ACK.
            handler.SendRawAsync = EnqueueOutboundAsync;
            handler.LogInfo = _logInfo;
            handler.LogError = _logError;

            handlers.Add(handler);
        }

        return handlers;
    }

    private Task EnqueueOutboundAsync(string raw, IOutboundAckListener? listener, bool expectsAck)
    {
        if (raw is null)
        {
            throw new ArgumentNullException(nameof(raw));
        }

        // Process the payload: replace tokens and/or inject HDR
        var (processedPayload, controlId) = ProcessOutboundPayload(raw);

        var outboundMessage = new OutboundMessage(processedPayload, controlId, listener, expectsAck);
        _pendingOutbound.Enqueue(outboundMessage);

        _logInfo(
            $"[QUEUE] Session {Context.SessionId}: Queued message with control ID {controlId}. Queue depth: {_pendingOutbound.Count}.");

        return Task.CompletedTask;
    }

    /// <summary>
    /// Processes an outbound payload by:
    /// 1. Replacing tokens ({{ control_id }}, {{ datetime_now[:format] }})
    /// 2. Injecting HDR element if not present
    /// Returns the processed payload and the assigned control ID.
    /// </summary>
    private (string ProcessedPayload, int ControlId) ProcessOutboundPayload(string raw)
    {
        var now = DateTimeOffset.Now;
        var hasTokens = raw.Contains("{{") && raw.Contains("}}");

        // Try to parse as XML
        XDocument? doc = null;
        try
        {
            doc = XDocument.Parse(raw, LoadOptions.PreserveWhitespace);
        }
        catch
        {
            doc = null;
        }

        // 1. NON-XML → engine must own the control ID
        if (doc is null || doc.Root is null)
        {
            var controlId = GetNextOutboundControlId();

            var processed = hasTokens
                ? ReplaceTokens(raw, controlId, now)
                : raw;

            return (processed, controlId);
        }

        var root = doc.Root;
        var hdr = root.Element("HDR");

        // 2. NO HDR → create engine-owned HDR
        if (hdr is null)
        {
            var controlId = GetNextOutboundControlId();
            var messageType = root.Name.LocalName;

            var hdrElement = new XElement(
                "HDR",
                new XElement("HDR.message_type", new XAttribute("V", messageType)),
                new XElement("HDR.control_id", new XAttribute("V", controlId.ToString(CultureInfo.InvariantCulture))),
                new XElement("HDR.version_id", new XAttribute("V", "POCT1")),
                new XElement("HDR.creation_dttm", new XAttribute("V",
                    now.ToString("yyyy-MM-dd'T'HH:mm:ss.ffK", CultureInfo.InvariantCulture)))
            );

            root.AddFirst(hdrElement);

            var xmlString = doc.ToString(SaveOptions.DisableFormatting);

            if (xmlString.Contains("{{") && xmlString.Contains("}}"))
                xmlString = ReplaceTokens(xmlString, controlId, now);

            return (xmlString, controlId);
        }

        // 3. HDR EXISTS
        var hdrString = hdr.ToString();
        var hdrHasTokens = hdrString.Contains("{{") && hdrString.Contains("}}");

        // 3A. HDR contains tokens → engine owns control ID
        if (hdrHasTokens)
        {
            var controlId = GetNextOutboundControlId();

            var processed = ReplaceTokens(
                doc.ToString(SaveOptions.DisableFormatting),
                controlId,
                now);

            return (processed, controlId);
        }

        // 3B. HDR has NO tokens → vendor may own control ID
        var controlIdElement = hdr.Element("HDR.control_id");
        var controlIdAttr = controlIdElement?.Attribute("V");

        if (controlIdAttr is not null &&
            int.TryParse(controlIdAttr.Value, NumberStyles.Integer, CultureInfo.InvariantCulture,
                out var vendorControlId))
        {
            // Vendor-supplied ID is valid → vendor owns control ID
            var xmlString = doc.ToString(SaveOptions.DisableFormatting);

            if (xmlString.Contains("{{") && xmlString.Contains("}}"))
                xmlString = ReplaceTokens(xmlString, vendorControlId, now);

            return (xmlString, vendorControlId);
        }

        // 3C. Vendor attempted control ID but it's missing/malformed → fallback to engine's
        var fallbackId = GetNextOutboundControlId();

        if (controlIdElement is null)
        {
            // Create the element since vendor didn't supply one
            hdr.Add(new XElement("HDR.control_id",
                new XAttribute("V", fallbackId.ToString(CultureInfo.InvariantCulture))));
        }
        else
        {
            // Overwrite malformed vendor attribute
            controlIdElement.SetAttributeValue("V", fallbackId.ToString(CultureInfo.InvariantCulture));
        }

        var fallbackXml = doc.ToString(SaveOptions.DisableFormatting);

        if (fallbackXml.Contains("{{") && fallbackXml.Contains("}}"))
            fallbackXml = ReplaceTokens(fallbackXml, fallbackId, now);

        return (fallbackXml, fallbackId);
    }


    /// <summary>
    /// Replaces tokens in the payload:
    /// - {{ control_id }} -> the control ID
    /// - {{ datetime_now }} -> current datetime in default format
    /// - {{ datetime_now:format }} -> current datetime in specified format
    /// </summary>
    private static string ReplaceTokens(string payload, int controlId, DateTimeOffset now)
    {
        // Replace {{ control_id }}
        payload = ControlIdTokenRegex().Replace(payload, controlId.ToString(CultureInfo.InvariantCulture));

        // Replace {{ datetime_now }} with default format
        payload = DateTimeNowDefaultRegex().Replace(payload,
            now.ToString(DefaultDateTimeFormat, CultureInfo.InvariantCulture));

        // Replace {{ datetime_now:format }}
        payload = DateTimeNowFormatRegex().Replace(payload, match =>
        {
            var format = match.Groups["format"].Value;
            return now.ToString(format, CultureInfo.InvariantCulture);
        });

        return payload;
    }

    [GeneratedRegex(@"\{\{\s*control_id\s*\}\}", RegexOptions.IgnoreCase)]
    private static partial Regex ControlIdTokenRegex();

    [GeneratedRegex(@"\{\{\s*datetime_now\s*\}\}", RegexOptions.IgnoreCase)]
    private static partial Regex DateTimeNowDefaultRegex();

    [GeneratedRegex(@"\{\{\s*datetime_now\s*:\s*(?<format>[^}]+)\s*\}\}", RegexOptions.IgnoreCase)]
    private static partial Regex DateTimeNowFormatRegex();

    private IReadOnlyList<HandlerBase> ResolveHandlers()
    {
        return _handlers ?? (IReadOnlyList<HandlerBase>)Array.Empty<HandlerBase>();
    }

    private static XDocument? TryParseXml(string rawPayload)
    {
        var trimmed = rawPayload.Trim();
        if (!trimmed.StartsWith("<", StringComparison.Ordinal))
        {
            return null;
        }

        try
        {
            return XDocument.Parse(trimmed, LoadOptions.PreserveWhitespace);
        }
        catch
        {
            return null;
        }
    }

    private static string DetermineNonXmlMessageType(string rawPayload)
    {
        var trimmed = rawPayload.Trim();
        if (trimmed.Length == 0)
        {
            return string.Empty;
        }

        var delimiters = new[] { '|', '^', '~', '\r', '\n', '\t', ' ' };
        var index = trimmed.IndexOfAny(delimiters);
        return index > 0 ? trimmed[..index] : trimmed;
    }

    /// <summary>
    /// Sends a POCT1A acknowledgement back to the device.
    /// - AA when Context.ErrorMessage is null/empty.
    /// - AE when Context.ErrorMessage is set, including the text.
    /// </summary>
    private async Task SendAcknowledgementIfNeededAsync()
    {
        if (Context.CurrentXDocument is null)
        {
            return;
        }

        var messageType = Context.MessageType ?? string.Empty;

        if (messageType.StartsWith("ACK", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var hdr = Context.CurrentXDocument.Root?.Element("HDR");
        var inboundControlId = hdr?.Element("HDR.control_id")?.Attribute("V")?.Value;
        if (string.IsNullOrWhiteSpace(inboundControlId))
        {
            _logInfo($"[ACK] Session {Context.SessionId}: Skipping ACK – no HDR.control_id found.");
            return;
        }

        var versionId =
            hdr?.Element("HDR.version_id")?.Attribute("V")?.Value
            ?? "POCT1";

        var outboundControlId = GetNextOutboundControlId();
        var errorMessage = Context.ErrorMessage;

        var ackDocument = _messageFactory.CreateAck(
            inboundControlId,
            versionId,
            outboundControlId,
            errorMessage);

        var ackString = ackDocument.ToString(SaveOptions.DisableFormatting);

        _logInfo(
            $"[ACK] Session {Context.SessionId}: Sending {(string.IsNullOrWhiteSpace(errorMessage) ? "ACK (AA)" : "NAK (AE)")} " +
            $"for inbound control ID '{inboundControlId}', outbound control ID '{outboundControlId}'.");

        // Add ACK to message history
        Context.MessageHistory.Add(new SessionMessage
        {
            MessageType = "ACK.R01",
            Direction = MessageDirection.ServerToDevice,
            RawPayload = ackString
        });

        await _sendRawAsync(ackString);
    }

    private void AbortPendingMessagesForHandler(IOutboundAckListener handler)
    {
        var messagesToKeep = new Queue<OutboundMessage>();
        var abortedCount = 0;

        while (_pendingOutbound.Count > 0)
        {
            var message = _pendingOutbound.Dequeue();

            if (ReferenceEquals(message.AckListener, handler))
            {
                abortedCount++;
                _logInfo(
                    $"[ABORT] Session {Context.SessionId}: Aborted message with control ID {message.ControlId} for handler.");
            }
            else
            {
                messagesToKeep.Enqueue(message);
            }
        }

        while (messagesToKeep.Count > 0)
        {
            _pendingOutbound.Enqueue(messagesToKeep.Dequeue());
        }

        if (abortedCount > 0)
        {
            _logInfo(
                $"[ABORT] Session {Context.SessionId}: Aborted {abortedCount} pending message(s) for handler. Queue depth: {_pendingOutbound.Count}.");
        }
    }
}