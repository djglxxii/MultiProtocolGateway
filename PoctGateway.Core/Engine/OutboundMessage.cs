namespace PoctGateway.Core.Engine;

/// <summary>
/// Represents an outbound message queued for delivery.
/// </summary>
public sealed class OutboundMessage
{
    public string Payload { get; }
    public int ControlId { get; }
    public IOutboundAckListener? AckListener { get; }
    public bool ExpectsAck { get; }

    public OutboundMessage(string payload, int controlId, IOutboundAckListener? ackListener, bool expectsAck)
    {
        Payload = payload ?? throw new ArgumentNullException(nameof(payload));
        ControlId = controlId;
        AckListener = ackListener;
        ExpectsAck = expectsAck;
    }
}
