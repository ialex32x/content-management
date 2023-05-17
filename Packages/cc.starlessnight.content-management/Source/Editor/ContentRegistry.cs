using System;
using System.Collections.Generic;

namespace Iris.ContentManagement.Editor
{
    using UnityEditor;
    using Iris.ContentManagement.Utility;

    /* 
    methods:
        + load-state
        + save-state, report
    phases:
        + collect (scan)
        + arrange (plan): properly put asset into packages (by default) 
        + analysis
        + build-packages: generate all packages
        + post-process (encrypt)
        + generate-manifest: write manifest (optionally encripted)
        + distribute: upload to OSS if provided
     */
    // 打包管理
    public class ContentRegistry
    {
        public struct AssetBuildHandle
        {
            public readonly SIndex index;
            public AssetBuildHandle(SIndex index) { this.index = index; }
        }

        public struct PackageBuildHandle
        {
            public readonly SIndex index;
            public PackageBuildHandle(SIndex index) { this.index = index; }
        }

        public struct CollectionBuildHandle
        {
            public readonly SIndex index;
            public CollectionBuildHandle(SIndex index) { this.index = index; }
        }

        private SList<AssetBuildState> _assets = new();
        private SList<PackageBuildState> _packages = new();
        private SList<CollectionBuildState> _collections = new();

        // asset path => asset index
        private Dictionary<string, SIndex> _assetPathCache = new();

        public void LoadState() { }
        public void SaveState() { }

        public void Collect() { }
        public void Arrange() { }
        public void BuildPackages() { }
        public void GenerateManifest() { }

        public bool TryGetAssetState(AssetBuildHandle handle, out AssetBuildState asset)
        {
            return _assets.TryGetValue(handle.index, out asset);
        }

        public bool TryGetPackageState(PackageBuildHandle handle, out PackageBuildState package, out CollectionBuildState collection)
        {
            if (_packages.TryGetValue(handle.index, out package) && _collections.TryGetValue(package.collection, out collection))
            {
                return true;
            }
            collection = default;
            return false;
        }

        private AssetBuildHandle AddAsset(string assetPath)
        {
            if (!_assetPathCache.TryGetValue(assetPath, out var index))
            {
                var state = new AssetBuildState();
                state.assetRef = assetPath;
                index = _assetPathCache[assetPath] = _assets.Add(state);
            }
            return new AssetBuildHandle(index);
        }

        private string GenerateGuid()
        {
            return Guid.NewGuid().ToString("N");
        }

        private CollectionBuildHandle GetCollection()
        {
            var e = _collections.GetStableIndexEnumerator();
            if (e.MoveNext())
            {
                return new(e.Current);
            }
            var state = new CollectionBuildState();
            state.name = GenerateGuid();
            return new(_collections.Add(state));
        }

        private PackageBuildHandle GetPackage()
        {
            var e = _packages.GetStableIndexEnumerator();
            if (e.MoveNext())
            {
                return new(e.Current);
            }
            var state = new PackageBuildState();
            state.collection = GetCollection().index;
            state.name = GenerateGuid();
            return new(_packages.Add(state));
        }

        private void SetAssetPackageAuto(AssetBuildHandle assetHandle)
        {
            if (!_assets.IsValidIndex(assetHandle.index))
            {
                return;
            }
            ref var state = ref _assets.UnsafeGetValueByRef(assetHandle.index);
            state.package = GetPackage().index;
        }
    }
}