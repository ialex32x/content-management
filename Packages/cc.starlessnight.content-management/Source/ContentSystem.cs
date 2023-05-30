namespace Iris.ContentManagement
{
    //TODO PackageManager, LocalStorage, FileCache, Downloader, ContentLibrary 的层次结构
    //TODO ContentLibrary 的加载过程
    public sealed class ContentSystem
    {
        private static ContentSystem _system;
        private IContentManager _contentManager;
        private IScheduler _scheduler;
        private Cache.LocalStorage _storage;
        private Net.IDownloader _downloader;

        public static IScheduler Scheduler => _system._scheduler;

        public static Net.IDownloader Downloader => _system._downloader;

        private ContentSystem(in StartupOptions options)
        {
            //TODO content library 的更新逻辑
            var library = new Utility.ContentLibrary();

            _scheduler = new Utility.DefaultScheduler();

            var cache = new Cache.FileCacheCollection();
            _storage = cache.Add<Cache.LocalStorage>();
            if (options.useArtifacts)
            {
                cache.Add<Cache.ArtifactsFileCache>().Bind(options.artifactsPath);
            }
            // 启用 StreamingAssets
            if (options.useStreamingAssets)
            {
                cache.Add<Cache.StreamingAssetsFileCache>();
            }
            if (options.useDownloader)
            {
                _downloader = new Net.SimpleDownloader(options.uriResolver);
            }
            var packageManager = new Internal.PackageManager(cache, _storage, _downloader);
            _contentManager = new Internal.DownloadableContentManager(library, packageManager);
        }

        public static void Startup(in StartupOptions options)
        {
            if (_system != null)
            {
                return;
            }
            _system = new ContentSystem(options);
        }

        public static void Shutdown()
        {
            if (_system == null)
            {
                return;
            }
            _system._contentManager.Shutdown();
            _system._storage.Shutdown();
            _system._scheduler.Shutdown();
            _system = null;
        }

        public static AssetHandle GetAsset(string assetPath) => new AssetHandle(_system._contentManager.GetAsset(assetPath));
    }
}
