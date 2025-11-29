using System;
using System.Linq;
using System.Threading.Tasks;
using PoctGateway.Core.Engine;
using PoctGateway.Core.Handlers;
using PoctGateway.Core.Session;

namespace PoctGateway.Analyzers.GeneXpert.Handlers;

/// <summary>
/// Handler for patient query responses.
/// Demonstrates the use of token templates for control_id and datetime_now.
/// </summary>
public sealed class PTQRY_Handler : HandlerBase, IOutboundAckListener
{
    public override async Task HandleAsync(SessionContext ctx, Func<Task> next)
    {
        if (ctx.MessageType == "HEL.R01")
        {
            var doc = ctx.CurrentXDocument;
            var vendorSpecific = doc?.Descendants("DCP.vendor_specific").FirstOrDefault();
            if (vendorSpecific != null)
            {
                string raw = vendorSpecific.Value?.Trim() ?? "";
                var parts = raw.Split('=');
                if (parts.Length > 1)
                {
                    var pid = parts[1];
                
                    // Use token templates - the engine will replace {{ control_id }} and {{ datetime_now }}
                    // with the actual values when the message is processed for sending
                    string xml = @"
<DTV.CEPHEID.PTQRY>
  <HDR>
    <HDR.message_type V=""DTV.CEPHEID.PTQRY"" SN=""CEPHEID"" SV=""2.0"" />
    <HDR.control_id V=""{{ control_id }}"" />
    <HDR.version_id V=""POCT1"" />
    <HDR.creation_dttm V=""{{ datetime_now:yyyy-MM-dd'T'HH:mm:ssK }}"" />
  </HDR>
  <DTV>
    <DTV.command_cd V=""1"" SN=""CEPHEID"" SV=""2.0"" />
  </DTV>
  <PT>
    <PT.patient_id V=""" + pid + @""" />
    <PT.name V=""Jane Doe"" />
    <PT.birth_date V=""2011-11-01T17:19:10-07:00"" />
  </PT>
</DTV.CEPHEID.PTQRY>";
                    await SendAsync(xml, this);
                }
            }
        }
        
        await next();
    }

    public void OnOutboundAcknowledged(int controlId)
    {
        LogInfo?.Invoke($"[PTQRY] Patient query response with control ID {controlId} was acknowledged.");
    }

    public bool OnOutboundError(int controlId, string? errorMessage)
    {
        LogError?.Invoke($"[PTQRY] Patient query response with control ID {controlId} was rejected: {errorMessage ?? "No error message"}");
        return true;
    }
}
