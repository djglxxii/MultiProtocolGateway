using System;
using System.Threading.Tasks;
using Gateway.Core;

namespace Gateway.Vendors.Sample.Handlers
{
    /// <summary>
    /// Handler that logs inbound messages to the console.
    /// </summary>
    public sealed class ConsoleLoggingHandler : HandlerBase
    {
        public override Task HandleAsync(SessionContext context, Func<Task> next)
        {
            if (context.Mode == ProcessingMode.InboundMessage)
            {
                string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                string deviceId = context.DeviceId ?? "unknown";
                string vendorName = context.VendorName ?? "unknown";

                Console.WriteLine("[{0}] [{1}] [{2}] Received: {3}",
                    timestamp,
                    vendorName,
                    deviceId,
                    context.CurrentMessage);
            }

            return next();
        }
    }
}
