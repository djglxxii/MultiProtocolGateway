using System.Xml.Linq;

namespace PoctGateway.Core.Protocol.Poct1A.EotR01;

public class EotFacade
{
    protected const string ValueAttributeName = "V";

    protected readonly XDocument Document;
    protected readonly XElement Root;
    protected readonly XElement Hdr;
    protected readonly XElement Eot;

    public EotFacade(XDocument document)
    {
        if (document == null) throw new ArgumentNullException("document");

        Document = document;
        Root = document.Root ?? new XElement("EOT.R01");

        Hdr = Root.Element("HDR") ?? new XElement("HDR");
        Eot = Root.Element("EOT") ?? new XElement("EOT");
    }

    // Header

    public virtual string ControlId
    {
        get { return GetChildAttr(Hdr, "HDR.control_id"); }
    }

    public virtual string VersionId
    {
        get { return GetChildAttr(Hdr, "HDR.version_id"); }
    }

    public virtual string CreationDateTime
    {
        get { return GetChildAttr(Hdr, "HDR.creation_dttm"); }
    }

    // EOT

    /// <summary>
    /// Logical topic code for the message the EOT refers to.
    /// Default: EOT.topic_cd/V
    /// </summary>
    public virtual string TopicCode
    {
        get { return GetChildAttr(Eot, "EOT.topic_cd"); }
    }

    // Map to POCO

    public virtual EotMessage ToModel()
    {
        var model = new EotMessage();

        model.Header.ControlId        = ControlId;
        model.Header.VersionId        = VersionId;
        model.Header.CreationDateTime = CreationDateTime;

        model.Eot.TopicCode = TopicCode;

        return model;
    }

    // Helpers

    protected string GetChildAttr(XElement parent, string childElementName)
    {
        if (parent == null) return string.Empty;

        var child = parent.Element(childElementName);
        if (child == null) return string.Empty;

        return GetAttr(child, ValueAttributeName);
    }

    protected string GetAttr(XElement element, string attributeName)
    {
        if (element == null) return string.Empty;

        var attr = element.Attribute(attributeName);
        return attr != null ? attr.Value : string.Empty;
    }
}