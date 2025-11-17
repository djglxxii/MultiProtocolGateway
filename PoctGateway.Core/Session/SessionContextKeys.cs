namespace PoctGateway.Core.Session;

internal static class SessionContextKeys
{
    internal static class Ack
    {
        // "AA" or "AE", etc.
        public const string Type = "Poct.AckType";

        // bool flag: true = no ACK/NAK should be sent
        public const string Suppress = "Poct.SuppressAck";
    }
}
