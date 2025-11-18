using System;
using System.Diagnostics;
using System.Threading.Tasks;
using PoctGateway.Core.Handlers;
using PoctGateway.Core.Protocol.Poct1A.HelR01;
using PoctGateway.Core.Session;

namespace PoctGateway.Analyzers.GeneXpert.Handlers;

public sealed class HEL_Handler : HandlerBase
{
    public override async Task HandleAsync(SessionContext ctx, Func<Task> next)
    {
        if (ctx.MessageType == "HEL.R01")
        {
            var doc = ctx.CurrentXDocument!;
            var facade = new HelR01Facade(doc);
            var hel = facade.ToModel();
        }
        
        await next();
    }
}
