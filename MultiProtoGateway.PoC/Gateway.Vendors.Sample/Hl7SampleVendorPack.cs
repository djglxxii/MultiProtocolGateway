using System;
using System.Collections.Generic;
using System.Text;
using Gateway.Core;
using Gateway.Vendors.Sample.Handlers;

namespace Gateway.Vendors.Sample
{
    /// <summary>
    /// Sample vendor pack for HL7 v2.x protocol.
    /// Detects messages starting with MSH|.
    /// </summary>
    public sealed class Hl7SampleVendorPack : IVendorDevicePack
    {
        public string VendorName
        {
            get { return "HL7-Sample"; }
        }

        public ProtocolKind ProtocolKind
        {
            get { return ProtocolKind.Hl7; }
        }

        /// <summary>
        /// Detects HL7 messages by looking for MSH| header.
        /// </summary>
        public DetectionResult Detect(ReadOnlySpan<byte> initialPayload)
        {
            if (initialPayload.Length < 4)
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

            // HL7 messages start with MSH segment
            if (text.StartsWith("MSH|", StringComparison.Ordinal))
            {
                return DetectionResult.Match(100, null);
            }

            // Also check for MLLP framing (VT character = 0x0B)
            if (initialPayload.Length > 0 && initialPayload[0] == 0x0B)
            {
                string trimmed = text.TrimStart('\x0B');
                if (trimmed.StartsWith("MSH|", StringComparison.Ordinal))
                {
                    return DetectionResult.Match(100, "MLLP");
                }
            }

            return DetectionResult.NoMatch();
        }

        /// <summary>
        /// Creates a session engine with HL7-specific handlers.
        /// </summary>
        public ISessionEngine CreateSession(SessionContext context)
        {
            context.VendorName = VendorName;
            context.ProtocolKind = ProtocolKind;

            List<HandlerBase> handlers = new List<HandlerBase>
            {
                new ConsoleLoggingHandler(),
                new Hl7EchoHandler()
            };

            return new TextSessionEngine(context, handlers);
        }
    }
}
