using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PoctGateway.Core.Handlers;
using PoctGateway.Core.Session;
using PoctGateway.Core.Vendors;

namespace PoctGateway.Host.StubVendors;

public sealed class Hl7StubDevicePack : IVendorDevicePack
{
    public string VendorKey => "HL7Stub";
    public string ProtocolKind => "HL7";

    public bool IsMatch(RawInitialPacket packet)
        => packet.RawText.TrimStart().StartsWith("MSH|", StringComparison.OrdinalIgnoreCase);

    public IReadOnlyCollection<Type> GetHandlerTypes()
        => new[] { typeof(Hl7StubHandler) };
}

public sealed class Hl7StubHandler : HandlerBase
{
    public override Task HandleAsync(SessionContext ctx, Func<Task> next)
    {
        LogInfo?.Invoke($"[HL7] Session {ctx.SessionId}: {ctx.CurrentRaw}");
        return next();
    }
}
