using PoctGateway.Core.Engine;
using PoctGateway.Core.Session;

namespace PoctGateway.Core.Handlers;

public abstract class HandlerBase
{
    public virtual Task HandleAsync(SessionContext ctx, Func<Task> next)
        => next();

    protected internal Func<string, IOutboundAckListener?, Task>? SendRawAsync { get; internal set; }
    protected internal Action<string>? LogInfo { get; internal set; }
    protected internal Action<string>? LogError { get; internal set; }

    /// <summary>
    /// Queues an outbound message for delivery. The message will be sent
    /// after any pending ACK is received.
    /// </summary>
    /// <param name="raw">The raw message payload (may contain tokens or be bare body).</param>
    protected Task SendAsync(string raw)
        => SendAsync(raw, null);

    /// <summary>
    /// Queues an outbound message for delivery with an ACK listener.
    /// </summary>
    /// <param name="raw">The raw message payload (may contain tokens or be bare body).</param>
    /// <param name="listener">Optional listener to notify on ACK/NAK.</param>
    protected Task SendAsync(string raw, IOutboundAckListener? listener)
        => SendRawAsync != null ? SendRawAsync(raw, listener) : Task.CompletedTask;
}
