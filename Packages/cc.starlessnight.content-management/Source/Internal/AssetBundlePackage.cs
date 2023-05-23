using System;
using System.Collections.Generic;

namespace Iris.ContentManagement.Internal
{
    using Iris.ContentManagement.Utility;

    public sealed class AssetBundlePackage : IPackage, IAssetBundleRequestHandler, IPackageRequestHandler
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

        private AssetBundleManager.AssetBundleHandle _assetBundle;
        private bool _isSelfLoaded;
        private EPackageState _state;
        private List<AssetBundlePackage> _dependencies = new();
        private SList<IPackageRequestHandler> _packageRequestHandlers = new();
        private SList<AssetRequest> _assetRequests = new();
        private Dictionary<string, WeakReference<UnityAsset>> _cachedAssets = new();

        public AssetBundlePackage(in AssetBundleManager.AssetBundleHandle assetBundle)
        {
            _assetBundle = assetBundle;
            _state = EPackageState.Created;
            _isSelfLoaded = false;
        }

        ~AssetBundlePackage()
        {
            _assetBundle.Unload();
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

        public UnityEngine.Object LoadAssetSync(string assetName)
        {
            LoadSync();
            Utility.Assert.Debug(_state == EPackageState.Loaded);
            return _assetBundle.LoadAsset(assetName);
        }

        public void CancelAssetRequest(in SIndex index)
        {
            _assetRequests.RemoveAt(index);
        }

        public void RequestAssetAsync(ref SIndex index, string assetName, IUnityAssetRequestHandler handler)
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

        public void LoadSync()
        {
            if (_state == EPackageState.Loaded)
            {
                return;
            }

            foreach (var dependency in _dependencies)
            {
                dependency.LoadSync();
            }

            _state = EPackageState.Loaded;
            _isSelfLoaded = true;
            _assetBundle.LoadSync();
            CheckDependenciesState();
        }

        public void LoadAsync(out SIndex index, IPackageRequestHandler handler)
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
                        handler?.OnRequestCompleted();
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
                        _assetBundle.LoadAsync(this);
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

        public void AddDependency(AssetBundlePackage package)
        {
            Utility.Assert.Debug(_state == EPackageState.Created, "call AddDependency only on init");
            Utility.Assert.Debug(!_dependencies.Contains(package) && package != this);
            _dependencies.Add(package);
        }

        void IPackageRequestHandler.OnRequestCompleted()
        {
            CheckDependenciesState();
        }

        void IAssetBundleRequestHandler.OnAssetBundleLoaded()
        {
            _isSelfLoaded = true;
            Utility.Logger.Debug("{0} loaded {1}", nameof(AssetBundlePackage), _assetBundle.name);
            CheckDependenciesState();
        }

        private void InvokeLoadAssetAsync(string assetName, IUnityAssetRequestHandler handler)
        {
            var request = _assetBundle.LoadAssetAsync(assetName);
            if (request == null)
            {
                handler.OnRequestCompleted(null);
                return;
            }

            request.completed += op => handler.OnRequestCompleted(((UnityEngine.AssetBundleRequest)op).asset);
        }

        private void CheckDependenciesState()
        {
            if (!_isSelfLoaded)
            {
                return;
            }

            foreach (var dependency in _dependencies)
            {
                if (!dependency._isSelfLoaded)
                {
                    return;
                }
            }

            if (_state == EPackageState.Loading)
            {
                _state = EPackageState.Loaded;
                Utility.Logger.Debug("{0} fully loaded {1}", nameof(AssetBundlePackage), _assetBundle.name);

                while (_packageRequestHandlers.TryRemoveAt(0, out var handler))
                {
                    handler.OnRequestCompleted();
                }

                while (_assetRequests.TryRemoveAt(0, out var handler))
                {
                    InvokeLoadAssetAsync(handler.assetName, handler.handler);
                }
            }
        }
    }
}