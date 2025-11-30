using System;
using System.Collections.Generic;
using System.Text;
using Gateway.Core;
using Gateway.Vendors.Sample.Handlers;

namespace Gateway.Vendors.Sample
{
    /// <summary>
    /// Sample vendor pack for POCT1A-style XML protocol.
    /// Detects messages containing XML-like content.
    /// </summary>
    public sealed class Poct1ASampleVendorPack : IVendorDevicePack
    {
        public string VendorName
        {
            get { return "POCT1A-Sample"; }
        }

        public ProtocolKind ProtocolKind
        {
            get { return ProtocolKind.Poct1A; }
        }

        /// <summary>
        /// Detects POCT1A-style messages by looking for XML markers.
        /// </summary>
        public DetectionResult Detect(ReadOnlySpan<byte> initialPayload)
        {
            if (initialPayload.Length == 0)
            {
                return DetectionResult.NoMatch();
            }

            // Convert to string for simple pattern matching
            string text;
            try
            {
                text = Encoding.UTF8.GetString(initialPayload.ToArray());
            }
            catch
            {
                return DetectionResult.NoMatch();
            }

            // Look for specific POCT1A message types (highest confidence)
            if (text.Contains("<HEL.R01>") || text.Contains("<DST.R01>"))
            {
                return DetectionResult.Match(100, null);
            }

            // Look for general XML-like structure
            if (text.Contains("<") && text.Contains(">"))
            {
                // Check for what looks like POCT1A format (element with .R or .Q suffix)
                if (text.Contains(".R0") || text.Contains(".Q0"))
                {
                    return DetectionResult.Match(90, null);
                }

                // Generic XML - lower confidence
                return DetectionResult.Match(50, null);
            }

            return DetectionResult.NoMatch();
        }

        /// <summary>
        /// Creates a session engine with POCT1A-specific handlers.
        /// </summary>
        public ISessionEngine CreateSession(SessionContext context)
        {
            context.VendorName = VendorName;
            context.ProtocolKind = ProtocolKind;

            List<HandlerBase> handlers = new List<HandlerBase>
            {
                new ConsoleLoggingHandler(),
                new Poct1AEchoHandler()
            };

            return new TextSessionEngine(context, handlers);
        }
    }
}
