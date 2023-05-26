using System;
using System.Threading;

namespace Iris.ContentManagement.Internal
{
    using Cache;
    using Utility;

    // 管理 AssetBundle 的加载
    public sealed partial class PackageManager
    {
        private int _mainThreadId;

        private bool _released;
        private IFileCache _fileCache;
        private LocalStorage _storage;
        private IWebRequestQueue _downloader;
        private SList<IPackageSlot> _packages = new();

        public PackageManager(IFileCache fileCache, LocalStorage storage, IWebRequestQueue downloader)
        {
            _mainThreadId = Thread.CurrentThread.ManagedThreadId;
            _released = false;
            _fileCache = fileCache;
            _storage = storage;
            _downloader = downloader;
        }

        internal PackageHandle CreatePackage(in ContentLibrary.PackageInfo packageInfo)
        {
            Utility.SAssert.Debug(packageInfo.type == EPackageType.Zip);
            var index = _packages.Add(default);
            IPackageSlot slot = packageInfo.type == EPackageType.AssetBundle
                ? new AssetBundleSlot(this, index, packageInfo.name, packageInfo.digest)
                : new ZipArchiveSlot(this, index, packageInfo.name, packageInfo.digest);
            _packages.UnsafeSetValue(index, slot);
            return new(this, index);
        }

        public void Shutdown()
        {
            if (_released)
            {
                return;
            }
            _released = true;
            while (_packages.TryRemoveAt(0, out var slot))
            {
                slot.Unbind();
                slot.Unload();
                WaitUntilCompleted(slot);
            }
        }

        private void ReleaseSlot(in SIndex referenceIndex) => _packages.RemoveAt(referenceIndex);

        private string GetReferenceName(in SIndex referenceIndex) => _packages.TryGetValue(referenceIndex, out var slot) ? slot.name : default;

        private bool IsReferenceCompleted(in SIndex referenceIndex) => _packages.TryGetValue(referenceIndex, out var slot) ? slot.isCompleted : true;

        private void VerifyState(string contextObject)
        {
            if (_released)
            {
                throw new ObjectDisposedException(contextObject, "manager is released");
            }
        }

        private WebRequestHandle EnqueueWebRequest(string name, uint size)
        {
            if (_downloader == null)
            {
                return default;
            }
            return _downloader.Enqueue(_storage, name, size);
        }

        private void WaitUntilCompleted(IPackageSlot slot)
        {
            Utility.SLogger.Debug("wait for pending package {0}", slot.name);
            ContentSystem.Scheduler.WaitUntilCompleted(() => slot.isCompleted);
            Utility.SLogger.Debug("finish pending package {0}", slot.name);
        }

        private void Bind(in SIndex referenceIndex, IPackageRequestHandler callback)
        {
            if (!_packages.TryGetValue(referenceIndex, out var slot))
            {
                Utility.SLogger.Error("invalid assetbundle reference {0}", referenceIndex);
                return;
            }
            VerifyState(slot.name);
            slot.Bind(callback);
        }

        //NOTE will remove the last callback
        private void LoadPackageSync(in SIndex referenceIndex)
        {
            LoadPackageAsync(referenceIndex);
            if (_packages.TryGetValue(referenceIndex, out var slot))
            {
                WaitUntilCompleted(slot);
            }
        }

        private void LoadPackageAsync(in SIndex referenceIndex)
        {
            if (!_packages.TryGetValue(referenceIndex, out var slot))
            {
                Utility.SLogger.Error("invalid assetbundle reference {0}", referenceIndex);
                return;
            }
            VerifyState(slot.name);
            slot.Load();
        }

        /// <summary>
        /// [threading] 卸载 AssetBundle 
        /// </summary>
        private void UnloadPackage(SIndex referenceIndex)
        {
            if (_mainThreadId != Thread.CurrentThread.ManagedThreadId)
            {
                ContentSystem.Scheduler.Post(() =>
                {
                    if (_packages.TryGetValue(referenceIndex, out var slot))
                    {
                        slot.Unload();
                    }
                });
            }
            else
            {
                if (_packages.TryGetValue(referenceIndex, out var slot))
                {
                    slot.Unload();
                }
            }
        }

        // private Stream OpenRead(in SIndex referenceIndex, string assetName)
        // {
        //     VerifyState(assetName);
        //     if (_packages.TryGetValue(referenceIndex, out var slot))
        //     {
        //         return slot.OpenRead(assetName);
        //     }
        //     return default;
        // }

        private object LoadAsset(in SIndex referenceIndex, string assetName)
        {
            VerifyState(assetName);
            if (_packages.TryGetValue(referenceIndex, out var slot))
            {
                return slot.LoadAsset(assetName);
            }
            return default;
        }

        private void LoadAssetAsync(in SIndex referenceIndex, string assetName, in Utility.SIndex payload)
        {
            VerifyState(assetName);
            if (_packages.TryGetValue(referenceIndex, out var slot))
            {
                slot.RequestAssetAsync(assetName, payload);
            }
        }
    } // - class AssetBundleManager
} // - namespace
