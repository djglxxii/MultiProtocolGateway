namespace PoctGateway.Core.Protocol.Poct1A.DST;

public sealed class DstR01Message
{
    public DstHeader Header { get; set; }
    public DstSegment Dst { get; set; }

    public DstR01Message()
    {
        Header = new DstHeader();
        Dst = new DstSegment();
    }
}

public sealed class DstHeader
{
    public string ControlId { get; set; }
    public string VersionId { get; set; }
    public string CreationDateTime { get; set; }
}

public sealed class DstSegment
{
    public string StatusDateTime { get; set; }

    /// <summary>
    /// Number of new observations since last status.
    /// </summary>
    public string NewObservationsQuantity { get; set; }

    /// <summary>
    /// Device condition code (e.g. "R" = Ready).
    /// </summary>
    public string ConditionCode { get; set; }
    public string ConditionCodeSystem { get; set; }
    public string ConditionCodeSystemVersion { get; set; }

    public string ObservationsUpdateDateTime { get; set; }
    public string OperatorsUpdateDateTime { get; set; }
}