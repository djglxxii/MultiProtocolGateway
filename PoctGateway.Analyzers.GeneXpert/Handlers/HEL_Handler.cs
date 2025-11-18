using System;
using System.Diagnostics;
using System.Threading.Tasks;
using PoctGateway.Analyzers.GeneXpert.Facades.HelR01;
using PoctGateway.Core.Handlers;
using PoctGateway.Core.Session;

namespace PoctGateway.Analyzers.GeneXpert.Handlers;

public sealed class HEL_Handler : HandlerBase
{
    public override async Task HandleAsync(SessionContext ctx, Func<Task> next)
    {
        if (ctx.MessageType == "HEL.R01")
        {
            var doc = ctx.CurrentXDocument!;
            var hel = new GeneXpertHelR01Facade(doc).ToModel();
            ctx.Items["SerialNo"] = hel.Device.DeviceIdentifier;
            ctx.Items["DeviceName"] = hel.Device.DeviceName;
            ctx.Items["Manufacturer"] = hel.Device.ManufacturerName;
            ctx.Items["ModelId"] = hel.Device.ModelId;
        }
        
        await next();
    }
}
