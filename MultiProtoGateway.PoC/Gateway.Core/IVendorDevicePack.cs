using System;

namespace Gateway.Core
{
    /// <summary>
    /// Represents a vendor-specific device pack that handles detection and session creation.
    /// </summary>
    public interface IVendorDevicePack
    {
        /// <summary>
        /// Human-readable name of the vendor/device pack.
        /// </summary>
        string VendorName { get; }

        /// <summary>
        /// The protocol family this pack handles.
        /// </summary>
        ProtocolKind ProtocolKind { get; }

        /// <summary>
        /// Attempts to detect if the initial payload matches this vendor's protocol.
        /// </summary>
        /// <param name="initialPayload">The first bytes received from the connection.</param>
        /// <returns>Detection result indicating match status and confidence.</returns>
        DetectionResult Detect(ReadOnlySpan<byte> initialPayload);

        /// <summary>
        /// Creates a session engine for handling communication with this vendor's devices.
        /// </summary>
        /// <param name="context">The session context for the new session.</param>
        /// <returns>A session engine instance.</returns>
        ISessionEngine CreateSession(SessionContext context);
    }
}
