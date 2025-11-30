using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Gateway.Core
{
    /// <summary>
    /// Executes a sequence of handlers for processing messages.
    /// </summary>
    public sealed class HandlerPipeline
    {
        private readonly IList<HandlerBase> _handlers;

        /// <summary>
        /// Creates a pipeline with the specified handlers.
        /// </summary>
        /// <param name="handlers">Ordered list of handlers to execute.</param>
        public HandlerPipeline(IList<HandlerBase> handlers)
        {
            _handlers = handlers ?? throw new ArgumentNullException(nameof(handlers));
        }

        /// <summary>
        /// Executes the pipeline for the given session context.
        /// </summary>
        /// <param name="context">The session context to process.</param>
        public Task ExecuteAsync(SessionContext context)
        {
            return InvokeNext(context, 0);
        }

        private Task InvokeNext(SessionContext context, int index)
        {
            if (index >= _handlers.Count)
            {
                return Task.CompletedTask;
            }

            HandlerBase handler = _handlers[index];
            return handler.HandleAsync(context, () => InvokeNext(context, index + 1));
        }
    }
}
