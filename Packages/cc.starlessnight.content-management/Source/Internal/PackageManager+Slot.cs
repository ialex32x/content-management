using System;
using System.IO;
using System.Threading;

namespace Iris.ContentManagement.Internal
{
    using Iris.ContentManagement.Utility;
    using UnityEngine;

    public sealed partial class PackageManager
    {
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
            CompletedFlags = Loaded | Invalid,
        }

        private interface IPackageSlot
        {
            string name { get; }
            ContentDigest digest { get; }

            bool isCompleted { get; }

            void WaitUntilCompleted();
            void Bind(IPackageRequestHandler handler);
            void Unbind();

            void Load(bool shouldDownloadMissing);
            void Unload(bool bPreferAsync);

            UnityEngine.Object LoadAsset(string assetName);
            void RequestAssetAsync(string assetName, Utility.SIndex payload);
        }

        private class PackageSlotBase
        {
            protected readonly PackageManager _manager;
            protected readonly SIndex _index;
            protected WeakReference<IPackageRequestHandler> _handler = new(null);
            protected ESlotState _state;

            protected readonly string _name;
            protected readonly ContentDigest _digest;

            public string name => _name;
            public ContentDigest digest => _digest;

            public bool isCompleted => (_state & ESlotState.CompletedFlags) != 0;

            public PackageSlotBase(PackageManager manager, in SIndex index, string name, in ContentDigest digest)
            {
                this._index = index;
                this._manager = manager;
                this._name = name;
                this._digest = digest;
                this._state = ESlotState.Created;
            }

            public void WaitUntilCompleted()
            {
                while ((_state & ESlotState.PendingFlags) != 0)
                {
                    Utility.Logger.Debug("wait for pending operation {0}: {1}", name, _state);
                    Scheduler.ForceUpdate();
                }
            }

            public void Bind(IPackageRequestHandler handler)
            {
                _handler.SetTarget(handler);
            }

            public void Unbind()
            {
                _handler.SetTarget(null);
            }

            protected void OnPackageLoaded()
            {
                if (_handler.TryGetTarget(out var target))
                {
                    try
                    {
                        target.OnPackageLoaded(new(_manager, _index));
                    }
                    catch (Exception exception)
                    {
                        Utility.Logger.Exception(exception, "AssetBundleSlot.callback failed");
                    }
                }
            }
        }

        //TODO WIP 
        private class ZipArchiveSlot : PackageSlotBase, IPackageSlot
        {
            private Stream _stream;
            private WebRequestHandle _webRequest;
            private ICSharpCode.SharpZipLib.Zip.ZipFile _file;

            public ZipArchiveSlot(PackageManager manager, in SIndex index, string name, in ContentDigest digest) : base(manager, index, name, digest)
            {
            }

            public void Load(bool shouldDownloadMissing)
            {
                throw new NotImplementedException();
            }

            public void Unload(bool bPreferAsync)
            {
                throw new NotImplementedException();
            }

            public UnityEngine.Object LoadAsset(string assetName) => throw new NotSupportedException();
            public void RequestAssetAsync(string assetName, SIndex payload) => throw new NotSupportedException();
        }

        private class AssetBundleSlot : PackageSlotBase, IPackageSlot
        {
            private Stream _stream;
            private WebRequestHandle _webRequest;

            private AssetBundle _assetBundle;

            private AsyncOperation _unloadingOperation;
            private AssetBundleCreateRequest _loadingOperation;

            public AssetBundleSlot(PackageManager manager, in SIndex index, string name, in ContentDigest digest) : base(manager, index, name, digest)
            {
            }

            public UnityEngine.Object LoadAsset(string assetName)
            {
                Utility.Assert.Debug(this._state == ESlotState.Loaded || this._state == ESlotState.Invalid);
                return this._assetBundle != null ? this._assetBundle.LoadAsset(assetName) : default;
            }

            public void RequestAssetAsync(string assetName, SIndex payload)
            {
                Utility.Assert.Debug(this._state == ESlotState.Loaded || this._state == ESlotState.Invalid);
                var request = this._assetBundle != null ? this._assetBundle.LoadAssetAsync(assetName) : default;
                if (request == null)
                {
                    OnAssetLoaded(payload, null);
                    return;
                }
                request.completed += op => OnAssetLoaded(payload, ((AssetBundleRequest)op).asset);
            }

            private void OnAssetLoaded(in SIndex payload, UnityEngine.Object asset)
            {
                if (_handler.TryGetTarget(out var target))
                {
                    target.OnAssetLoaded(payload, asset);
                }
            }

