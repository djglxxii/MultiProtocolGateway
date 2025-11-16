using System;

namespace PoctGateway.Core.Handlers;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class PoctHandlerAttribute : Attribute
{
    public string? MessageType { get; }

    public PoctHandlerAttribute(string? messageType = null)
    {
        MessageType = messageType;
    }
}
