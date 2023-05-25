using System;
using System.IO;
using System.Threading;

namespace Iris.ContentManagement.Internal
{
    using Iris.ContentManagement.Utility;
    using UnityEngine;

    public sealed partial class PackageManager
    {
        private interface IPackageSlot
        {
            string name { get; }
            ContentDigest digest { get; }

            bool isCompleted { get; }

            void Bind(IPackageRequestHandler handler);
            void Unbind();

            void Load();
            void Unload();

            object LoadAsset(string assetName);

            void RequestAssetAsync(string assetName, Utility.SIndex payload);
        }

        private class PackageSlotBase
        {
            protected readonly PackageManager _manager;
            protected readonly SIndex _index;
            protected WeakReference<IPackageRequestHandler> _handler = new(null);

            protected readonly string _name;
            protected readonly ContentDigest _digest;

            public string name => _name;
            public ContentDigest digest => _digest;

            public PackageSlotBase(PackageManager manager, in SIndex index, string name, in ContentDigest digest)
            {
                this._index = index;
                this._manager = manager;
                this._name = name;
                this._digest = digest;
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
    }
}
