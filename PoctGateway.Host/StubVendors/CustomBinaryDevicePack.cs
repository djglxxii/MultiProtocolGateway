using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PoctGateway.Core.Handlers;
using PoctGateway.Core.Session;
using PoctGateway.Core.Vendors;

namespace PoctGateway.Host.StubVendors;

public sealed class CustomBinaryDevicePack : IVendorDevicePack
{
    public string VendorKey => "CustomBinary";
    public string ProtocolKind => "BINARY";

    public bool IsMatch(RawInitialPacket packet)
    {
        var span = packet.RawBytes.Span;
        return span.Length >= 2 && span[0] == 0xAA && span[1] == 0x55;
    }

    public IReadOnlyCollection<Type> GetHandlerTypes()
        => new[] { typeof(CustomBinaryHandler) };
}

public sealed class CustomBinaryHandler : HandlerBase
{
    public override Task HandleAsync(SessionContext ctx, Func<Task> next)
    {
        LogInfo?.Invoke($"[BIN] Session {ctx.SessionId}: handled proprietary frame ({ctx.CurrentRaw.Length} chars shown).");
        return next();
    }
}
