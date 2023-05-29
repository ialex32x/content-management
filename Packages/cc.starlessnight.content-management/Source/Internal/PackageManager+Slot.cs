using System;

namespace Iris.ContentManagement.Internal
{
    using Iris.ContentManagement.Utility;

    public sealed partial class PackageManager
    {
        private interface IPackageSlot
        {
            string name { get; }
            ContentDigest digest { get; }

            bool isCompleted { get; }

            void Bind(IManagedPackageRequestHandler handler);
            void Unbind();

            void Load();
            void Unload();

            System.IO.Stream LoadStream(string assetName);

            object LoadAsset(string assetName);

            void RequestAssetAsync(string assetName, Utility.SIndex payload);
        }

        private class PackageSlotBase
        {
            protected readonly PackageManager _manager;
            // this reference index for fastpath
            protected readonly SIndex _index;
            protected WeakReference<IManagedPackageRequestHandler> _handler = new(null);

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

            public void Bind(IManagedPackageRequestHandler handler)
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
                        Utility.SLogger.Exception(exception, "AssetBundleSlot.callback failed");
                    }
                }
            }
        }
    }
}
