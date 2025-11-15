using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PoctGateway.Core.Handlers;
using PoctGateway.Core.Session;
using PoctGateway.Core.Vendors;

namespace PoctGateway.Host.StubVendors;

public sealed class AstmStubDevicePack : IVendorDevicePack
{
    public string VendorKey => "AstmStub";
    public string ProtocolKind => "ASTM";

    public bool IsMatch(RawInitialPacket packet)
    {
        var text = packet.RawText.TrimStart();
        return text.StartsWith("ASTM|", StringComparison.OrdinalIgnoreCase)
               || text.StartsWith("\u0002", StringComparison.Ordinal);
    }

    public IReadOnlyCollection<Type> GetHandlerTypes()
        => new[] { typeof(AstmStubHandler) };
}

[PoctHandler(order: 0, messageType: null)]
public sealed class AstmStubHandler : HandlerBase
{
    public override Task HandleAsync(SessionContext ctx, Func<Task> next)
    {
        LogInfo?.Invoke($"[ASTM] Session {ctx.SessionId}: raw length {ctx.CurrentRaw.Length} characters.");
        return next();
    }
}
