using System;
using System.Diagnostics;
using System.Threading.Tasks;
using PoctGateway.Core.Handlers;
using PoctGateway.Core.Protocol.Poct1A.DST;
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
        var doc = ctx.CurrentXDocument!;
        var facade = new DstFacade(doc);
        var dst = facade.ToModel();
        if (int.TryParse(dst.Dst.NewObservationsQuantity, out var newObsQty))
        {
            if (newObsQty > 0)
            {
                await SendAsync(@"<REQ><REQ.request_cd V=""ROBS""/></REQ>");    
            }
        }
    }

    private void HandleObs(SessionContext ctx)
    {
        var doc = ctx.CurrentXDocument!;
        var facade = new ObsFacade(doc);
        var obs = facade.ToModel();
        
        Debugger.Break();
    }

    private void HandleEot(SessionContext ctx)
    {
        var doc = ctx.CurrentXDocument!;
        var facade = new EotFacade(doc);
        var eot = facade.ToModel();
        if (eot.Eot.TopicCode == "OBS")
        {
            LogInfo?.Invoke($"[EOT] Session {ctx.SessionId}: OBS topic completed.");
        }
    }
}