            public void Load(bool shouldDownloadMissing)
            {
                switch (this._state)
                {
                    case ESlotState.WaitForUnload:
                        // resurrect
                        Utility.Assert.Debug(this._assetBundle != null);
                        this._state = ESlotState.Loaded;
                        this.OnPackageLoaded();
                        break;
                    case ESlotState.WaitForLoad:
                    case ESlotState.Loading:
                        {
                            break;
                        }
                    case ESlotState.Created:
                        {
                            this._state = ESlotState.Loading;
                            var stream = _manager._fileCache.OpenRead(this.name, this.digest);
                            if (stream != null)
                            {
                                this._stream = stream;
                                this._loadingOperation = AssetBundle.LoadFromStreamAsync(this._stream);
                                this._loadingOperation.completed += OnAssetBundleLoaded();
                                return;
                            }

                            if (shouldDownloadMissing && _manager._downloader != null)
                            {
                                this._webRequest = _manager._downloader.Enqueue(_manager._storage, this.name, this.digest.size);
                                this._webRequest.Bind(OnWebRequestCompleted());
                                return;
                            }

                            // 下载完成后仍然无法载入, 视为无效
                            this._state = ESlotState.Invalid;
                            this.OnPackageLoaded();
                            break;
                        }
                    case ESlotState.Invalid:
                    case ESlotState.Loaded:
                        {
                            this.OnPackageLoaded();
                            break;
                        }
                    case ESlotState.Unloading:
                        this._state = ESlotState.WaitForLoad;
                        break;
                    default:
                        Utility.Assert.Never();
                        break;
                }
            }

            public void Unload(bool bPreferAsync)
            {
                switch (this._state)
                {
                    case ESlotState.Loading:
                        // 当前载入中, 标记成待卸载
                        this._state = ESlotState.WaitForUnload;
                        break;
                    case ESlotState.WaitForLoad:
                        // 当前等待载入 (实际已经在执行卸载)
                        Utility.Assert.Debug(this._unloadingOperation != null);
                        this._state = ESlotState.Unloading;
                        break;
                    case ESlotState.Loaded:
                        Utility.Assert.Debug(this._assetBundle != null);
                        this._state = ESlotState.Unloading;
                        if (bPreferAsync)
                        {
                            this._unloadingOperation = this._assetBundle.UnloadAsync(true);
                            this._unloadingOperation.completed += _ => OnAssetBundleUnloaded();
                        }
                        else
                        {
                            this._assetBundle.Unload(true);
                            OnAssetBundleUnloaded();
                        }
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

            private Action<AsyncOperation> OnAssetBundleLoaded()
            {
                return op =>
                {
                    // 忽略因为强制同步流程已经处理过的情况
                    if (this._loadingOperation != op)
                    {
                        return;
                    }

                    this._assetBundle = this._loadingOperation.assetBundle;
                    this._loadingOperation = null;
                    switch (this._state)
                    {
                        case ESlotState.Loading:
                            {
                                this._state = ESlotState.Loaded;
                                this.OnPackageLoaded();
                            }
                            break;
                        case ESlotState.WaitForUnload:
                            if (this._assetBundle != null)
                            {
                                this._state = ESlotState.Unloading;
                                this._unloadingOperation = this._assetBundle.UnloadAsync(true);
                                this._unloadingOperation.completed += _ => OnAssetBundleUnloaded();
                            }
                            else
                            {
                                OnReleased();
                            }
                            break;
                        default:
                            Utility.Assert.Never();
                            break;
                    }
                };
            }

            private void OnAssetBundleUnloaded()
            {
                this._unloadingOperation = null;
                this._assetBundle = null;
                if (this._stream != null)
                {
                    this._stream.Close();
                    this._stream = null;
                }
                switch (this._state)
                {
                    case ESlotState.Unloading:
                        OnReleased();
                        break;
                    case ESlotState.WaitForLoad:
                        this._state = ESlotState.Created;
                        Load(true);
                        break;
                    default:
                        Utility.Assert.Never();
                        break;
                }
            }

            private WebRequestAction OnWebRequestCompleted()
            {
                return result =>
                {
                    Utility.Assert.Debug(this._webRequest.info == result.info, "callback on incorrect web request");
                    this._webRequest.Unbind();
                    switch (this._state)
                    {
                        case ESlotState.Loading:
                            Load(false);
                            break;
                        case ESlotState.WaitForUnload:
                            // 尚未加载 ab, 直接标记为卸载状态即可
                            OnReleased();
                            break;
                        default:
                            Utility.Assert.Never();
                            break;
                    }
                };
            }

            private void OnReleased()
            {
                Utility.Assert.Debug(this._assetBundle == null);
                this._handler.SetTarget(null);
                this._state = ESlotState.Created;
                this._manager.ReleaseSlot(this._index);
                this._stream?.Close();
            }
        }
    }
}
