namespace PoctGateway.Core.Protocol.Poct1A.ObsR01;

public sealed class ObsMessage
{
    public ObsHeader Header { get; set; }
    public ObsService Service { get; set; }

    public ObsMessage()
    {
        Header = new ObsHeader();
        Service = new ObsService();
    }
}

public sealed class ObsHeader
{
    public string ControlId { get; set; }
    public string VersionId { get; set; }
    public string CreationDateTime { get; set; }
}

public sealed class ObsService
{
    public string RoleCode { get; set; }
    public string ObservationDateTime { get; set; }
    public string ReasonCode { get; set; }

    public ObsPatient Patient { get; set; }
    public ObsOperator Operator { get; set; }
    public ObsSpecimen Specimen { get; set; }
    public ObsOrder Order { get; set; }
    public ObsReagent Reagent { get; set; }

    public IList<ObsResult> Observations { get; set; }
    public IList<ObsNote> Notes { get; set; }

    public ObsService()
    {
        Patient = new ObsPatient();
        Operator = new ObsOperator();
        Specimen = new ObsSpecimen();
        Order = new ObsOrder();
        Reagent = new ObsReagent();
        Observations = new List<ObsResult>();
        Notes = new List<ObsNote>();
    }
}

public sealed class ObsPatient
{
    public string PatientId { get; set; }
    public string GivenName { get; set; }
    public string FamilyName { get; set; }
}

public sealed class ObsOperator
{
    public string OperatorId { get; set; }
    public string Name { get; set; }
}

public sealed class ObsSpecimen
{
    public string SpecimenDateTime { get; set; }
    public string SpecimenId { get; set; }
    public string SpecimenIdSystem { get; set; }
    public string SpecimenIdSystemVersion { get; set; }
    public string SpecimenTypeCode { get; set; }
    public string SpecimenTypeSystem { get; set; }
    public string SpecimenTypeSystemVersion { get; set; }
}

public sealed class ObsOrder
{
    public string UniversalServiceId { get; set; }
    public string UniversalServiceSystem { get; set; }
    public string UniversalServiceSystemVersion { get; set; }
}

public sealed class ObsReagent
{
    public string Name { get; set; }
    public string LotNumber { get; set; }
    public string ExpirationDate { get; set; }
}

public sealed class ObsResult
{
    public string ObservationId { get; set; }
    public string ObservationIdSystem { get; set; }
    public string ObservationIdSystemVersion { get; set; }

    public string QualitativeValue { get; set; }
    public string QualitativeSystem { get; set; }
    public string QualitativeSystemVersion { get; set; }

    public string MethodCode { get; set; }
}

public sealed class ObsNote
{
    public string Text { get; set; }
}