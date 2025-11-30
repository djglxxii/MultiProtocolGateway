namespace Gateway.Core
{
    /// <summary>
    /// Represents the outcome of a vendor detection attempt on initial connection data.
    /// </summary>
    public sealed class DetectionResult
    {
        /// <summary>
        /// Indicates whether the vendor pack recognizes this payload.
        /// </summary>
        public bool IsMatch { get; private set; }

        /// <summary>
        /// Confidence score (0-100). Higher values indicate stronger match certainty.
        /// </summary>
        public int Confidence { get; private set; }

        /// <summary>
        /// Optional vendor-specific data to bootstrap the session.
        /// </summary>
        public object BootstrapInfo { get; private set; }

        private DetectionResult(bool isMatch, int confidence, object bootstrapInfo)
        {
            IsMatch = isMatch;
            Confidence = confidence;
            BootstrapInfo = bootstrapInfo;
        }

        /// <summary>
        /// Creates a result indicating no match was found.
        /// </summary>
        public static DetectionResult NoMatch()
        {
            return new DetectionResult(false, 0, null);
        }

        /// <summary>
        /// Creates a result indicating a successful match.
        /// </summary>
        /// <param name="confidence">Confidence score (0-100).</param>
        /// <param name="bootstrapInfo">Optional vendor-specific bootstrap data.</param>
        public static DetectionResult Match(int confidence, object bootstrapInfo)
        {
            return new DetectionResult(true, confidence, bootstrapInfo);
        }
    }
}
