using System;
using System.Collections.Generic;

namespace Iris.ContentManagement.Internal
{
    using Iris.ContentManagement.Utility;

    internal interface IPackage
    {
        object LoadAssetSync(string assetName);
        void CancelAssetRequest(in SIndex index);
        void RequestAssetAsync(ref SIndex index, string assetName, IManagedAssetRequestHandler handler);
    }
}
