using System;
using System.IO;
using System.Threading.Tasks;
using PoctGateway.Core.Engine;
using PoctGateway.Core.Handlers;
using PoctGateway.Core.Session;

namespace PoctGateway.Analyzers.GeneXpert.Handlers;

public sealed class PTL_Handler : HandlerBase, IOutboundAckListener
{
    private static readonly string[] PtlFiles =
    [
        "ptl_output_chunk_1.xml",
        "ptl_output_chunk_2.xml",
        "ptl_output_chunk_3.xml",
        "ptl_output_chunk_4.xml",
        "ptl_output_chunk_5.xml",
        "ptl_output_chunk_6.xml"
    ];
    
    public override async Task HandleAsync(SessionContext ctx, Func<Task> next)
    {
        if (ctx.MessageType == "DST.R01")
        {
            foreach (var ptlFile in PtlFiles)
            {
                var path = Path.Combine(AppContext.BaseDirectory, "Data", ptlFile);
                var ptl = await File.ReadAllTextAsync(path);
                // Pass 'this' as the ACK listener to receive notifications
                if (ptl.Contains("<EOT.R01>"))
                {
                    await SendAsync(ptl, this, expectsAck: false);
                }
                else
                {
                    await SendAsync(ptl, this);
                }
                
            }
        }
        
        await next();
    }

    /// <summary>
    /// Called when the device acknowledges an OPL message.
    /// </summary>
    public void OnOutboundAcknowledged(int controlId)
    {
        LogInfo?.Invoke($"[PTL] PTL message with control ID {controlId} was acknowledged.");
    }

    /// <summary>
    /// Called when the device rejects an OPL message.
    /// </summary>
    public bool OnOutboundError(int controlId, string? errorMessage)
    {
        LogError?.Invoke($"[PTL] PTL message with control ID {controlId} was rejected: {errorMessage ?? "No error message"}");
        return true;
    }
}
