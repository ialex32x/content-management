using System;
using System.Collections.Generic;

namespace Iris.ContentManagement.Internal
{
    using Iris.ContentManagement.Utility;

    internal sealed class UPackage : IPackageRequestHandler
    {
        private enum EPackageState
        {
            Created,
            Loading,
            Loaded,
        }

        public readonly static UPackage Null = new(default);

        private readonly struct AssetRequest
        {
            public readonly string assetName;
            public readonly WeakReference<IPackageAssetRequestHandler> handler;

            public AssetRequest(string assetName, IPackageAssetRequestHandler handler)
            {
                this.assetName = assetName;
                this.handler = new(handler);
            }
        }

        private PackageManager.PackageHandle _packageHandle;
        private EPackageState _state;
        private List<UPackage> _dependencies = new();
        private SList<IPackageRequestHandler> _packageRequestHandlers = new();
        private SList<AssetRequest> _assetRequests = new();

        public UPackage(in PackageManager.PackageHandle handle)
        {
            _packageHandle = handle;
            _packageHandle.Bind(this);
            _state = EPackageState.Created;
        }

        ~UPackage()
        {
            _packageHandle.Unload();
        }

        internal object LoadAssetSync(string assetName)
        {
            LoadSync();
            Utility.SAssert.Debug(_state == EPackageState.Loaded);
            return _packageHandle.LoadAsset(assetName);
        }

        internal void CancelAssetRequest(in SIndex index)
        {
            _assetRequests.RemoveAt(index);
        }

        internal void RequestAssetAsync(ref SIndex index, string assetName, IPackageAssetRequestHandler handler)
        {
            if (_assetRequests.IsValidIndex(index))
            {
                _assetRequests.UnsafeSetValue(index, new(assetName, handler));
            }
            else
            {
                index = _assetRequests.Add(new(assetName, handler));
            }

            if (_state == EPackageState.Loaded)
            {
                _packageHandle.LoadAssetAsync(assetName, index);
            }
            else
            {
                // postpone until assetbundle loaded
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
                        handler?.OnPackageLoaded(_packageHandle);
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
                        _packageHandle.LoadAsync();
                        CheckDependenciesState();
                        return;
                    }
                default:
                    {
                        Utility.SAssert.Never("should not happen");
                        index = SIndex.None;
                        return;
                    }
            }

        }

        internal void AddDependency(UPackage package)
        {
            Utility.SAssert.Debug(package != null, "add dependency with null package");
            Utility.SAssert.Debug(_state == EPackageState.Created, "call AddDependency only on init");
            Utility.SAssert.Debug(!_dependencies.Contains(package) && package != this);
            _dependencies.Add(package);
        }

        void IPackageRequestHandler.OnAssetLoaded(in SIndex index, object target)
        {
            if (_assetRequests.TryRemoveAt(index, out var value) && value.handler.TryGetTarget(out var handler))
            {
                handler.OnRequestCompleted(target as UnityEngine.Object);
            }
        }

        void IPackageRequestHandler.OnPackageLoaded(in PackageManager.PackageHandle handle)
        {
            if (handle == _packageHandle)
            {
                Utility.SLogger.Debug("{0} loaded {1}", nameof(UPackage), _packageHandle.name);
            }
            CheckDependenciesState();
        }

        private void CheckDependenciesState()
        {
            if (_state != EPackageState.Loading)
            {
                return;
            }

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

            _state = EPackageState.Loaded;
            OnLoaded();
        }

        private void OnLoaded()
        {
            Utility.SLogger.Debug("{0} fully loaded {1}", nameof(UPackage), _packageHandle.name);
            while (_packageRequestHandlers.TryRemoveAt(0, out var handler))
            {
                handler.OnPackageLoaded(_packageHandle);
            }
            var e = _assetRequests.GetStableIndexEnumerator();
            while (e.MoveNext())
            {
                var index = e.Current;
                var req = e.Value;
                _packageHandle.LoadAssetAsync(req.assetName, index);
            }
        }
    }
}