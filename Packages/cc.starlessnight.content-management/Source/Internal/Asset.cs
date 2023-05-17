namespace Iris.ContentManagement.Internal
{
    using Iris.ContentManagement.Utility;
    
    public interface IAsset
    {
        EAssetState state { get; }

        UnityEngine.Object Get();

        //TODO 是否在 Asset 上提供文件读接口 (混合资源和文件)
        // Stream OpenRead();

        void RequestSyncLoad();

        void RequestAsyncLoad(ref SIndex index, IAssetRequestHandler handler);

        void CancelRequest(ref SIndex index);
    }
}
