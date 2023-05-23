using System;
using System.IO;
using System.Threading;

namespace Iris.ContentManagement.Internal
{
    using Iris.ContentManagement.Utility;
    using UnityEngine;

    // 管理 AssetBundle 的加载
    public sealed class AssetBundleManager
    {
        public readonly struct AssetBundleHandle : IEquatable<AssetBundleHandle>
        {
            private readonly AssetBundleManager _manager;
            private readonly Utility.SIndex _index;

            public readonly bool isValid => _manager.IsValidAssetBundle(_index);

            public readonly string name => _manager.GetAssetBundleName(_index);

            public AssetBundleHandle(AssetBundleManager manager, Utility.SIndex index) => (this._manager, this._index) = (manager, index);

            public UnityEngine.Object LoadAsset(string assetName) => _manager.LoadAsset(_index, assetName);

            public AssetBundleRequest LoadAssetAsync(string assetName) => _manager.LoadAssetAsync(_index, assetName);

            public void LoadSync() => _manager.LoadAssetBundleSync(_index);

            public void LoadAsync(IAssetBundleRequestHandler callback) => _manager.LoadAssetBundleAsync(_index, callback);

            /// <summary>
            /// [threading] 卸载
            /// </summary>
            public void Unload() => _manager.UnloadAssetBundle(_index);

            public bool Equals(in AssetBundleHandle other) => this == other;

            public bool Equals(AssetBundleHandle other) => this == other;

            public override bool Equals(object obj) => obj is AssetBundleHandle other && this == other;

            public override int GetHashCode() => (int)(_manager.GetHashCode() ^ _index.GetHashCode());

            public override string ToString() => name;

            public static bool operator ==(AssetBundleHandle a, AssetBundleHandle b) => a._manager == b._manager && a._index == b._index;

            public static bool operator !=(AssetBundleHandle a, AssetBundleHandle b) => a._manager != b._manager || a._index != b._index;
        }

        [Flags]
        private enum ESlotState
        {
            Created = 1,
            Loading = 2,
            Loaded = 4,
            Unloading = 8,
            WaitForUnload = 16,
            WaitForLoad = 32,
            Invalid = 64,

            // 未完成的操作
            PendingFlags = Loading | Unloading | WaitForUnload | WaitForLoad,
        }

        private class AssetBundleSlot
        {
            private WeakReference<IAssetBundleRequestHandler> _handler = new(null);

            public readonly string name;
            public readonly ContentDigest digest;

            public ESlotState state;
            public AssetBundle assetBundle;
            public Stream stream;
            public WebRequestHandle webRequest;

            public AsyncOperation unloadingOperation;
            public AssetBundleCreateRequest loadingOperation;

            public AssetBundleSlot(string name, in ContentDigest digest)
            {
                this.name = name;
                this.digest = digest;
                this.state = ESlotState.Created;
            }

            public void WaitUntilCompleted()
            {
                while ((state & ESlotState.PendingFlags) != 0)
                {
                    Utility.Logger.Debug("wait for pending operation {0}: {1}", name, state);
                    Scheduler.ForceUpdate();
                }
            }

            public void Bind(IAssetBundleRequestHandler handler)
            {
                _handler.SetTarget(handler);
            }

            public void Unbind()
            {
                _handler.SetTarget(null);
            }

            public void Notify()
            {
                if (_handler.TryGetTarget(out var target))
                {
                    try
                    {
                        _handler.SetTarget(null);
                        target.OnAssetBundleLoaded();
                    }
                    catch (Exception exception)
                    {
                        Utility.Logger.Exception(exception, "AssetBundleSlot.callback failed");
                    }
                }
            }
        }

        private int _mainThreadId;

        private IFileCache _fileCache;
        private LocalStorage _storage;
        private IWebRequestQueue _downloader;
        private SList<AssetBundleSlot> _assetBundles = new();

        public AssetBundleManager(IFileCache fileCache, IWebRequestQueue downloader, LocalStorage storage)
        {
            _mainThreadId = Thread.CurrentThread.ManagedThreadId;
            _fileCache = fileCache;
            _storage = storage;
            _downloader = downloader;
        }

        public AssetBundleHandle CreateAssetBundle(in ContentLibrary.PackageInfo packageInfo)
        {
            return new(this, _assetBundles.Add(new AssetBundleSlot(packageInfo.name, packageInfo.digest)));
        }

