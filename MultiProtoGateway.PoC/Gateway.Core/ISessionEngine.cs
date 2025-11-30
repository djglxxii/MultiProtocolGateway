using System.Threading;
using System.Threading.Tasks;

namespace Gateway.Core
{
    /// <summary>
    /// Manages the communication lifecycle for a single device session.
    /// </summary>
    public interface ISessionEngine
    {
        /// <summary>
        /// Runs the session engine, processing messages until the session ends.
        /// </summary>
        /// <param name="transport">The transport for communication.</param>
        /// <param name="cancellationToken">Token to signal cancellation.</param>
        Task RunAsync(ITransport transport, CancellationToken cancellationToken);
    }
}
