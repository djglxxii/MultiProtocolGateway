using System;
using System.Collections.Generic;
using System.Linq;
using PoctGateway.Core.Session;

namespace PoctGateway.Core.Vendors;

public sealed class VendorRegistry
{
    public IReadOnlyList<IVendorDevicePack> Vendors { get; }

    public VendorRegistry(IEnumerable<IVendorDevicePack> vendors)
    {
        Vendors = vendors.ToList().AsReadOnly();
    }

    public IVendorDevicePack? DetectVendor(RawInitialPacket packet, Action<string> log)
    {
        var matches = Vendors.Where(v => v.IsMatch(packet)).ToList();

        if (matches.Count == 0)
        {
            log("No vendor matched initial packet.");
            return null;
        }

        if (matches.Count > 1)
        {
            var keys = string.Join(", ", matches.Select(m => m.VendorKey));
            log($"Multiple vendors matched initial packet: {keys}");
            return null;
        }

        log($"Session bound to vendor '{matches[0].VendorKey}' ({matches[0].ProtocolKind}).");
        return matches[0];
    }
}
