using System;
using System.Threading.Tasks;
using System.Xml.Linq;
using PoctGateway.Core.Handlers;
using PoctGateway.Core.Session;

namespace PoctGateway.VendorX.Handlers;

[PoctHandler(order: 0, messageType: "HEL.R01")]
public sealed class HelHandler : HandlerBase
{
    public override Task HandleAsync(SessionContext ctx, Func<Task> next)
    {
        var dev = ctx.CurrentXDocument?.Root?.Element("DEV");
        if (dev != null)
        {
            ctx.Items["DeviceId"] = dev.Element("DEV.device_id")?.Attribute("V")?.Value ?? string.Empty;
            ctx.Items["VendorId"] = dev.Element("DEV.vendor_id")?.Attribute("V")?.Value ?? string.Empty;
            ctx.Items["ModelId"] = dev.Element("DEV.model_id")?.Attribute("V")?.Value ?? string.Empty;
        }

        var modelId = ctx.Items.TryGetValue("ModelId", out var model) ? model as string : null;
        LogInfo?.Invoke($"[HEL] Session {ctx.SessionId} initialized for device '{modelId ?? "unknown"}'.");

        return next();
    }
}
