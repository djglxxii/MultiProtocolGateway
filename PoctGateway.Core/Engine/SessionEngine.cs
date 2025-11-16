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

        Func<Task> next = () => Task.CompletedTask;

        foreach (var descriptor in handlers.AsEnumerable().Reverse())
        {
            var handler = descriptor.Handler;
            var downstream = next;
            next = () => handler.HandleAsync(Context, downstream);
        }

        await next();
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

        return matches
            .ToList();
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

    private sealed record HandlerDescriptor(HandlerBase Handler, IReadOnlyList<PoctHandlerAttribute> Attributes);

    private sealed record HandlerInvocation(HandlerBase Handler);
}
