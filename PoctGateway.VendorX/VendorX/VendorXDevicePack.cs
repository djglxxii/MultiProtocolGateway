using System;
using System.Collections.Generic;
using System.Xml.Linq;
using PoctGateway.Core.Vendors;
using PoctGateway.Core.Session;
using PoctGateway.VendorX.Handlers;

namespace PoctGateway.VendorX;

public sealed class VendorXDevicePack : IVendorDevicePack
{
    public string VendorKey => "VendorX";
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
        return new[]
        {
            typeof(HEL_Handler),
            typeof(OBS_Handler),
            typeof(OPL_Handler),
            typeof(PTQRY_Handler)
        };
    }
}
