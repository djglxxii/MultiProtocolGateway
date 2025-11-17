namespace PoctGateway.Core.Protocol.Poct1A.HelR01;

public sealed class HelR01Message
{
    public HelHeader Header { get; set; }
    public HelDevice Device { get; set; }

    public HelR01Message()
    {
        Header = new HelHeader();
        Device = new HelDevice();
    }
}

public sealed class HelHeader
{
    public string ControlId { get; set; }
    public string VersionId { get; set; }
    public string CreationDateTime { get; set; }
}

public sealed class HelDevice
{
    public string DeviceIdentifier { get; set; }          // logical name
    public string VendorId { get; set; }
    public string ModelId { get; set; }
    public string SerialId { get; set; }
    public string ManufacturerName { get; set; }
    public string SoftwareVersion { get; set; }
    public string DeviceName { get; set; }

    public HelDeviceDcpSettings Dcp { get; set; }
    public HelDeviceDscSettings Dsc { get; set; }

    public HelDevice()
    {
        Dcp = new HelDeviceDcpSettings();
        Dsc = new HelDeviceDscSettings();
    }
}

public sealed class HelDeviceDcpSettings
{
    public string ApplicationTimeout { get; set; }
}

public sealed class HelDeviceDscSettings
{
    public string ConnectionProfileCode { get; set; }
    public string TopicsSupportedCode { get; set; }
    public string DirectivesSupportedCode { get; set; }
    public string MaxMessageSize { get; set; }
}
