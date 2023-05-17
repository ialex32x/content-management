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
            _manager = new Internal.EdContentManager();
            // _manager = new Internal.DownloadableContentManager(_)
        }

        public void Shutdown()
        {
            _manager.Shutdown();
        }

        public AssetHandle GetAsset(string assetPath) => new AssetHandle(_manager.GetAsset(assetPath));
    }
}
