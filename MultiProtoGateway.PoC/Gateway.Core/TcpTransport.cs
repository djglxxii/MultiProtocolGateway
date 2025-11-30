using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Gateway.Core
{
    /// <summary>
    /// ITransport implementation wrapping a TCP NetworkStream.
    /// </summary>
    public sealed class TcpTransport : ITransport
    {
        private readonly TcpClient _client;
        private readonly NetworkStream _stream;
        private readonly StreamReader _reader;
        private readonly StreamWriter _writer;
        private bool _disposed;

        /// <summary>
        /// Creates a new TcpTransport wrapping the specified TcpClient.
        /// </summary>
        /// <param name="client">The connected TcpClient.</param>
        public TcpTransport(TcpClient client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _stream = client.GetStream();
            _reader = new StreamReader(_stream, Encoding.UTF8, false, 1024, true);
            // Use UTF8 without BOM for cleaner protocol output
            _writer = new StreamWriter(_stream, new UTF8Encoding(false), 1024, true);
            _writer.AutoFlush = false;
        }

        /// <summary>
        /// Gets the remote endpoint address as a string.
        /// </summary>
        public string RemoteEndPoint
        {
            get
            {
                try
                {
                    return _client.Client.RemoteEndPoint?.ToString() ?? "unknown";
                }
                catch
                {
                    return "unknown";
                }
            }
        }

        public async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken token)
        {
            ThrowIfDisposed();
            return await _stream.ReadAsync(buffer, offset, count, token).ConfigureAwait(false);
        }

        public async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken token)
        {
            ThrowIfDisposed();
            await _stream.WriteAsync(buffer, offset, count, token).ConfigureAwait(false);
        }

        public async Task FlushAsync(CancellationToken token)
        {
            ThrowIfDisposed();
            await _stream.FlushAsync(token).ConfigureAwait(false);
        }

        public async Task<string> ReadLineAsync(CancellationToken token)
        {
            ThrowIfDisposed();

            // Use a TaskCompletionSource to handle cancellation with ReadLineAsync
            if (token.IsCancellationRequested)
            {
                token.ThrowIfCancellationRequested();
            }

            try
            {
                // Note: StreamReader.ReadLineAsync doesn't support CancellationToken in older APIs
                // For PoC, we accept this limitation. Production code should use a custom reader.
                string line = await _reader.ReadLineAsync().ConfigureAwait(false);
                return line;
            }
            catch (IOException)
            {
                // Connection closed or error
                return null;
            }
            catch (ObjectDisposedException)
            {
                return null;
            }
        }

        public async Task WriteLineAsync(string line, CancellationToken token)
        {
            ThrowIfDisposed();

            if (token.IsCancellationRequested)
            {
                token.ThrowIfCancellationRequested();
            }

            await _writer.WriteLineAsync(line).ConfigureAwait(false);
            await _writer.FlushAsync().ConfigureAwait(false);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            try
            {
                _reader.Dispose();
            }
            catch
            {
                // Ignore disposal errors
            }

            try
            {
                _writer.Dispose();
            }
            catch
            {
                // Ignore disposal errors
            }

            try
            {
                _stream.Dispose();
            }
            catch
            {
                // Ignore disposal errors
            }

            try
            {
                _client.Dispose();
            }
            catch
            {
                // Ignore disposal errors
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(TcpTransport));
            }
        }
    }
}
