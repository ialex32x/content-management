namespace Iris.ContentManagement.Internal
{
    internal interface IUnityAssetRequestHandler
    {
        void OnRequestCompleted(UnityEngine.Object asset);
    }

    public interface IAssetRequestHandler
    {
        void OnRequestCompleted();
    }

    internal interface IPackageRequestHandler
    {
        void OnPackageLoaded(in PackageManager.PackageHandle handle);

        // unsafe 
        void OnAssetLoaded(in Utility.SIndex index, UnityEngine.Object target); 
    }
}
