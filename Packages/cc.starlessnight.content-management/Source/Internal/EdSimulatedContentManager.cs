using System;
using System.Collections.Generic;

namespace Iris.ContentManagement.Internal
{
    //TODO 编辑器模式下也使用 ContentBuilder 产生 ContentDB 提供与最终运行时相同的流程
    // 编辑器模式下的资源管理器 (没有 ContentDB 查询支持)
    public sealed class EdSimulatedContentManager : IContentManager
    {
        private IFileSystem _rawFileSystem;
        private Dictionary<string, WeakReference<IAsset>> _cachedAssets = new();

        public EdSimulatedContentManager()
        {
            _rawFileSystem = new Utility.OSFileSystem();
            _rawFileSystem.Load();
        }

        public void Shutdown()
        {
            _rawFileSystem.Unload();
        }

        public IAsset GetAsset(string assetPath)
        {
            if (_cachedAssets.TryGetValue(assetPath, out var weakReference) && weakReference.TryGetTarget(out var asset))
            {
                return asset;
            }

            if (_rawFileSystem.Exists(assetPath))
            {
                asset = new Internal.EdSimulatedAsset(assetPath);
                _cachedAssets[assetPath] = new(asset);
                return asset;
            }

            return EdSimulatedAsset.Null;
        }
    }
}
