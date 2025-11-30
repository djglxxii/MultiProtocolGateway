using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Gateway.Core
{
    /// <summary>
    /// A simple line-based session engine that reads text lines and processes them through a handler pipeline.
    /// </summary>
    public sealed class TextSessionEngine : ISessionEngine
    {
        private readonly SessionContext _context;
        private readonly HandlerPipeline _pipeline;
        private const int MaxHistorySize = 50;

        /// <summary>
        /// Creates a new TextSessionEngine.
        /// </summary>
        /// <param name="context">The session context.</param>
        /// <param name="handlers">The handlers to execute for each message.</param>
        public TextSessionEngine(SessionContext context, IList<HandlerBase> handlers)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _pipeline = new HandlerPipeline(handlers ?? new List<HandlerBase>());
        }

        /// <summary>
        /// Runs the session engine, processing messages until the session ends or is cancelled.
        /// </summary>
        public async Task RunAsync(ITransport transport, CancellationToken cancellationToken)
        {
            // Store transport reference in context for handlers
            _context.Transport = transport;

            // Transition to Active state after negotiation (for PoC, immediate)
            if (_context.LifecycleState == SessionLifecycleState.Negotiating)
            {
                _context.LifecycleState = SessionLifecycleState.Active;
            }

            while (_context.LifecycleState != SessionLifecycleState.Closed && !cancellationToken.IsCancellationRequested)
            {
                string line = await transport.ReadLineAsync(cancellationToken).ConfigureAwait(false);

                if (line == null)
                {
                    // Connection closed by remote
                    _context.LifecycleState = SessionLifecycleState.Closed;
                    break;
                }

                if (line.Length == 0)
                {
                    // Ignore blank lines in PoC
                    continue;
                }

                _context.Mode = ProcessingMode.InboundMessage;
                _context.CurrentMessage = line;
                _context.MessageHistory.Add(line);

                // Keep history bounded
                while (_context.MessageHistory.Count > MaxHistorySize)
                {
                    _context.MessageHistory.RemoveAt(0);
                }

                await _pipeline.ExecuteAsync(_context).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Processes a single message through the pipeline.
        /// Used for the first message that was used for detection.
        /// </summary>
        /// <param name="message">The message to process.</param>
        /// <param name="transport">The transport for responses.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task ProcessFirstMessageAsync(string message, ITransport transport, CancellationToken cancellationToken)
        {
            // Store transport reference in context for handlers
            _context.Transport = transport;

            _context.Mode = ProcessingMode.InboundMessage;
            _context.CurrentMessage = message;
            _context.MessageHistory.Add(message);

            await _pipeline.ExecuteAsync(_context).ConfigureAwait(false);
        }
    }
}
