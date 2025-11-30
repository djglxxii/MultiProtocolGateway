using System;
using System.Collections.Generic;

namespace Gateway.Core
{
    /// <summary>
    /// Registry of available vendor device packs.
    /// </summary>
    public sealed class VendorRegistry
    {
        private readonly List<IVendorDevicePack> _packs;

        /// <summary>
        /// Creates a registry with the specified vendor packs.
        /// </summary>
        /// <param name="packs">The vendor packs to register.</param>
        public VendorRegistry(IEnumerable<IVendorDevicePack> packs)
        {
            _packs = new List<IVendorDevicePack>(packs ?? throw new ArgumentNullException(nameof(packs)));
        }

        /// <summary>
        /// Gets all registered vendor packs.
        /// </summary>
        public IList<IVendorDevicePack> AllPacks
        {
            get { return _packs; }
        }
    }
}
