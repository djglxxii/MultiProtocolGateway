using System;
using System.Collections.Generic;
using System.Xml.Linq;

namespace PoctGateway.Core.Protocol.Poct1A.ObsR01;

public class ObsFacade
{
    protected const string ValueAttributeName = "V";

    protected readonly XDocument Document;
    protected readonly XElement Root;
    protected readonly XElement Hdr;
    protected readonly XElement Svc;
    protected readonly XElement Pt;
    protected readonly XElement Opr;
    protected readonly XElement Spc;
    protected readonly XElement Ord;
    protected readonly XElement Rgt;

    public ObsFacade(XDocument document)
    {
        if (document == null) throw new ArgumentNullException("document");

        Document = document;
        Root = document.Root ?? new XElement("OBS.R01");

        Hdr = Root.Element("HDR") ?? new XElement("HDR");
        Svc = Root.Element("SVC") ?? new XElement("SVC");

        Pt  = Svc.Element("PT")  ?? new XElement("PT");
        Opr = Svc.Element("OPR") ?? new XElement("OPR");
        Spc = Svc.Element("SPC") ?? new XElement("SPC");
        Ord = Svc.Element("ORD") ?? new XElement("ORD");
        Rgt = Svc.Element("RGT") ?? new XElement("RGT");
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

    // ----- SVC -----

    public virtual string RoleCode
    {
        get { return GetChildAttr(Svc, "SVC.role_cd"); }
    }

    public virtual string ObservationDateTime
    {
        get { return GetChildAttr(Svc, "SVC.observation_dttm"); }
    }

    public virtual string ReasonCode
    {
        get { return GetChildAttr(Svc, "SVC.reason_cd"); }
    }

    // ----- Patient (PT) -----

    public virtual string PatientId
    {
        get { return GetChildAttr(Pt, "PT.patient_id"); }
    }

    public virtual string PatientGivenName
    {
        get
        {
            var name = Pt.Element("PT.name");
            if (name == null) return string.Empty;
            return GetAttr(name.Element("GIV"), ValueAttributeName);
        }
    }

    public virtual string PatientFamilyName
    {
        get
        {
            var name = Pt.Element("PT.name");
            if (name == null) return string.Empty;
            return GetAttr(name.Element("FAM"), ValueAttributeName);
        }
    }

    // ----- Operator (OPR) -----

    public virtual string OperatorId
    {
        get { return GetChildAttr(Opr, "OPR.operator_id"); }
    }

    public virtual string OperatorName
    {
        get { return GetChildAttr(Opr, "OPR.name"); }
    }

    // ----- Specimen (SPC) -----

    public virtual string SpecimenDateTime
    {
        get { return GetChildAttr(Spc, "SPC.specimen_dttm"); }
    }

    public virtual string SpecimenId
    {
        get
        {
            var el = Spc.Element("SPC.specimen_id");
            return GetAttr(el, ValueAttributeName);
        }
    }

    public virtual string SpecimenIdSystem
    {
        get
        {
            var el = Spc.Element("SPC.specimen_id");
            return GetAttr(el, "SN");
        }
    }

    public virtual string SpecimenIdSystemVersion
    {
        get
        {
            var el = Spc.Element("SPC.specimen_id");
            return GetAttr(el, "SV");
        }
    }

    public virtual string SpecimenTypeCode
    {
        get
        {
            var el = Spc.Element("SPC.type_cd");
            return GetAttr(el, ValueAttributeName);
        }
    }

    public virtual string SpecimenTypeSystem
    {
        get
        {
            var el = Spc.Element("SPC.type_cd");
            return GetAttr(el, "SN");
        }
    }

    public virtual string SpecimenTypeSystemVersion
    {
        get
        {
            var el = Spc.Element("SPC.type_cd");
            return GetAttr(el, "SV");
        }
    }

    // ----- Order (ORD) -----

    public virtual string UniversalServiceId
    {
        get
        {
            var el = Ord.Element("ORD.universal_service_id");
            return GetAttr(el, ValueAttributeName);
        }
    }

    public virtual string UniversalServiceSystem
    {
        get
        {
            var el = Ord.Element("ORD.universal_service_id");
            return GetAttr(el, "SN");
        }
    }

    public virtual string UniversalServiceSystemVersion
    {
        get
        {
            var el = Ord.Element("ORD.universal_service_id");
            return GetAttr(el, "SV");
        }
    }

    // ----- Reagent (RGT) -----

    public virtual string ReagentName
    {
        get { return GetChildAttr(Rgt, "RGT.name"); }
    }

    public virtual string ReagentLotNumber
    {
        get { return GetChildAttr(Rgt, "RGT.lot_number"); }
    }

    public virtual string ReagentExpirationDate
    {
        get { return GetChildAttr(Rgt, "RGT.expiration_date"); }
    }

    // ----- Observations: PT/OBS (potentially multiple) -----

    public virtual IEnumerable<ObsResult> Observations
    {
        get { return GetObservationResults(); }
    }

    protected virtual IEnumerable<ObsResult> GetObservationResults()
    {
        var list = new List<ObsResult>();

        foreach (var obs in Pt.Elements("OBS"))
        {
            var idEl  = obs.Element("OBS.observation_id");
            var valEl = obs.Element("OBS.qualitative_value");
            var methodEl = obs.Element("OBS.method_cd");

            var item = new ObsResult
            {
                ObservationId            = GetAttr(idEl,  ValueAttributeName),
                ObservationIdSystem      = GetAttr(idEl,  "SN"),
                ObservationIdSystemVersion = GetAttr(idEl, "SV"),

                QualitativeValue         = GetAttr(valEl, ValueAttributeName),
                QualitativeSystem        = GetAttr(valEl, "SN"),
                QualitativeSystemVersion = GetAttr(valEl, "SV"),

                MethodCode               = GetAttr(methodEl, ValueAttributeName)
            };

            list.Add(item);
        }

        return list;
    }

    // ----- Notes: SVC/NTE (multiple) -----

    public virtual IEnumerable<ObsNote> Notes
    {
        get { return GetNotes(); }
    }

    protected virtual IEnumerable<ObsNote> GetNotes()
    {
        var list = new List<ObsNote>();

        foreach (var nte in Svc.Elements("NTE"))
        {
            var txtEl = nte.Element("NTE.text");
            var text = GetAttr(txtEl, ValueAttributeName);

            if (!string.IsNullOrEmpty(text))
            {
                list.Add(new ObsNote { Text = text });
            }
        }

        return list;
    }

    // ----- Map to POCO -----

    public virtual ObsMessage ToModel()
    {
        var model = new ObsMessage();

        model.Header.ControlId        = ControlId;
        model.Header.VersionId        = VersionId;
        model.Header.CreationDateTime = CreationDateTime;

        model.Service.RoleCode          = RoleCode;
        model.Service.ObservationDateTime = ObservationDateTime;
        model.Service.ReasonCode        = ReasonCode;

        model.Service.Patient.PatientId   = PatientId;
        model.Service.Patient.GivenName   = PatientGivenName;
        model.Service.Patient.FamilyName  = PatientFamilyName;

        model.Service.Operator.OperatorId = OperatorId;
        model.Service.Operator.Name       = OperatorName;

        model.Service.Specimen.SpecimenDateTime        = SpecimenDateTime;
        model.Service.Specimen.SpecimenId              = SpecimenId;
        model.Service.Specimen.SpecimenIdSystem        = SpecimenIdSystem;
        model.Service.Specimen.SpecimenIdSystemVersion = SpecimenIdSystemVersion;
        model.Service.Specimen.SpecimenTypeCode        = SpecimenTypeCode;
        model.Service.Specimen.SpecimenTypeSystem      = SpecimenTypeSystem;
        model.Service.Specimen.SpecimenTypeSystemVersion = SpecimenTypeSystemVersion;

        model.Service.Order.UniversalServiceId           = UniversalServiceId;
        model.Service.Order.UniversalServiceSystem       = UniversalServiceSystem;
        model.Service.Order.UniversalServiceSystemVersion = UniversalServiceSystemVersion;

        model.Service.Reagent.Name           = ReagentName;
        model.Service.Reagent.LotNumber      = ReagentLotNumber;
        model.Service.Reagent.ExpirationDate = ReagentExpirationDate;

        foreach (var obs in Observations)
        {
            model.Service.Observations.Add(obs);
        }

        foreach (var note in Notes)
        {
            model.Service.Notes.Add(note);
        }

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