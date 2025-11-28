namespace PoctGateway.Core.Engine;

/// <summary>
/// Represents an outbound message queued for delivery.
/// </summary>
/// <param name="Payload">The processed message payload (with tokens replaced and HDR injected if needed).</param>
/// <param name="ControlId">The control ID assigned to this message.</param>
/// <param name="AckListener">Optional listener to be notified when ACK/NAK is received.</param>
public sealed record OutboundMessage(
    string Payload,
    int ControlId,
    IOutboundAckListener? AckListener);