        public void Shutdown()
        {
            while (_assetBundles.TryRemoveAt(0, out var assetBundleSlot))
            {
                assetBundleSlot.Unbind();
                UnloadAssetBundleAsyncSafe(assetBundleSlot);
                assetBundleSlot.WaitUntilCompleted();
            }
        }

        private string GetAssetBundleName(in SIndex referenceIndex)
        {
            return _assetBundles.TryGetValue(referenceIndex, out var assetBundleSlot) ? assetBundleSlot.name : default;
        }

        private bool IsValidAssetBundle(in SIndex referenceIndex)
        {
            return _assetBundles.IsValidIndex(referenceIndex);
        }

        private UnityEngine.Object LoadAsset(in SIndex referenceIndex, string assetName)
        {
            if (_assetBundles.TryGetValue(referenceIndex, out var assetBundleSlot))
            {
                Utility.Assert.Debug(assetBundleSlot.state == ESlotState.Loaded || assetBundleSlot.state == ESlotState.Invalid);
                if (assetBundleSlot.assetBundle != null)
                {
                    return assetBundleSlot.assetBundle.LoadAsset(assetName);
                }
            }
            return default;
        }

        private AssetBundleRequest LoadAssetAsync(in SIndex referenceIndex, string assetName)
        {
            if (_assetBundles.TryGetValue(referenceIndex, out var assetBundleSlot))
            {
                Utility.Assert.Debug(assetBundleSlot.state == ESlotState.Loaded || assetBundleSlot.state == ESlotState.Invalid);
                if (assetBundleSlot.assetBundle != null)
                {
                    return assetBundleSlot.assetBundle.LoadAssetAsync(assetName);
                }
            }
            return default;
        }

        private void LoadAssetBundleSync(in SIndex referenceIndex)
        {
            LoadAssetBundleAsync(referenceIndex, null);
            if (_assetBundles.TryGetValue(referenceIndex, out var assetBundleSlot))
            {
                assetBundleSlot.WaitUntilCompleted();
            }
        }

        private void LoadAssetBundleAsync(in SIndex referenceIndex, IAssetBundleRequestHandler callback)
        {
            if (!_assetBundles.TryGetValue(referenceIndex, out var assetBundleSlot))
            {
                Utility.Logger.Error("invalid assetbundle reference {0}", referenceIndex);
                return;
            }

            assetBundleSlot.Bind(callback);
            switch (assetBundleSlot.state)
            {
                case ESlotState.WaitForUnload:
                    // resurrect
                    Utility.Assert.Debug(assetBundleSlot.assetBundle != null);
                    assetBundleSlot.state = ESlotState.Loaded;
                    assetBundleSlot.Notify();
                    break;
                case ESlotState.WaitForLoad:
                case ESlotState.Loading:
                    {
                        break;
                    }
                case ESlotState.Created:
                    {
                        assetBundleSlot.state = ESlotState.Loading;
                        LoadAssetBundleImpl(assetBundleSlot, true);
                        break;
                    }
                case ESlotState.Invalid:
                case ESlotState.Loaded:
                    {
                        assetBundleSlot.Notify();
                        break;
                    }
                case ESlotState.Unloading:
                    assetBundleSlot.state = ESlotState.WaitForLoad;
                    break;
                default:
                    Utility.Assert.Never();
                    break;
            }
        }

        /// <summary>
        /// [threading] 卸载 AssetBundle 
        /// </summary>
        private void UnloadAssetBundle(SIndex referenceIndex)
        {
            if (_mainThreadId != Thread.CurrentThread.ManagedThreadId)
            {
                Scheduler.Get().Post(() =>
                {
                    if (_assetBundles.TryGetValue(referenceIndex, out var assetBundleSlot))
                    {
                        UnloadAssetBundleAsyncSafe(assetBundleSlot);
                    }
                });
            }
            else
            {
                if (_assetBundles.TryGetValue(referenceIndex, out var assetBundleSlot))
                {
                    UnloadAssetBundleAsyncSafe(assetBundleSlot);
                }
            }
        }

