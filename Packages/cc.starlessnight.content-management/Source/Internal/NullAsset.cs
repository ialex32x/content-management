namespace Iris.ContentManagement.Internal
{
    using Iris.ContentManagement.Utility;
    using UnityEngine;

    public sealed class NullAsset : IAsset
    {
        public static readonly NullAsset Default = new();

        public EAssetState state => EAssetState.Invalid;

        public Object Get() => null;

        public IPackage GetPackage() => null;

        public void RequestSyncLoad() { }

        public void RequestAsyncLoad(ref SIndex index, IAssetRequestHandler handler) => handler.OnRequestCompleted();

        public void CancelRequest(ref SIndex index) { }
    }
}
