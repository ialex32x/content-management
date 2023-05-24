using System;
using System.Collections.Generic;

namespace Iris.ContentManagement.Internal
{
    using Iris.ContentManagement.Utility;

    //TODO ?? 改为通用 Package 包装, assetbundle/zip 的区分已由底层 PackageManager 处理
    public sealed class AssetBundlePackage : IPackage, IPackageRequestHandler
    {
        private readonly struct AssetRequest
        {
            public readonly string assetName;
            public readonly IUnityAssetRequestHandler handler;

            public AssetRequest(string assetName, IUnityAssetRequestHandler handler)
            {
                this.assetName = assetName;
                this.handler = handler;
            }
        }

        private PackageManager.PackageHandle _packageHandle;
        private EPackageState _state;
        private List<AssetBundlePackage> _dependencies = new();
        private SList<IPackageRequestHandler> _packageRequestHandlers = new();
        private SList<AssetRequest> _assetRequests = new();
        private Dictionary<string, WeakReference<UnityAsset>> _cachedAssets = new();

        public AssetBundlePackage(in PackageManager.PackageHandle handle)
        {
            _packageHandle = handle;
            _state = EPackageState.Created;
        }

        ~AssetBundlePackage()
        {
            _packageHandle.Unload();
        }

        IAsset IPackage.GetAsset(string assetPath)
        {
            if (_cachedAssets.TryGetValue(assetPath, out var weakReference) && weakReference.TryGetTarget(out var asset))
            {
                return asset;
            }

            asset = new UnityAsset(this, assetPath);
            _cachedAssets[assetPath] = new(asset);
            return asset;
        }

        internal UnityEngine.Object LoadAssetSync(string assetName)
        {
            LoadSync();
            Utility.Assert.Debug(_state == EPackageState.Loaded);
            return _packageHandle.LoadAsset(assetName);
        }

        internal void CancelAssetRequest(in SIndex index)
        {
            _assetRequests.RemoveAt(index);
        }

        internal void RequestAssetAsync(ref SIndex index, string assetName, IUnityAssetRequestHandler handler)
        {
            _assetRequests.RemoveAt(index);
            if (_state == EPackageState.Loaded)
            {
                InvokeLoadAssetAsync(assetName, handler);
            }
            else
            {
                // postpone until assetbundle loaded
                index = _assetRequests.Add(new(assetName, handler));
                LoadAsync(out var none, null);
            }
        }

        internal void LoadSync()
        {
            if (_state == EPackageState.Loaded)
            {
                return;
            }

            foreach (var dependency in _dependencies)
            {
                dependency.LoadSync();
            }

            _packageHandle.LoadSync();
            _state = EPackageState.Loaded;
            OnLoaded();
        }

        internal void LoadAsync(out SIndex index, IPackageRequestHandler handler)
        {
            switch (_state)
            {
                case EPackageState.Loading:
                    {
                        index = handler != null ? _packageRequestHandlers.Add(handler) : SIndex.None;
                        return;
                    }
                case EPackageState.Loaded:
                    {
                        index = SIndex.None;
                        handler?.OnRequestCompleted(_packageHandle);
                        return;
                    }
                case EPackageState.Created:
                    {
                        foreach (var dependency in _dependencies)
                        {
                            dependency.LoadAsync(out var none, this);
                        }

                        index = handler != null ? _packageRequestHandlers.Add(handler) : SIndex.None;
                        _state = EPackageState.Loading;
                        _packageHandle.LoadAsync(this);
                        CheckDependenciesState();
                        return;
                    }
                default:
                    {
                        Utility.Assert.Never("should not happen");
                        index = SIndex.None;
                        return;
                    }
            }

        }

        internal void AddDependency(AssetBundlePackage package)
        {
            Utility.Assert.Debug(_state == EPackageState.Created, "call AddDependency only on init");
            Utility.Assert.Debug(!_dependencies.Contains(package) && package != this);
            _dependencies.Add(package);
        }

        void IPackageRequestHandler.OnRequestCompleted(in PackageManager.PackageHandle handle)
        {
            if (handle == _packageHandle)
            {
                Utility.Logger.Debug("{0} loaded {1}", nameof(AssetBundlePackage), _packageHandle.name);
            }
            CheckDependenciesState();
        }

        private void InvokeLoadAssetAsync(string assetName, IUnityAssetRequestHandler handler)
        {
            var request = _packageHandle.LoadAssetAsync(assetName);
            if (request == null)
            {
                handler.OnRequestCompleted(null);
                return;
            }

            request.completed += op => handler.OnRequestCompleted(((UnityEngine.AssetBundleRequest)op).asset);
        }

        private void CheckDependenciesState()
        {
            if (!_packageHandle.isCompleted)
            {
                return;
            }

            foreach (var dependency in _dependencies)
            {
                if (!dependency._packageHandle.isCompleted)
                {
                    return;
                }
            }

            if (_state == EPackageState.Loading)
            {
                _state = EPackageState.Loaded;
                OnLoaded();
            }
        }

        private void OnLoaded()
        {
            Utility.Logger.Debug("{0} fully loaded {1}", nameof(AssetBundlePackage), _packageHandle.name);
            while (_packageRequestHandlers.TryRemoveAt(0, out var handler))
            {
                handler.OnRequestCompleted(_packageHandle);
            }
            while (_assetRequests.TryRemoveAt(0, out var handler))
            {
                InvokeLoadAssetAsync(handler.assetName, handler.handler);
            }
        }
    }
}