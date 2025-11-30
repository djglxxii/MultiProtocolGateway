using System;
using System.Threading.Tasks;

namespace Gateway.Core
{
    /// <summary>
    /// Base class for message handlers in the processing pipeline.
    /// Handlers are invoked in sequence; each handler decides whether to continue to the next.
    /// </summary>
    public abstract class HandlerBase
    {
        /// <summary>
        /// Processes the current message or command in the session context.
        /// </summary>
        /// <param name="context">The session context containing message data and state.</param>
        /// <param name="next">Delegate to invoke the next handler in the pipeline.</param>
        public abstract Task HandleAsync(SessionContext context, Func<Task> next);
    }
}
