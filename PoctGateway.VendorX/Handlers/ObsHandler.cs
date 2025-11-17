using System.Threading.Tasks;
using PoctGateway.Core.Handlers;
using PoctGateway.Core.Session;

namespace PoctGateway.VendorX.Handlers;

[PoctHandler(messageType: null)]
public sealed class ObsHandler : HandlerBase
{
    private const string ObsStateKey = "VendorX.ObsState";

    private sealed class ObsState
    {
        public bool IsActive { get; set; }
        public int ObsMessageCount { get; set; }
    }

    public override async Task HandleAsync(SessionContext ctx, Func<Task> next)
    {
        var state = GetOrCreateState(ctx);

        switch (ctx.MessageType)
        {
            case "DST.R01":
                await HandleDst(ctx, state);
                break;
            case "OBS.R01":
                HandleObs(ctx, state);
                break;
            case "EOT.R01":
                HandleEot(ctx, state);
                break;
        }

        await next();
    }

    private static ObsState GetOrCreateState(SessionContext ctx)
    {
        if (!ctx.Items.TryGetValue(ObsStateKey, out var obj) || obj is not ObsState state)
        {
            state = new ObsState();
            ctx.Items[ObsStateKey] = state;
        }

        return state;
    }

    private async Task HandleDst(SessionContext ctx, ObsState state)
    {
        var raw = ctx.CurrentRaw;
        if (raw.Contains("DST.new_observations_qty"))
        {
            await SendAsync(@"<REQ><REQ.request_cd V=""ROBS""/></REQ>");
        }
    }

    private void HandleObs(SessionContext ctx, ObsState state)
    {
        if (!state.IsActive)
        {
            state.IsActive = true;
            state.ObsMessageCount = 0;
            LogInfo?.Invoke($"[OBS] Session {ctx.SessionId}: OBS topic started.");
        }

        state.ObsMessageCount++;
        LogInfo?.Invoke($"[OBS] Session {ctx.SessionId}: Received OBS message #{state.ObsMessageCount}.");
    }

    private void HandleEot(SessionContext ctx, ObsState state)
    {
        if (!state.IsActive)
        {
            LogInfo?.Invoke($"[EOT] Session {ctx.SessionId}: EOT received but OBS state not active (ignoring).");
            return;
        }

        LogInfo?.Invoke($"[OBS] Session {ctx.SessionId}: OBS topic completed after {state.ObsMessageCount} messages.");
        state.IsActive = false;
        state.ObsMessageCount = 0;
    }
}