namespace Gateway.Core
{
    /// <summary>
    /// Represents the lifecycle state of a device session.
    /// </summary>
    public enum SessionLifecycleState
    {
        /// <summary>
        /// Initial state while establishing protocol and identity.
        /// </summary>
        Negotiating,

        /// <summary>
        /// Normal active communication state.
        /// </summary>
        Active,

        /// <summary>
        /// Continuous mode hold (device streaming data).
        /// </summary>
        ContinuousHold,

        /// <summary>
        /// Session is in the process of shutting down.
        /// </summary>
        Terminating,

        /// <summary>
        /// Session has ended.
        /// </summary>
        Closed
    }
}
