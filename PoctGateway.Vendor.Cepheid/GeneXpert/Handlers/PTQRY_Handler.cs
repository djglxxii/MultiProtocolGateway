using PoctGateway.Core.Handlers;
using PoctGateway.Core.Session;

namespace PoctGateway.Vendor.Cepheid.GeneXpert.Handlers;

public class PTQRY_Handler : HandlerBase
{
    public override async Task HandleAsync(SessionContext ctx, Func<Task> next)
    {
        if (ctx.MessageType == "HEL.R01")
        {
            var doc = ctx.CurrentXDocument;
            var vendorSpecific = doc.Descendants("DCP.vendor_specific").FirstOrDefault();
            if (vendorSpecific != null)
            {
                string raw = vendorSpecific?.Value?.Trim() ?? "";
                var parts = raw.Split('=');
                var pid = parts[1];
            
                string xml = @"
<DTV.CEPHEID.PTQRY>
  <HDR>
    <HDR.message_type V=""DTV.CEPHEID.PTQRY"" SN=""CEPHEID"" SV=""2.0"" />
    <HDR.control_id V=""545"" />
    <HDR.version_id V=""POCT1"" />
    <HDR.creation_dttm V=""2023-01-12T01:44:28-08:00"" />
  </HDR>
  <DTV>
    <DTV.command_cd V=""1"" SN=""CEPHEID"" SV=""2.0"" />
  </DTV>
  <PT>
    <PT.patient_id V=""1268"" />
    <PT.name V=""Jane Doe"" />
    <PT.birth_date V=""2011-11-01T17:19:10-07:00"" />
  </PT>
</DTV.CEPHEID.PTQRY>";
                await SendAsync(xml);
            }
        }
        
        await next();
    }
}
