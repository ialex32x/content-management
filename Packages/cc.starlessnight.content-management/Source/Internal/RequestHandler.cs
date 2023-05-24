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
        void OnRequestCompleted(in PackageManager.PackageHandle handle);
    }
}
