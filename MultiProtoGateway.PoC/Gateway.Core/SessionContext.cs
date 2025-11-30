using System;
using System.Collections.Generic;

namespace Gateway.Core
{
    /// <summary>
    /// Holds per-connection state for a device session.
    /// </summary>
    public sealed class SessionContext
    {
        /// <summary>
        /// Device identifier (assigned during negotiation or from message content).
        /// </summary>
        public string DeviceId { get; set; }

        /// <summary>
        /// Name of the vendor pack handling this session.
        /// </summary>
        public string VendorName { get; set; }

        /// <summary>
        /// The protocol family detected for this session.
        /// </summary>
        public ProtocolKind ProtocolKind { get; set; }

        /// <summary>
        /// Current lifecycle state of the session.
        /// </summary>
        public SessionLifecycleState LifecycleState { get; set; }

        /// <summary>
        /// Current processing mode (inbound message or server command).
        /// </summary>
        public ProcessingMode Mode { get; set; }

        /// <summary>
        /// The current message being processed (for PoC, simple string lines).
        /// </summary>
        public string CurrentMessage { get; set; }

        /// <summary>
        /// Placeholder for server-initiated commands.
        /// </summary>
        public SessionCommand CurrentCommand { get; set; }

        /// <summary>
        /// General-purpose property bag for per-session data.
        /// </summary>
        public Dictionary<string, object> Bag { get; private set; }

        /// <summary>
        /// History of recent messages (bounded).
        /// </summary>
        public List<string> MessageHistory { get; private set; }

        /// <summary>
        /// Indicates if the session is in continuous data mode.
        /// </summary>
        public bool IsContinuousMode { get; set; }

        /// <summary>
        /// Reference to the transport for handlers that need to send responses.
        /// </summary>
        public ITransport Transport { get; set; }

        /// <summary>
        /// Creates a new session context in the Negotiating state.
        /// </summary>
        public SessionContext()
        {
            LifecycleState = SessionLifecycleState.Negotiating;
            Bag = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            MessageHistory = new List<string>();
        }
    }
}
