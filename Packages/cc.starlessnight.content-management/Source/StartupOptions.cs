namespace Iris.ContentManagement
{
    public struct StartupOptions
    {
        public bool useStreamingAssets;
        public bool useDownloader;
        public Utility.IUriResolver uriResolver;
    }
}