using System;
using System.Collections.Generic;

namespace Iris.ContentManagement.Internal
{
    using Iris.ContentManagement.Utility;

    internal readonly struct ManagedAssetRequest
    {
        public readonly string assetName;
        public readonly WeakReference<IManagedAssetRequestHandler> handler;

        public ManagedAssetRequest(string assetName, IManagedAssetRequestHandler handler)
        {
            this.assetName = assetName;
            this.handler = new(handler);
        }
    }
}
