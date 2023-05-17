namespace Iris.ContentManagement.Internal
{
    public interface IAssetBundleRequestHandler
    {
        void OnAssetBundleLoaded();
    }
    
    public interface IUnityAssetRequestHandler
    {
        void OnRequestCompleted(UnityEngine.Object asset);
    }

    public interface IAssetRequestHandler
    {
        void OnRequestCompleted();
    }

    public interface IPackageRequestHandler
    {
        void OnRequestCompleted();
    }
}
