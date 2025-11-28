namespace PoctGateway.Core.Engine;

/// <summary>
/// Interface for handlers that want to be notified when their outbound messages
/// are acknowledged (ACK) or rejected (NAK) by the device.
/// </summary>
public interface IOutboundAckListener
{
    /// <summary>
    /// Called when the device acknowledges the outbound message (AA response).
    /// </summary>
    /// <param name="controlId">The control ID of the acknowledged message.</param>
    void OnOutboundAcknowledged(int controlId);

    /// <summary>
    /// Called when the device rejects the outbound message (AE response).
    /// </summary>
    /// <param name="controlId">The control ID of the rejected message.</param>
    /// <param name="errorMessage">The error message from the device, if any.</param>
    void OnOutboundError(int controlId, string? errorMessage);
}
