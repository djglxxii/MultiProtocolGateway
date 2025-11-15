using System;

namespace PoctGateway.Core.Session;

public sealed class RawInitialPacket
{
    public RawInitialPacket(ReadOnlyMemory<byte> rawBytes, string rawText)
    {
        RawBytes = rawBytes;
        RawText = rawText;
    }

    public ReadOnlyMemory<byte> RawBytes { get; }
    public string RawText { get; }
}
