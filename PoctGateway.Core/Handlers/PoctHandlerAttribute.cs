using System;

namespace PoctGateway.Core.Handlers;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class PoctHandlerAttribute : Attribute
{
    public int Order { get; }
    public string? MessageType { get; }

    public PoctHandlerAttribute(int order, string? messageType = null)
    {
        Order = order;
        MessageType = messageType;
    }
}
