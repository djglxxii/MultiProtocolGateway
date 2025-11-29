using System;
using System.Collections.Generic;
using System.Xml.Linq;
using PoctGateway.Analyzers.GeneXpert.Handlers;
using PoctGateway.Core.Handlers;
using PoctGateway.Core.Session;
using PoctGateway.Core.Vendors;

namespace PoctGateway.Analyzers.GeneXpert;

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
        return
        [
            typeof(HEL_Handler),
            typeof(PTQRY_Handler),
            typeof(OBS_Handler),
            typeof(OPL_Handler),
            typeof(PTL_Handler),
            typeof(EOT_Handler)
        ];
    }
}
