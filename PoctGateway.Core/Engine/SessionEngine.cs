using System.Reflection;
using System.Text;
using System.Xml.Linq;
using PoctGateway.Core.Handlers;
using PoctGateway.Core.Session;
using PoctGateway.Core.Vendors;

namespace PoctGateway.Core.Engine;

public sealed class SessionEngine
{
    private readonly VendorRegistry _vendorRegistry;
    private readonly Func<string, Task> _sendRawAsync;
    private readonly Action<string> _logInfo;
    private readonly Action<string> _logError;

    private IVendorDevicePack? _boundVendor;
    private List<HandlerBase>? _handlers;

    // Queue for messages requested by handlers via HandlerBase.SendAsync
    private readonly Queue<string> _pendingOutbound = new();

    public SessionContext Context { get; }

    public SessionEngine(
        SessionContext context,
        VendorRegistry vendorRegistry,
        Func<string, Task> sendRawAsync,
        Action<string> logInfo,
        Action<string> logError)
    {
        Context = context ?? throw new ArgumentNullException(nameof(context));
        _vendorRegistry = vendorRegistry ?? throw new ArgumentNullException(nameof(vendorRegistry));
        _sendRawAsync = sendRawAsync ?? (_ => Task.CompletedTask);
        _logInfo = logInfo ?? (_ => { });
        _logError = logError ?? (_ => { });
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

        // Reset per-message outbound queue
        _pendingOutbound.Clear();

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
        
        // Then send all payloads requested by handlers via SendAsync
        while (_pendingOutbound.Count > 0)
        {
            var outbound = _pendingOutbound.Dequeue();
            await _sendRawAsync(outbound);
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
                throw new InvalidOperationException($"Handler type '{handlerType.FullName}' does not inherit HandlerBase.");
            }

            if (handlerType.GetConstructor(Type.EmptyTypes) is null)
            {
                throw new InvalidOperationException($"Handler type '{handlerType.FullName}' must have a public parameterless constructor.");
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

    private Task EnqueueOutboundAsync(string raw)
    {
        if (raw is null)
        {
            throw new ArgumentNullException(nameof(raw));
        }

        _pendingOutbound.Enqueue(raw);
        return Task.CompletedTask;
    }

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
        // Only XML messages can carry the HDR control ID in this model.
        if (Context.CurrentXDocument is null)
        {
            return;
        }

        var messageType = Context.MessageType ?? string.Empty;

        // Do not ACK an ACK to avoid loops.
        if (messageType.StartsWith("ACK", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        // <HDR><HDR.control_id V="..." /></HDR>
        var hdr = Context.CurrentXDocument.Root?.Element("HDR");
        var controlId = hdr?.Element("HDR.control_id")?.Attribute("V")?.Value;
        if (string.IsNullOrWhiteSpace(controlId))
        {
            return;
        }

        var hasError = !string.IsNullOrWhiteSpace(Context.ErrorMessage);
        var ackCode = hasError ? "AE" : "AA";

        var ackElement = new XElement("ACK",
            new XElement("ACK.type_cd",        new XAttribute("V", ackCode)),
            new XElement("ACK.ack_control_id", new XAttribute("V", controlId)));

        if (hasError)
        {
            ackElement.Add(
                new XElement("ACK.error_msg", new XAttribute("V", Context.ErrorMessage!)));
        }

        var ackString = ackElement.ToString(SaveOptions.DisableFormatting);

        _logInfo?.Invoke(
            $"[ACK] Session {Context.SessionId}: Sending {(hasError ? "NAK (AE)" : "ACK (AA)")} for control ID '{controlId}'.");

        await _sendRawAsync(ackString);
    }
}
