using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Gateway.Core;
using Gateway.Vendors.Sample;

namespace Gateway.Host
{
    internal class Program
    {
        private const int DefaultPort = 5000;

        private static async Task Main(string[] args)
        {
            int port = DefaultPort;

            // Simple argument parsing for port
            if (args.Length > 0)
            {
                if (!int.TryParse(args[0], out port) || port < 1 || port > 65535)
                {
                    Console.WriteLine("Invalid port number. Using default: {0}", DefaultPort);
                    port = DefaultPort;
                }
            }

            // Create vendor registry with sample packs
            VendorRegistry registry = CreateVendorRegistry();

            Console.WriteLine("========================================");
            Console.WriteLine("  Multi-Protocol Gateway PoC");
            Console.WriteLine("========================================");
            Console.WriteLine("Registered vendor packs:");
            foreach (IVendorDevicePack pack in registry.AllPacks)
            {
                Console.WriteLine("  - {0} ({1})", pack.VendorName, pack.ProtocolKind);
            }
            Console.WriteLine();
            Console.WriteLine("Starting TCP listener on port {0}...", port);
            Console.WriteLine("Press Ctrl+C to stop.");
            Console.WriteLine();

            // Set up cancellation for graceful shutdown
            CancellationTokenSource cts = new CancellationTokenSource();
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                Console.WriteLine("\nShutdown requested...");
                cts.Cancel();
            };

            try
            {
                await RunListenerAsync(port, registry, cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Listener stopped.");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Fatal error: {0}", ex.Message);
            }
        }

        private static VendorRegistry CreateVendorRegistry()
        {
            List<IVendorDevicePack> packs = new List<IVendorDevicePack>
            {
                new Poct1ASampleVendorPack(),
                new Hl7SampleVendorPack()
            };

            return new VendorRegistry(packs);
        }

        private static async Task RunListenerAsync(int port, VendorRegistry registry, CancellationToken cancellationToken)
        {
            TcpListener listener = new TcpListener(IPAddress.Any, port);
            listener.Start();

            Console.WriteLine("Listening on 0.0.0.0:{0}", port);
            Console.WriteLine();

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    TcpClient client;
                    try
                    {
                        client = await listener.AcceptTcpClientAsync().ConfigureAwait(false);
                    }
                    catch (ObjectDisposedException)
                    {
                        break;
                    }
                    catch (SocketException)
                    {
                        break;
                    }

                    // Handle each connection in a separate task (fire and forget for PoC)
                    Task connectionTask = HandleConnectionAsync(client, registry, cancellationToken);

                    // Don't await - let it run independently
                    // In production, track these tasks properly
                    _ = connectionTask.ContinueWith(t =>
                    {
                        if (t.IsFaulted && t.Exception != null)
                        {
                            Console.WriteLine("[ERROR] Unhandled connection error: {0}",
                                t.Exception.InnerException?.Message ?? t.Exception.Message);
                        }
                    }, TaskContinuationOptions.OnlyOnFaulted);
                }
            }
            finally
            {
                listener.Stop();
            }
        }

        private static async Task HandleConnectionAsync(TcpClient client, VendorRegistry registry, CancellationToken cancellationToken)
        {
            string remoteEndPoint = "unknown";
            TcpTransport transport = null;

            try
            {
                transport = new TcpTransport(client);
                remoteEndPoint = transport.RemoteEndPoint;

                Console.WriteLine("[CONNECT] Connection accepted from {0}", remoteEndPoint);

                // Read first line for detection
                string firstLine = await transport.ReadLineAsync(cancellationToken).ConfigureAwait(false);

                if (string.IsNullOrEmpty(firstLine))
                {
                    Console.WriteLine("[DISCONNECT] {0} - No data received, closing", remoteEndPoint);
                    return;
                }

                Console.WriteLine("[DETECT] First line from {0}: {1}",
                    remoteEndPoint,
                    TruncateForLog(firstLine, 80));

                // Convert to bytes for detection
                byte[] firstLineBytes = Encoding.UTF8.GetBytes(firstLine);

                // Run detection across all vendor packs
                List<DetectionMatch> matches = new List<DetectionMatch>();
                foreach (IVendorDevicePack pack in registry.AllPacks)
                {
                    DetectionResult result = pack.Detect(new ReadOnlySpan<byte>(firstLineBytes));
                    if (result.IsMatch)
                    {
                        matches.Add(new DetectionMatch(pack, result));
                    }
                }

                // Evaluate matches
                if (matches.Count == 0)
                {
                    Console.WriteLine("[REJECT] {0} - No vendor matched the payload, closing", remoteEndPoint);
                    return;
                }

                if (matches.Count > 1)
                {
                    // Sort by confidence descending
                    matches.Sort((a, b) => b.Result.Confidence.CompareTo(a.Result.Confidence));

                    // If top two have same confidence, it's ambiguous
                    if (matches[0].Result.Confidence == matches[1].Result.Confidence)
                    {
                        Console.WriteLine("[REJECT] {0} - Ambiguous detection ({1} vendors matched with same confidence), closing",
                            remoteEndPoint, matches.Count);
                        return;
                    }

                    Console.WriteLine("[DETECT] Multiple matches, using highest confidence: {0} ({1}%)",
                        matches[0].Pack.VendorName, matches[0].Result.Confidence);
                }

                // Use the best match
                DetectionMatch chosen = matches[0];
                Console.WriteLine("[VENDOR] {0} - Selected vendor: {1} (Protocol: {2}, Confidence: {3}%)",
                    remoteEndPoint,
                    chosen.Pack.VendorName,
                    chosen.Pack.ProtocolKind,
                    chosen.Result.Confidence);

                // Create session context
                SessionContext context = new SessionContext();
                context.DeviceId = remoteEndPoint; // Use endpoint as device ID for PoC

                // Create session engine
                ISessionEngine engine = chosen.Pack.CreateSession(context);

                // Process the first message (that was used for detection)
                TextSessionEngine textEngine = engine as TextSessionEngine;
                if (textEngine != null)
                {
                    await textEngine.ProcessFirstMessageAsync(firstLine, transport, cancellationToken).ConfigureAwait(false);
                }

                // Run the session engine for subsequent messages
                await engine.RunAsync(transport, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("[DISCONNECT] {0} - Cancelled", remoteEndPoint);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[ERROR] {0} - {1}", remoteEndPoint, ex.Message);
            }
            finally
            {
                Console.WriteLine("[DISCONNECT] {0} - Connection closed", remoteEndPoint);

                if (transport != null)
                {
                    transport.Dispose();
                }
                else
                {
                    client.Dispose();
                }
            }
        }

        private static string TruncateForLog(string text, int maxLength)
        {
            if (text == null)
            {
                return "(null)";
            }

            if (text.Length <= maxLength)
            {
                return text;
            }

            return text.Substring(0, maxLength) + "...";
        }

        /// <summary>
        /// Helper class to hold detection match results.
        /// </summary>
        private sealed class DetectionMatch
        {
            public IVendorDevicePack Pack { get; private set; }
            public DetectionResult Result { get; private set; }

            public DetectionMatch(IVendorDevicePack pack, DetectionResult result)
            {
                Pack = pack;
                Result = result;
            }
        }
    }
}
