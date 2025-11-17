using System.Xml.Linq;
using PoctGateway.Core.Protocol.Poct1A.HelR01;

namespace PoctGateway.Analyzers.GeneXpert.Facades.HelR01;

public sealed class GeneXpertHelR01FacadeFactory : IHelR01FacadeFactory
{
    public HelR01Facade Create(XDocument doc)
    {
        return new GeneXpertHelR01Facade(doc);
    }
}
