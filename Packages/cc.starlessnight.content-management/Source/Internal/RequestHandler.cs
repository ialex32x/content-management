namespace Iris.ContentManagement.Internal
{
    internal interface IPackageAssetRequestHandler
    {
        void OnRequestCompleted(object target);
    }

    public interface IAssetRequestHandler
    {
        void OnRequestCompleted();
    }

    internal interface IPackageRequestHandler
    {
        void OnPackageLoaded(in PackageManager.PackageHandle handle);

        // unsafe 
        void OnAssetLoaded(in Utility.SIndex index, object target); 
    }
}
