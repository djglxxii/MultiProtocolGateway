using System;

namespace PoctGateway.Core.Protocol.Poct1A.EotR01;
// --------------------- POCOs ---------------------

public sealed class EotR01Message
{
    public EotHeader Header { get; set; }
    public EotSegment Eot { get; set; }

    public EotR01Message()
    {
        Header = new EotHeader();
        Eot = new EotSegment();
    }
}

public sealed class EotHeader
{
    public string ControlId { get; set; }
    public string VersionId { get; set; }
    public string CreationDateTime { get; set; }
}

public sealed class EotSegment
{
    /// <summary>
    /// Logical topic code for the end-of-transaction (e.g. OBS, OP_LST, etc.).
    /// </summary>
    public string TopicCode { get; set; }
}

// --------------------- Facade ---------------------