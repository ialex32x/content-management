using System;
using System.Collections.Generic;

namespace Iris.ContentManagement
{
    public sealed class ContentSystem
    {
        private static ContentSystem _system = new();
        private IContentManager _manager;

        public static ContentSystem Get() => _system;

        private ContentSystem()
        {
            Internal.Scheduler.Initialize();
            _manager = new Internal.EdSimulatedContentManager();
            // _manager = new Internal.DownloadableContentManager(_)
        }

        public void Shutdown()
        {
            _manager.Shutdown();
            Internal.Scheduler.Shutdown();
        }

        public AssetHandle GetAsset(string assetPath) => new AssetHandle(_manager.GetAsset(assetPath));
    }
}
