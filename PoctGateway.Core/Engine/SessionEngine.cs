using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
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
    private List<HandlerDescriptor>? _handlerDescriptors;

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

        var handlers = ResolveHandlers(Context.MessageType);
        if (handlers.Count == 0)
        {
            _logInfo($"Session {Context.SessionId}: No handlers for message type '{Context.MessageType}'.");
            return;
        }

        // For this inbound message, queue all outbound messages requested by handlers.
        // These will be sent AFTER any ACK has been generated and sent.
        var deferredOutbound = new List<string>();

        Task EnqueueOutboundAsync(string payload)
        {
            if (!string.IsNullOrWhiteSpace(payload))
            {
                deferredOutbound.Add(payload);
            }

            return Task.CompletedTask;
        }

        // Temporarily replace the handlers' SendRawAsync with the deferred sender.
        var originalSendDelegates = new Dictionary<HandlerBase, Func<string, Task>?>();
        foreach (var invocation in handlers)
        {
            var handler = invocation.Handler;
            if (!originalSendDelegates.ContainsKey(handler))
            {
                originalSendDelegates[handler] = handler.SendRawAsync;
                handler.SendRawAsync = EnqueueOutboundAsync;
            }
        }

        try
        {
            Func<Task> next = () => Task.CompletedTask;

            foreach (var descriptor in handlers.AsEnumerable().Reverse())
            {
                var handler = descriptor.Handler;
                var downstream = next;
                next = () => handler.HandleAsync(Context, downstream);
            }

            // Execute handler pipeline.
            await next();

            // After all handlers: send ACK/NAK first.
            await SendAcknowledgementIfNeededAsync();

            // Then flush any outbound messages that handlers requested via SendAsync.
            foreach (var payload in deferredOutbound)
            {
                await _sendRawAsync(payload);
            }
        }
        finally
        {
            // Restore original SendRawAsync delegates to avoid leaking state across messages.
            foreach (var kvp in originalSendDelegates)
            {
                kvp.Key.SendRawAsync = kvp.Value;
            }
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
        _handlerDescriptors = BuildHandlerDescriptors(vendor);
    }

    private List<HandlerDescriptor> BuildHandlerDescriptors(IVendorDevicePack vendor)
    {
        var descriptors = new List<HandlerDescriptor>();

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
            handler.SendRawAsync = _sendRawAsync;
            handler.LogInfo = _logInfo;
            handler.LogError = _logError;

            var attributes = handlerType
                .GetCustomAttributes<PoctHandlerAttribute>(inherit: true)
                .DefaultIfEmpty(new PoctHandlerAttribute())
                .ToArray();

            descriptors.Add(new HandlerDescriptor(handler, attributes));
        }

        return descriptors;
    }

    private IReadOnlyList<HandlerInvocation> ResolveHandlers(string? messageType)
    {
        if (_handlerDescriptors is null)
        {
            return Array.Empty<HandlerInvocation>();
        }

        var normalized = messageType ?? string.Empty;
        var matches = new List<HandlerInvocation>();

        foreach (var descriptor in _handlerDescriptors)
        {
            var matchingAttribute = descriptor.Attributes
                .FirstOrDefault(a => string.IsNullOrEmpty(a.MessageType)
                                     || string.Equals(a.MessageType, normalized, StringComparison.OrdinalIgnoreCase));

            if (matchingAttribute != null)
            {
                matches.Add(new HandlerInvocation(descriptor.Handler));
            }
        }

        return matches.ToList();
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
    /// Examines the current message and sends a POCT1A acknowledgement back to the
    /// device if the message is XML with an HDR.control_id element. Acks are not
    /// sent in response to ACK messages themselves to avoid loops.
    /// </summary>
    private async Task SendAcknowledgementIfNeededAsync()
    {
        // Only XML messages can carry the HDR control ID.
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

        // Allow a handler to suppress ACK/NAK entirely.
        if (Context.Items.TryGetValue(SessionContextKeys.Ack.Suppress, out var suppressObj) &&
            suppressObj is bool suppress &&
            suppress)
        {
            return;
        }

        // Extract control ID: <HDR><HDR.control_id V="..." /></HDR>
        var hdr = Context.CurrentXDocument.Root?.Element("HDR");
        var controlId = hdr?.Element("HDR.control_id")?.Attribute("V")?.Value;
        if (string.IsNullOrWhiteSpace(controlId))
        {
            return;
        }

        // Determine ACK type: default AA, handler may override to AE (NAK).
        var ackType = "AA"; // normal ACK
        if (Context.Items.TryGetValue(SessionContextKeys.Ack.Type, out var ackTypeObj) &&
            ackTypeObj is string requested &&
            !string.IsNullOrWhiteSpace(requested))
        {
            ackType = requested; // e.g. "AE" for NAK
        }

        var ackXml = new XElement("ACK",
            new XElement("ACK.type_cd",      new XAttribute("V", ackType)),
            new XElement("ACK.ack_control_id", new XAttribute("V", controlId)));

        var ackString = ackXml.ToString(SaveOptions.DisableFormatting);

        _logInfo?.Invoke(
            $"[ACK] Session {Context.SessionId}: Sending {(ackType == "AE" ? "NAK" : "ACK")} for control ID '{controlId}'.");

        await _sendRawAsync(ackString);
    }
    
    private sealed record HandlerDescriptor(HandlerBase Handler, IReadOnlyList<PoctHandlerAttribute> Attributes);

    private sealed record HandlerInvocation(HandlerBase Handler);
}
