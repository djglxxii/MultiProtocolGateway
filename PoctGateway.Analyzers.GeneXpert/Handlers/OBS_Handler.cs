using System;
using System.Diagnostics;
using System.Threading.Tasks;
using PoctGateway.Core.Handlers;
using PoctGateway.Core.Protocol.Poct1A.EotR01;
using PoctGateway.Core.Protocol.Poct1A.ObsR01;
using PoctGateway.Core.Session;

namespace PoctGateway.Analyzers.GeneXpert.Handlers;

public sealed class OBS_Handler : HandlerBase
{
    public override async Task HandleAsync(SessionContext ctx, Func<Task> next)
    {
        switch (ctx.MessageType)
        {
            case "DST.R01":
                await HandleDst(ctx);
                break;
            case "OBS.R01":
                HandleObs(ctx);
                break;
            case "EOT.R01":
                HandleEot(ctx);
                break;
        }

        await next();
    }
    
    private async Task HandleDst(SessionContext ctx)
    {
        var raw = ctx.CurrentRaw;
        if (raw.Contains("DST.new_observations_qty"))
        {
            await SendAsync(@"<REQ><REQ.request_cd V=""ROBS""/></REQ>");
        }
    }

    private void HandleObs(SessionContext ctx)
    {
        var doc = ctx.CurrentXDocument!;
        var facade = new ObsR01Facade(doc);
        var obs = facade.ToModel();
        
        Debugger.Break();
    }

    private void HandleEot(SessionContext ctx)
    {
        var doc = ctx.CurrentXDocument!;
        var facade = new EotR01Facade(doc);
        var eot = facade.ToModel();
        if (eot.Eot.TopicCode == "OBS")
        {
            LogInfo?.Invoke($"[EOT] Session {ctx.SessionId}: OBS topic completed.");
        }
    }
}