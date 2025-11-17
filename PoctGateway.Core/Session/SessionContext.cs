using System;
using System.Collections.Generic;
using System.Xml.Linq;

namespace PoctGateway.Core.Session;

public sealed class SessionContext
{
    public SessionContext(Guid sessionId, string remoteEndpoint, DateTimeOffset connectedAt)
    {
        SessionId = sessionId;
        RemoteEndpoint = remoteEndpoint;
        ConnectedAt = connectedAt;
    }

    public Guid SessionId { get; }
    public string RemoteEndpoint { get; }
    public DateTimeOffset ConnectedAt { get; }

    public string MessageType { get; set; } = string.Empty;
    public string CurrentRaw { get; set; } = string.Empty;
    public XDocument? CurrentXDocument { get; set; }

    public List<SessionMessage> MessageHistory { get; } = new();
    public IDictionary<string, object> Items { get; } = new Dictionary<string, object>();

    /// <summary>
    /// Optional error text for this inbound message. When non-empty,
    /// the engine will send an AE (error) acknowledgement and include
    /// this message in the ACK payload.
    /// </summary>
    public string? ErrorMessage { get; set; }
}