        private void UnloadAssetBundleAsyncSafe(AssetBundleSlot assetBundleSlot)
        {
            switch (assetBundleSlot.state)
            {
                case ESlotState.Loading:
                    // 当前载入中, 标记成待卸载
                    assetBundleSlot.state = ESlotState.WaitForUnload;
                    break;
                case ESlotState.WaitForLoad:
                    // 当前等待载入 (实际已经在执行卸载)
                    Utility.Assert.Debug(assetBundleSlot.unloadingOperation != null);
                    assetBundleSlot.state = ESlotState.Unloading;
                    break;
                case ESlotState.Loaded:
                    Utility.Assert.Debug(assetBundleSlot.assetBundle != null);
                    assetBundleSlot.state = ESlotState.Unloading;
                    assetBundleSlot.unloadingOperation = assetBundleSlot.assetBundle.UnloadAsync(true);
                    assetBundleSlot.unloadingOperation.completed += OnAssetBundleUnloaded(assetBundleSlot);
                    break;
                case ESlotState.Unloading: // 已在执行卸载
                case ESlotState.WaitForUnload: // 已请求执行卸载
                case ESlotState.Created: // 已卸载
                case ESlotState.Invalid: // 无效包, 无需卸载
                    break;
                default:
                    Utility.Assert.Never();
                    break;
            }
        }

        private void LoadAssetBundleImpl(AssetBundleSlot assetBundleSlot, bool shouldDownloadMissing)
        {
            var stream = _fileCache.OpenRead(assetBundleSlot.name, assetBundleSlot.digest);
            if (stream != null)
            {
                assetBundleSlot.stream = stream;
                assetBundleSlot.loadingOperation = AssetBundle.LoadFromStreamAsync(assetBundleSlot.stream);
                assetBundleSlot.loadingOperation.completed += OnAssetBundleLoaded(assetBundleSlot);
                return;
            }

            if (shouldDownloadMissing && _downloader != null)
            {
                assetBundleSlot.webRequest = _downloader.Enqueue(_storage, assetBundleSlot.name, assetBundleSlot.digest.size);
                assetBundleSlot.webRequest.Bind(OnWebRequestCompleted(assetBundleSlot));
                return;
            }

            // 下载完成后仍然无法载入, 视为无效
            assetBundleSlot.state = ESlotState.Invalid;
            assetBundleSlot.Notify();
        }

        private WebRequestAction OnWebRequestCompleted(AssetBundleSlot assetBundleSlot)
        {
            return result =>
            {
                Utility.Assert.Debug(assetBundleSlot.webRequest.info == result.info, "callback on incorrect web request");
                assetBundleSlot.webRequest.Unbind();
                switch (assetBundleSlot.state)
                {
                    case ESlotState.Loading:
                        LoadAssetBundleImpl(assetBundleSlot, false);
                        break;
                    case ESlotState.WaitForUnload:
                        // 尚未加载 ab, 直接标记为卸载状态即可
                        Utility.Assert.Debug(assetBundleSlot.assetBundle == null);
                        assetBundleSlot.state = ESlotState.Created;
                        break;
                    default:
                        Utility.Assert.Never();
                        break;
                }
            };
        }

        private Action<AsyncOperation> OnAssetBundleLoaded(AssetBundleSlot assetBundleSlot)
        {
            return op =>
            {
                // 忽略因为强制同步流程已经处理过的情况
                if (assetBundleSlot.loadingOperation != op)
                {
                    return;
                }

                assetBundleSlot.assetBundle = assetBundleSlot.loadingOperation.assetBundle;
                assetBundleSlot.loadingOperation = null;
                switch (assetBundleSlot.state)
                {
                    case ESlotState.Loading:
                        {
                            assetBundleSlot.state = ESlotState.Loaded;
                            assetBundleSlot.Notify();
                        }
                        break;
                    case ESlotState.WaitForUnload:
                        if (assetBundleSlot.assetBundle != null)
                        {
                            assetBundleSlot.state = ESlotState.Unloading;
                            assetBundleSlot.unloadingOperation = assetBundleSlot.assetBundle.UnloadAsync(true);
                            assetBundleSlot.unloadingOperation.completed += OnAssetBundleUnloaded(assetBundleSlot);
                        }
                        else
                        {
                            assetBundleSlot.state = ESlotState.Created;
                        }
                        break;
                    default:
                        Utility.Assert.Never();
                        break;
                }
            };
        }

        private Action<AsyncOperation> OnAssetBundleUnloaded(AssetBundleSlot assetBundleSlot)
        {
            return op =>
            {
                if (assetBundleSlot.unloadingOperation != op)
                {
                    return;
                }
                assetBundleSlot.unloadingOperation = null;
                switch (assetBundleSlot.state)
                {
                    case ESlotState.Unloading:
                        assetBundleSlot.state = ESlotState.Created;
                        break;
                    case ESlotState.WaitForLoad:
                        LoadAssetBundleImpl(assetBundleSlot, true);
                        break;
                    default:
                        Utility.Assert.Never();
                        break;
                }
            };
        }
    } // - class AssetBundleManager
} // - namespace
