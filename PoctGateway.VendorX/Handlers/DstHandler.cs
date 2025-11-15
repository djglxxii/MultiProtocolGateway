using System.Threading.Tasks;
using PoctGateway.Core.Handlers;
using PoctGateway.Core.Session;

namespace PoctGateway.VendorX.Handlers;

[PoctHandler(order: 10, messageType: "DST.R01")]
public sealed class DstHandler : HandlerBase
{
    public override Task HandleAsync(SessionContext ctx, Func<Task> next)
    {
        var root = ctx.CurrentXDocument?.Root;
        if (root != null)
        {
            ctx.Items["HasNewResults"] = true;
            ctx.Items["NeedsOperatorUpdate"] = true;
        }

        LogInfo?.Invoke($"[DST] Session {ctx.SessionId}: HasNewResults=true, NeedsOperatorUpdate=true (POC).");

        return next();
    }
}
