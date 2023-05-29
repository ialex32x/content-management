namespace Iris.ContentManagement.Internal
{
    // 通用请求回调
    public interface IRequestHandler
    {
        void OnRequestCompleted();
    }

    internal interface IManagedAssetRequestHandler
    {
        void OnRequestCompleted(object target);
    }

    internal interface IManagedPackageRequestHandler
    {
        void OnPackageLoaded(in PackageManager.PackageHandle handle);

        // unsafe 
        void OnAssetLoaded(in Utility.SIndex index, object target); 
    }
}
