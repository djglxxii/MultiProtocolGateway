using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using PoctGateway.Core.Handlers;
using PoctGateway.Core.Session;

namespace PoctGateway.Analyzers.GeneXpert.Handlers;

public sealed class OPL_Handler : HandlerBase
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
                await SendAsync(opl);
            }
        }
        
        await next();
    }
}

