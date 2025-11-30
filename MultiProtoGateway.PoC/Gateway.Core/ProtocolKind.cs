namespace Gateway.Core
{
    /// <summary>
    /// Identifies the protocol family used by a device.
    /// </summary>
    public enum ProtocolKind
    {
        /// <summary>
        /// POCT1-A (XML-based point-of-care testing protocol).
        /// </summary>
        Poct1A,

        /// <summary>
        /// HL7 v2.x (pipe-delimited healthcare messaging).
        /// </summary>
        Hl7,

        /// <summary>
        /// ASTM E1381/E1394 (laboratory instrument protocol).
        /// </summary>
        Astm,

        /// <summary>
        /// Vendor-specific binary protocol.
        /// </summary>
        CustomBinary
    }
}
