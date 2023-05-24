using System;
using System.IO;
using System.Threading;

namespace Iris.ContentManagement.Internal
{
    using Iris.ContentManagement.Utility;
    using UnityEngine;

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

        public PackageHandle CreateAssetBundle(in ContentLibrary.PackageInfo packageInfo)
        {
            Utility.Assert.Debug(packageInfo.type == EPackageType.AssetBundle);
            var index = _packages.Add(default);
            var slot = new AssetBundleSlot(this, index, packageInfo.name, packageInfo.digest);
            _packages.UnsafeSetValue(index, slot);
            return new(this, index);
        }

        public PackageHandle CreateZipArchive(in ContentLibrary.PackageInfo packageInfo)
        {
            Utility.Assert.Debug(packageInfo.type == EPackageType.Zip);
            var index = _packages.Add(default);
            var slot = new ZipArchiveSlot(this, index, packageInfo.name, packageInfo.digest);
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
                slot.Unload(false);
                slot.WaitUntilCompleted();
            }
        }

        private void ReleaseSlot(in SIndex referenceIndex)
        {
            _packages.RemoveAt(referenceIndex);
        }

        private string GetReferenceName(in SIndex referenceIndex) => _packages.TryGetValue(referenceIndex, out var slot) ? slot.name : default;

        private bool IsReferenceCompleted(in SIndex referenceIndex) => _packages.TryGetValue(referenceIndex, out var slot) ? slot.isCompleted : true;

        private void VerifyState(string contextObject)
        {
            if (_released)
            {
                throw new ObjectDisposedException(contextObject, "manager is released");
            }
        }

        private UnityEngine.Object LoadAsset(in SIndex referenceIndex, string assetName)
        {
            VerifyState(assetName);
            if (_packages.TryGetValue(referenceIndex, out var slot))
            {
                return slot.LoadAsset(assetName);
            }
            return default;
        }

        private AssetBundleRequest LoadAssetAsync(in SIndex referenceIndex, string assetName)
        {
            VerifyState(assetName);
            if (_packages.TryGetValue(referenceIndex, out var slot))
            {
                return slot.LoadAssetAsync(assetName);
            }
            return default;
        }

        //NOTE will remove the last callback
        private void LoadPackageSync(in SIndex referenceIndex)
        {
            LoadPackageAsync(referenceIndex, null);
            if (_packages.TryGetValue(referenceIndex, out var slot))
            {
                slot.WaitUntilCompleted();
            }
        }

        private void LoadPackageAsync(in SIndex referenceIndex, IPackageRequestHandler callback)
        {
            if (!_packages.TryGetValue(referenceIndex, out var slot))
            {
                Utility.Logger.Error("invalid assetbundle reference {0}", referenceIndex);
                return;
            }

            VerifyState(slot.name);
            slot.Bind(callback);
            slot.Load(true);
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
                    if (_packages.TryGetValue(referenceIndex, out var slot))
                    {
                        slot.Unload(true);
                    }
                });
            }
            else
            {
                if (_packages.TryGetValue(referenceIndex, out var slot))
                {
                    slot.Unload(true);
                }
            }
        }
    } // - class AssetBundleManager
} // - namespace
