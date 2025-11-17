using PoctGateway.Core.Handlers;
using PoctGateway.Core.Session;

namespace PoctGateway.Vendor.Cepheid.GeneXpert.Handlers;

public sealed class OPL_Handler : HandlerBase
{
    private const string OplStateKey = "VendorX.OplState";

    private sealed class OplState
    {
        public bool IsInProgress { get; set; }
        public int ChunkIndex { get; set; }
    }

    public override async Task HandleAsync(SessionContext ctx, Func<Task> next)
    {
        var state = GetOrCreateState(ctx);

        switch (ctx.MessageType)
        {
            case "DST.R01":
            {
                if (ctx.Items.TryGetValue("NeedsOperatorUpdate", out var needsUpdateObj)
                    && needsUpdateObj is bool needsUpdate
                    && needsUpdate
                    && !state.IsInProgress)
                {
                    state.IsInProgress = true;
                    state.ChunkIndex = 0;
                    LogInfo?.Invoke($"[OPL] Session {ctx.SessionId}: Starting OPL push (POC).");
                    await SendNextChunkAsync(ctx, state);
                }

                break;
            }
            case "ACK.R01" when state.IsInProgress:
                await SendNextChunkAsync(ctx, state);
                break;
        }

        await next();
    }

    private static OplState GetOrCreateState(SessionContext ctx)
    {
        if (!ctx.Items.TryGetValue(OplStateKey, out var obj) || obj is not OplState state)
        {
            state = new OplState();
            ctx.Items[OplStateKey] = state;
        }

        return state;
    }

    private async Task SendNextChunkAsync(SessionContext ctx, OplState state)
    {
        state.ChunkIndex++;

        if (state.ChunkIndex <= 2)
        {
            var xml = $"<OPL.R01><CHUNK V=\"{state.ChunkIndex}\" /></OPL.R01>";
            LogInfo?.Invoke($"[OPL] Session {ctx.SessionId}: Sending OPL chunk {state.ChunkIndex}.");
            await SendAsync(xml);
        }
        else
        {
            var eot = "<EOT.R01><TOPIC V=\"OPL\" /></EOT.R01>";
            LogInfo?.Invoke($"[OPL] Session {ctx.SessionId}: Sending OPL EOT.");
            await SendAsync(eot);
            state.IsInProgress = false;
            state.ChunkIndex = 0;
        }
    }
}
