namespace Iris.ContentManagement
{
    public struct StartupOptions
    {
        #region Editor Only
        public bool useArtifacts;
        public string artifactsPath;
        #endregion

        public bool useStreamingAssets;
        public bool useDownloader;
        public Utility.IUriResolver uriResolver;
    }
}