namespace Iris.ContentManagement
{
    //TODO PackageManager, LocalStorage, FileCache, Downloader, ContentLibrary 的层次结构
    //TODO ContentLibrary 的加载过程
    public sealed class ContentSystem
    {
        private static ContentSystem _system;
        private IContentManager _contentManager;
        private IScheduler _scheduler;
        private Internal.PackageManager _packageManager;
        private Cache.LocalStorage _storage;

        public static IScheduler Scheduler => _system._scheduler;

        internal static Internal.PackageManager PackageManager => _system._packageManager;

        private ContentSystem()
        {
            _scheduler = new Utility.DefaultScheduler();
            _contentManager = new Internal.EdSimulatedContentManager();

            _storage = new Cache.LocalStorage();
            var streamingAssets = Cache.StreamingAssetsFileCache.Create();
            var cache = new Cache.FileCacheCollection(streamingAssets, _storage);
            // var library = new Internal.ContentLibrary();
            // library.Import(_storage.OpenRead("contentlibrary.dat"));
            // var resolver = new 
            // var downloader = new Internal.Downloader(resolver);
            // _packageManager = new Internal.PackageManager(cache, _storage, null);
            // _contentManager = new Internal.DownloadableContentManager(library, _packageManager);
        }

        public static void Startup()
        {
            if (_system != null)
            {
                return;
            }
            _system = new ContentSystem();
        }

        public static void Shutdown()
        {
            if (_system == null)
            {
                return;
            }
            _system._contentManager.Shutdown();
            // _system._packageManager.Shutdown();
            _system._storage.Shutdown();
            _system._scheduler.Shutdown();
            _system = null;
        }

        public static AssetHandle GetAsset(string assetPath) => new AssetHandle(_system._contentManager.GetAsset(assetPath));
    }
}
