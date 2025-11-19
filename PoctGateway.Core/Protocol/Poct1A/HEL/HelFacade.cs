using System.Xml.Linq;

namespace PoctGateway.Core.Protocol.Poct1A.HelR01;

public class HelFacade
{
    protected const string ValueAttributeName = "V";

    protected readonly XDocument Document;
    protected readonly XElement Root;
    protected readonly XElement Hdr;
    protected readonly XElement Dev;
    protected readonly XElement Dcp;
    protected readonly XElement Dsc;

    public HelFacade(XDocument document)
    {
        if (document == null) throw new ArgumentNullException("document");

        Document = document;
        Root = document.Root ?? new XElement("HEL.R01");

        Hdr = Root.Element("HDR") ?? new XElement("HDR");
        Dev = Root.Element("DEV") ?? new XElement("DEV");
        Dcp = Dev.Element("DCP") ?? new XElement("DCP");
        Dsc = Dev.Element("DSC") ?? new XElement("DSC");
    }

    // Header (virtual in case someone really needs to override)

    public virtual string ControlId
    {
        get { return GetAttrValue(Hdr, "HDR.control_id"); }
    }

    public virtual string VersionId
    {
        get { return GetAttrValue(Hdr, "HDR.version_id"); }
    }

    public virtual string CreationDateTime
    {
        get { return GetAttrValue(Hdr, "HDR.creation_dttm"); }
    }

    // Device – logical properties

    /// <summary>
    /// Logical device identifier (MAC, analyzer id, etc.).
    /// Default: DEV.device_id/V
    /// </summary>
    public virtual string DeviceIdentifier
    {
        get { return GetAttrValue(Dev, "DEV.device_id"); }
    }

    public virtual string VendorId
    {
        get { return GetAttrValue(Dev, "DEV.vendor_id"); }
    }

    public virtual string ModelId
    {
        get { return GetAttrValue(Dev, "DEV.model_id"); }
    }

    public virtual string SerialId
    {
        get { return GetAttrValue(Dev, "DEV.serial_id"); }
    }

    public virtual string ManufacturerName
    {
        get { return GetAttrValue(Dev, "DEV.manufacturer_name"); }
    }

    public virtual string SoftwareVersion
    {
        get { return GetAttrValue(Dev, "DEV.sw_version"); }
    }

    public virtual string DeviceName
    {
        get { return GetAttrValue(Dev, "DEV.device_name"); }
    }

    // DCP

    public virtual string ApplicationTimeout
    {
        get { return GetAttrValue(Dcp, "DCP.application_timeout"); }
    }

    // DSC

    public virtual string ConnectionProfileCode
    {
        get { return GetAttrValue(Dsc, "DSC.connection_profile_cd"); }
    }

    public virtual string TopicsSupportedCode
    {
        get { return GetAttrValue(Dsc, "DSC.topics_supported_cd"); }
    }

    public virtual string DirectivesSupportedCode
    {
        get { return GetAttrValue(Dsc, "DSC.directives_supported_cd"); }
    }

    public virtual string MaxMessageSize
    {
        get { return GetAttrValue(Dsc, "DSC.max_message_sz"); }
    }

    // Facade -> POCO

    public HelMessage ToModel()
    {
        var model = new HelMessage();

        model.Header.ControlId        = ControlId;
        model.Header.VersionId        = VersionId;
        model.Header.CreationDateTime = CreationDateTime;

        model.Device.DeviceIdentifier         = DeviceIdentifier;
        model.Device.VendorId                 = VendorId;
        model.Device.ModelId                  = ModelId;
        model.Device.SerialId                 = SerialId;
        model.Device.ManufacturerName         = ManufacturerName;
        model.Device.SoftwareVersion          = SoftwareVersion;
        model.Device.DeviceName               = DeviceName;
        model.Device.Dcp.ApplicationTimeout   = ApplicationTimeout;
        model.Device.Dsc.ConnectionProfileCode   = ConnectionProfileCode;
        model.Device.Dsc.TopicsSupportedCode     = TopicsSupportedCode;
        model.Device.Dsc.DirectivesSupportedCode = DirectivesSupportedCode;
        model.Device.Dsc.MaxMessageSize          = MaxMessageSize;

        return model;
    }

    // ----- helpers -----

    protected string GetAttrValue(XElement parent, string childElementName)
    {
        if (parent == null) return string.Empty;

        var child = parent.Element(childElementName);
        if (child == null) return string.Empty;

        var attr = child.Attribute(ValueAttributeName);
        return attr != null ? attr.Value : string.Empty;
    }
}
