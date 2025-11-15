using System;
using System.Threading.Tasks;
using PoctGateway.Core.Session;

namespace PoctGateway.Core.Handlers;

public abstract class HandlerBase
{
    public virtual Task HandleAsync(SessionContext ctx, Func<Task> next)
        => next();

    protected internal Func<string, Task>? SendRawAsync { get; internal set; }
    protected internal Action<string>? LogInfo { get; internal set; }
    protected internal Action<string>? LogError { get; internal set; }

    protected Task SendAsync(string raw)
        => SendRawAsync != null ? SendRawAsync(raw) : Task.CompletedTask;
}
