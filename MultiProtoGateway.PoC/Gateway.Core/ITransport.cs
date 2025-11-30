using System;
using System.Threading;
using System.Threading.Tasks;

namespace Gateway.Core
{
    /// <summary>
    /// Abstraction over the underlying network transport.
    /// Provides both raw byte and line-based operations.
    /// </summary>
    public interface ITransport : IDisposable
    {
        /// <summary>
        /// Reads bytes from the transport.
        /// </summary>
        /// <param name="buffer">Buffer to receive data.</param>
        /// <param name="offset">Offset in buffer to start writing.</param>
        /// <param name="count">Maximum bytes to read.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Number of bytes read, or 0 if connection closed.</returns>
        Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken token);

        /// <summary>
        /// Writes bytes to the transport.
        /// </summary>
        /// <param name="buffer">Buffer containing data to send.</param>
        /// <param name="offset">Offset in buffer to start reading.</param>
        /// <param name="count">Number of bytes to write.</param>
        /// <param name="token">Cancellation token.</param>
        Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken token);

        /// <summary>
        /// Flushes any buffered data to the transport.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        Task FlushAsync(CancellationToken token);

        /// <summary>
        /// Reads a single line of text (terminated by newline).
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The line read (without terminator), or null if connection closed.</returns>
        Task<string> ReadLineAsync(CancellationToken token);

        /// <summary>
        /// Writes a line of text followed by a newline.
        /// </summary>
        /// <param name="line">The text to write.</param>
        /// <param name="token">Cancellation token.</param>
        Task WriteLineAsync(string line, CancellationToken token);
    }
}
