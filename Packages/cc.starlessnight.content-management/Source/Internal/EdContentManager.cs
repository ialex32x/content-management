using System;
using System.IO;
using System.Collections.Generic;

namespace Iris.ContentManagement.Internal
{
    //TODO 编辑器模式下也使用 ContentBuilder 产生 ContentDB 提供与最终运行时相同的流程
    // 编辑器模式下的资源管理器 (没有 ContentDB 查询支持)
    public sealed class EdContentManager : IContentManager
    {
        private IFileSystem _rawFileSystem;
        private Dictionary<string, WeakReference<IAsset>> _cachedAssets = new();

        public EdContentManager()
        {
            _rawFileSystem = new OSFileSystem();
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
                asset = new Internal.EdUnityAsset(assetPath);
                _cachedAssets[assetPath] = new(asset);
                return asset;
            }

            return NullAsset.Default;
        }
    }
}
