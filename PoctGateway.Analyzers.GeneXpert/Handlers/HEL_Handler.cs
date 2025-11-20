using System;
using System.Threading.Tasks;
using PoctGateway.Core.Handlers;
using PoctGateway.Core.Helpers;
using PoctGateway.Core.Protocol.Poct1A.HelR01;
using PoctGateway.Core.Session;

namespace PoctGateway.Analyzers.GeneXpert.Handlers;

public sealed class HEL_Handler : HandlerBase
{
    public override async Task HandleAsync(SessionContext ctx, Func<Task> next)
    {
        if (ctx.MessageType == "HEL.R01")
        {
            var hel = ctx.GetModel<HelMessage>();
        }
        
        await next();
    }
}
