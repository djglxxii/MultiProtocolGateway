using System.Xml.Linq;

namespace PoctGateway.Core.Protocol.Poct1A.HelR01;

public sealed class GenericHelR01FacadeFactory : IHelR01FacadeFactory
{
    public HelR01Facade Create(XDocument doc)
    {
        return new HelR01Facade(doc);
    }
}