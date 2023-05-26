using System;
using System.IO;

namespace Iris.ContentManagement.Internal
{
    using Iris.ContentManagement.Utility;
    using UnityEngine;

    public sealed partial class PackageManager
    {
        private class AssetBundleSlot : PackageSlotBase, IPackageSlot
        {
            [Flags]
            private enum EState
            {
                None = 1,
                Loading = 2,
                Loaded = 4,
                Unloading = 8,
                UnloadAfterLoading = 16,
                LoadAfterUnloading = 32,

                CompletedStates = Loaded,
            }

            private Stream _stream;
            private WebRequestHandle _webRequest;
            private EState _state;

            private AssetBundle _assetBundle;

            public bool isCompleted => (_state & EState.CompletedStates) != 0;

            public AssetBundleSlot(PackageManager manager, in SIndex index, string name, in ContentDigest digest) : base(manager, index, name, digest)
            {
                this._state = EState.None;
            }

            private void OnReleased()
            {
                Utility.SAssert.Debug(this._state == EState.None);
                Utility.SAssert.Debug(this._assetBundle == null);
                this._manager.ReleaseSlot(this._index);
                this._webRequest.Unbind();
                this._handler.SetTarget(null);
                this._stream?.Close();
            }

            #region Asset Access - (only valid on loaded package)
            // public Stream OpenRead(string assetName) => throw new NotSupportedException();

            public object LoadAsset(string assetName)
            {
                Utility.SAssert.Debug(this._state == EState.Loaded);
                return this._assetBundle != null ? this._assetBundle.LoadAsset(assetName) : default;
            }

            public void RequestAssetAsync(string assetName, SIndex payload)
            {
                Utility.SAssert.Debug(this._state == EState.Loaded);
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
            #endregion // Asset Access

            #region Package Access
            public void Load()
            {
                switch (this._state)
                {
                    case EState.None:
                        this._state = EState.Loading;
                        LoadPackageFileImpl(true);
                        break;
                    case EState.Loading:
                    case EState.LoadAfterUnloading:
                        // 执行中, 无需调整
                        break;
                    case EState.Loaded:
                        // 已加载的情况下, 直接触发事件
                        this.OnPackageLoaded();
                        break;
                    case EState.UnloadAfterLoading:
                        // resurrect, 仅在 loading 转 unload 时出现, 恢复加载中标记即可
                        Utility.SAssert.Debug(this._assetBundle == null);
                        this._state = EState.Loading;
                        break;
                    case EState.Unloading:
                        this._state = EState.LoadAfterUnloading;
                        break;
                    default:
                        Utility.SAssert.Never();
                        break;
                }
            }

            public void Unload()
            {
                switch (this._state)
                {
                    case EState.Loading:
                        // 当前载入中, 标记成待卸载
                        this._state = EState.UnloadAfterLoading;
                        break;
                    case EState.LoadAfterUnloading:
                        // 当前等待载入 (实际已经在执行卸载)
                        this._state = EState.Unloading;
                        break;
                    case EState.Loaded:
                        if (this._assetBundle != null)
                        {
                            this._state = EState.Unloading;
                            if (!this._manager._released)
                            {
                                var unloadingOperation = this._assetBundle.UnloadAsync(true);
                                unloadingOperation.completed += _ => OnAssetBundleUnloaded();
                            }
                            else
                            {
                                this._assetBundle.Unload(true);
                                OnAssetBundleUnloaded();
                            }
                        }
                        else
                        {
                            // 无效包, 无需卸载
                            this._state = EState.None;
                            OnReleased();
                        }
                        break;
                    case EState.Unloading:          // 已在执行卸载
                    case EState.UnloadAfterLoading: // 已请求执行卸载
                    case EState.None:               // 已卸载
                        break;
                    default:
                        Utility.SAssert.Never();
                        break;
                }
            }

            // 优先打开本地缓存文件流, 其次下载文件 (完成后重新尝试打开本地缓存文件流)
            private void LoadPackageFileImpl(bool shouldDownloadMissing)
            {
                Utility.SAssert.Debug(this._state == EState.Loading);
                var stream = _manager._fileCache.OpenRead(this.name, this.digest);
                if (stream != null)
                {
                    this._stream = stream;
                    var loadingOperation = AssetBundle.LoadFromStreamAsync(this._stream);
                    loadingOperation.completed += OnAssetBundleLoaded();
                    return;
                }

                if (shouldDownloadMissing)
                {
                    this._webRequest = _manager.EnqueueWebRequest(this.name, this.digest.size);
                    if (this._webRequest.isValid)
                    {
                        this._webRequest.Bind(OnWebRequestCompleted());
                        return;
                    }
                }

                // 下载完成后仍然无法载入, 视为无效
                this._state = EState.Loaded;
                this.OnPackageLoaded();
            }

            private WebRequestAction OnWebRequestCompleted()
            {
                return result =>
                {
                    Utility.SAssert.Debug(this._webRequest.info == result.info, "callback on incorrect web request");
                    this._webRequest.Unbind();
                    switch (this._state)
                    {
                        case EState.Loading:
                            LoadPackageFileImpl(false);
                            break;
                        case EState.UnloadAfterLoading:
                            // 尚未加载 ab, 直接标记为卸载状态即可
                            this._state = EState.None;
                            OnReleased();
                            break;
                        default:
                            Utility.SAssert.Never();
                            break;
                    }
                };
            }

            private Action<AsyncOperation> OnAssetBundleLoaded()
            {
                return op =>
                {
                    // 忽略因为强制同步流程已经处理过的情况
                    switch (this._state)
                    {
                        case EState.Loading:
                            this._state = EState.Loaded;
                            this._assetBundle = ((AssetBundleCreateRequest)op).assetBundle;
                            this.OnPackageLoaded();
                            break;
                        case EState.UnloadAfterLoading:
                            this._state = EState.Loaded;
                            this._assetBundle = ((AssetBundleCreateRequest)op).assetBundle;
                            Unload();
                            break;
                        default:
                            Utility.SAssert.Never();
                            break;
                    }
                };
            }

            private void OnAssetBundleUnloaded()
            {
                switch (this._state)
                {
                    case EState.Unloading:
                        this._state = EState.None;
                        this.OnReleased();
                        break;
                    case EState.LoadAfterUnloading:
                        // 需要重新加载, 仅清理相关字段即可
                        Utility.SAssert.Debug(!this._webRequest.isValid);
                        this._state = EState.None;
                        this._assetBundle = null;
                        this._stream?.Close();
                        Load();
                        break;
                    default:
                        Utility.SAssert.Never();
                        break;
                }
            }

            #endregion // Package Access
        }
    }
}