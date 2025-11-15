using System;
using System.Collections.Generic;
using PoctGateway.Core.Handlers;
using PoctGateway.Core.Session;

namespace PoctGateway.Core.Vendors;

public interface IVendorDevicePack
{
    string VendorKey { get; }
    string ProtocolKind { get; }

    bool IsMatch(RawInitialPacket packet);

    IReadOnlyCollection<Type> GetHandlerTypes();
}
