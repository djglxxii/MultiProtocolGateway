using System;
using System.IO;
using System.Threading.Tasks;
using PoctGateway.Core.Engine;
using PoctGateway.Core.Handlers;
using PoctGateway.Core.Session;

namespace PoctGateway.Analyzers.GeneXpert.Handlers;

/// <summary>
/// Handler for sending OPL (Operator List) messages.
/// Implements IOutboundAckListener to receive notifications when messages are acknowledged or rejected.
/// </summary>
public sealed class OPL_Handler : HandlerBase, IOutboundAckListener
{
    private static readonly string[] OplFiles =
    [
        "opl_output_chunk_1.xml",
        "opl_output_chunk_2.xml",
        "opl_output_chunk_3.xml",
        "opl_output_chunk_4.xml",
        "opl_output_chunk_5.xml"
    ];
    
    public override async Task HandleAsync(SessionContext ctx, Func<Task> next)
    {
        if (ctx.MessageType == "DST.R01")
        {
            foreach (var oplFile in OplFiles)
            {
                var path = Path.Combine(AppContext.BaseDirectory, "Data", oplFile);
                var opl = await File.ReadAllTextAsync(path);
                // Pass 'this' as the ACK listener to receive notifications
                await SendAsync(opl, this);
            }
        }
        
        await next();
    }

    /// <summary>
    /// Called when the device acknowledges an OPL message.
    /// </summary>
    public void OnOutboundAcknowledged(int controlId)
    {
        LogInfo?.Invoke($"[OPL] OPL message with control ID {controlId} was acknowledged.");
    }

    /// <summary>
    /// Called when the device rejects an OPL message.
    /// </summary>
    public void OnOutboundError(int controlId, string? errorMessage)
    {
        LogError?.Invoke($"[OPL] OPL message with control ID {controlId} was rejected: {errorMessage ?? "No error message"}");
    }
}
