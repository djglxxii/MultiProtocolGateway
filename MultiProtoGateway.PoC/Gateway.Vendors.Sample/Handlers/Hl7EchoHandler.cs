using System;
using System.Threading;
using System.Threading.Tasks;
using Gateway.Core;

namespace Gateway.Vendors.Sample.Handlers
{
    /// <summary>
    /// Handler that responds to HL7-style messages with a canned ACK.
    /// </summary>
    public sealed class Hl7EchoHandler : HandlerBase
    {
        // Simple HL7 ACK - using \r as segment terminator but sending as single line for PoC
        private const string AckResponse =
            "MSH|^~\\&|SERVER|GW||DEVICE||ACK|1|P|2.3\rMSA|AA|1";

        public override async Task HandleAsync(SessionContext context, Func<Task> next)
        {
            if (context.Mode == ProcessingMode.InboundMessage && context.Transport != null)
            {
                try
                {
                    await context.Transport.WriteLineAsync(AckResponse, CancellationToken.None).ConfigureAwait(false);

                    string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                    Console.WriteLine("[{0}] [HL7] Sent ACK response", timestamp);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[ERROR] Failed to send HL7 ACK: {0}", ex.Message);
                }
            }

            await next().ConfigureAwait(false);
        }
    }
}
