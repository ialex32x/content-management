namespace Iris.ContentManagement
{
    public sealed class ContentSystem
    {
        private static ContentSystem _system;
        private IContentManager _manager;
        private IScheduler _scheduler;

        public static IScheduler Scheduler => _system._scheduler;

        private ContentSystem()
        {
            _scheduler = new Utility.DefaultScheduler();
            _manager = new Internal.EdSimulatedContentManager();
            // _manager = new Internal.DownloadableContentManager(_)
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
            _system._manager.Shutdown();
            _system._scheduler.Shutdown();
            _system = null;
        }

        public static AssetHandle GetAsset(string assetPath) => new AssetHandle(_system._manager.GetAsset(assetPath));
    }
}
