namespace Gateway.Core
{
    /// <summary>
    /// Indicates the type of processing being performed in the handler pipeline.
    /// </summary>
    public enum ProcessingMode
    {
        /// <summary>
        /// Processing an inbound message from the device.
        /// </summary>
        InboundMessage,

        /// <summary>
        /// Processing a server-initiated command to the device.
        /// </summary>
        ServerCommand
    }
}
