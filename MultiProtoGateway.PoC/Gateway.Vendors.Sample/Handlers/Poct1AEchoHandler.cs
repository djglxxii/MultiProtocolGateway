using System;
using System.Threading;
using System.Threading.Tasks;
using Gateway.Core;

namespace Gateway.Vendors.Sample.Handlers
{
    /// <summary>
    /// Handler that responds to POCT1A-style XML messages with a canned ACK.
    /// </summary>
    public sealed class Poct1AEchoHandler : HandlerBase
    {
        private const string AckResponse =
            "<ACK.R01><HDR><HDR.message_type V=\"ACK.R01\"/></HDR><ACK><ACK.type_cd V=\"AA\"/></ACK></ACK.R01>";

        public override async Task HandleAsync(SessionContext context, Func<Task> next)
        {
            if (context.Mode == ProcessingMode.InboundMessage && context.Transport != null)
            {
                try
                {
                    await context.Transport.WriteLineAsync(AckResponse, CancellationToken.None).ConfigureAwait(false);

                    string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                    Console.WriteLine("[{0}] [POCT1A] Sent ACK response", timestamp);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[ERROR] Failed to send POCT1A ACK: {0}", ex.Message);
                }
            }

            await next().ConfigureAwait(false);
        }
    }
}
