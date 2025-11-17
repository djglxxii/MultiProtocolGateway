using System.Xml.Linq;

namespace PoctGateway.Core.Protocol.Poct1A.HelR01;

public interface IHelR01FacadeFactory
{
    HelR01Facade Create(XDocument doc);
}
