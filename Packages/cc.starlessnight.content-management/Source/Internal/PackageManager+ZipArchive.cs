using System;

namespace Iris.ContentManagement.Internal
{
    using Net;
    using Iris.ContentManagement.Utility;

    public sealed partial class PackageManager
    {
        private class ZipArchiveSlot : PackageSlotBase, IPackageSlot
        {
            [Flags]
            private enum EState
            {
                None,
                Loading,
                Loaded,
                UnloadAfterLoading,
                Invalid,

                CompletedStates = Loaded | Invalid,
            }

            private WebRequestHandle _webRequest;
            private EState _state;
            private ICSharpCode.SharpZipLib.Zip.ZipFile _file;

            public bool isCompleted => (_state & EState.CompletedStates) != 0;

            public ZipArchiveSlot(PackageManager manager, in SIndex index, string name, in ContentDigest digest) : base(manager, index, name, digest)
            {
            }

            public System.IO.Stream LoadStream(string assetName)
            {
                if (_file == null)
                {
                    return default;
                }
                var entry = _file.GetEntry(assetName);
                var result = _file.GetInputStream(entry);
                Utility.SLogger.Debug("load stream {0} => {1}", assetName, result);
                return result;
            }

            public object LoadAsset(string assetName) => new ManagedStream(_manager, _index);

            public void RequestAssetAsync(string assetName, SIndex payload)
            {
                //TODO make the callback async even if invalid
                if (_handler.TryGetTarget(out var target))
                {
                    Utility.SLogger.Debug("zip asset {0}", assetName);
                    var asset = LoadAsset(assetName);
                    target.OnAssetLoaded(payload, asset);
                }
            }

            private void OnReleased()
            {
                Utility.SAssert.Debug(this._state == EState.None);
                Utility.SAssert.Debug(this._file == null);
                this._manager.ReleaseSlot(this._index);
                this._webRequest.Unbind();
                this._handler.SetTarget(null);
            }

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
                        // 执行中, 无需调整
                        break;
                    case EState.Loaded:
                        // 已加载的情况下, 直接触发事件
                        this.OnPackageLoaded();
                        break;
                    case EState.UnloadAfterLoading:
                        // resurrect, 仅在 loading 转 unload 时出现, 恢复加载中标记即可
                        Utility.SAssert.Debug(this._file == null);
                        this._state = EState.Loading;
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
                    case EState.Loaded:
                        if (this._file != null)
                        {
                            this._file.Close();
                            this._file = null;
                        }
                        this._state = EState.None;
                        OnReleased();
                        break;
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
                if (stream == null)
                {
                    if (shouldDownloadMissing)
                    {
                        this._webRequest = _manager.EnqueueWebRequest(this.name, this.digest.size);
                        if (this._webRequest.isValid)
                        {
                            this._webRequest.Bind(OnWebRequestCompleted());
                            return;
                        }
                    }
                }
                else
                {
                    this._file = new ICSharpCode.SharpZipLib.Zip.ZipFile(stream);
                    this._file.IsStreamOwner = true;
                }

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
                            // 尚未加载 package, 直接标记为卸载状态即可
                            this._state = EState.None;
                            OnReleased();
                            break;
                        default:
                            Utility.SAssert.Never();
                            break;
                    }
                };
            }
            #endregion // Package Access
        }
    }
}