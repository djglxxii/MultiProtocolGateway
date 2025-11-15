namespace PoctGateway.Core.Session;

public enum MessageDirection
{
    DeviceToServer,
    ServerToDevice
}

public sealed class SessionMessage
{
    public string MessageType { get; init; } = string.Empty;
    public MessageDirection Direction { get; init; }
    public string RawPayload { get; init; } = string.Empty;
}
