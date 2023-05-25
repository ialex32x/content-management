using System;
using System.Collections.Generic;

namespace Iris.ContentManagement
{
    public sealed class ContentSystem
    {
        private static ContentSystem _system;
        private IContentManager _manager;
        private Internal.Scheduler _scheduler;

        public static Internal.Scheduler Scheduler => _system._scheduler;

        private ContentSystem()
        {
            _scheduler = new Internal.Scheduler();
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
