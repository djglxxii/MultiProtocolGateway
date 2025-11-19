using System.Xml.Linq;

namespace PoctGateway.Core.Protocol.Poct1A.DST;

public class DstFacade
{
    protected const string ValueAttributeName = "V";

    protected readonly XDocument Document;
    protected readonly XElement Root;
    protected readonly XElement Hdr;
    protected readonly XElement Dst;

    public DstFacade(XDocument document)
    {
        if (document == null) throw new ArgumentNullException("document");

        Document = document;
        Root = document.Root ?? new XElement("DST.R01");

        Hdr = Root.Element("HDR") ?? new XElement("HDR");
        Dst = Root.Element("DST") ?? new XElement("DST");
    }

    // ----- Header -----

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

    // ----- DST segment -----

    public virtual string StatusDateTime
    {
        get { return GetChildAttr(Dst, "DST.status_dttm"); }
    }

    public virtual string NewObservationsQuantity
    {
        get { return GetChildAttr(Dst, "DST.new_observations_qty"); }
    }

    public virtual string ConditionCode
    {
        get
        {
            var el = Dst.Element("DST.condition_cd");
            return GetAttr(el, ValueAttributeName);
        }
    }

    public virtual string ConditionCodeSystem
    {
        get
        {
            var el = Dst.Element("DST.condition_cd");
            return GetAttr(el, "SN");
        }
    }

    public virtual string ConditionCodeSystemVersion
    {
        get
        {
            var el = Dst.Element("DST.condition_cd");
            return GetAttr(el, "SV");
        }
    }

    public virtual string ObservationsUpdateDateTime
    {
        get { return GetChildAttr(Dst, "DST.observations_update_dttm"); }
    }

    public virtual string OperatorsUpdateDateTime
    {
        get { return GetChildAttr(Dst, "DST.operators_update_dttm"); }
    }

    // ----- Map to POCO -----

    public virtual DstR01Message ToModel()
    {
        var model = new DstR01Message();

        model.Header.ControlId        = ControlId;
        model.Header.VersionId        = VersionId;
        model.Header.CreationDateTime = CreationDateTime;

        model.Dst.StatusDateTime              = StatusDateTime;
        model.Dst.NewObservationsQuantity     = NewObservationsQuantity;
        model.Dst.ConditionCode               = ConditionCode;
        model.Dst.ConditionCodeSystem         = ConditionCodeSystem;
        model.Dst.ConditionCodeSystemVersion  = ConditionCodeSystemVersion;
        model.Dst.ObservationsUpdateDateTime  = ObservationsUpdateDateTime;
        model.Dst.OperatorsUpdateDateTime     = OperatorsUpdateDateTime;

        return model;
    }

    // ----- helpers -----

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