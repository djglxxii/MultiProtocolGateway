using System.Xml.Linq;
using PoctGateway.Core.Session;
using PoctGateway.Core.Vendors;

namespace PoctGateway.Vendor.Cepheid.GeneXpert;

public sealed class GeneXpertDevicePack : IVendorDevicePack
{
    public string VendorKey => "Cepheid_GeneXpert";
    public string ProtocolKind => "POCT1A";
    public bool IsMatch(RawInitialPacket packet)
    {
        try
        {
            var text = packet.RawText.Trim();
            if (!text.StartsWith("<", StringComparison.Ordinal))
            {
                return false;
            }

            var xdoc = XDocument.Parse(text);
            if (!string.Equals(xdoc.Root?.Name.LocalName, "HEL.R01", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    public IReadOnlyCollection<Type> GetHandlerTypes()
    {
        throw new NotImplementedException();
    }
}
