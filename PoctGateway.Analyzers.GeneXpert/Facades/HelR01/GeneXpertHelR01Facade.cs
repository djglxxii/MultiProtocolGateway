using System.Xml.Linq;
using PoctGateway.Core.Protocol.Poct1A.HelR01;

namespace PoctGateway.Analyzers.GeneXpert.Facades.HelR01;

public sealed class GeneXpertHelR01Facade : HelR01Facade
{
    public GeneXpertHelR01Facade(XDocument document)
        : base(document)
    {
    }

    public override string DeviceIdentifier
    {
        get
        {
            // Prefer analyzer-specific element if present, otherwise fall back
            var analyzerId = GetAttrValue(Dev, "DEV.device_id");
            if (!string.IsNullOrEmpty(analyzerId))
            {
                return analyzerId;
            }

            return base.DeviceIdentifier;
        }
    }

    // override other properties if this vendor puts them elsewhere
    // public override string TopicsSupportedCode => ...
}
