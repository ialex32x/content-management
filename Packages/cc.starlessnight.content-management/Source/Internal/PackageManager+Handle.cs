using System;

namespace Iris.ContentManagement.Internal
{
    public sealed partial class PackageManager
    {
        internal readonly struct PackageHandle : IEquatable<PackageHandle>
        {
            private readonly PackageManager _manager;
            private readonly Utility.SIndex _index;

            // name of the referenced package object, null if not existed
            public readonly string name => _manager.GetReferenceName(_index);

            // the referenced package object is invalid or loaded
            public readonly bool isCompleted => _manager.IsReferenceCompleted(_index);

            public PackageHandle(PackageManager manager, Utility.SIndex index) => (this._manager, this._index) = (manager, index);

            internal void Bind(IManagedPackageRequestHandler callback) => _manager.Bind(_index, callback);

            internal void LoadSync() => _manager.LoadPackageSync(_index);

            internal void LoadAsync() => _manager.LoadPackageAsync(_index);

            public void Unload() => _manager.UnloadPackage(_index);

            // internal Stream OpenRead(string assetName) => _manager.OpenRead(_index, assetName);

            internal object LoadAsset(string assetName) => _manager.LoadAsset(_index, assetName);

            internal void LoadAssetAsync(string assetName, in Utility.SIndex payload) => _manager.LoadAssetAsync(_index, assetName, payload);

            public bool Equals(in PackageHandle other) => this == other;

            public bool Equals(PackageHandle other) => this == other;

            public override bool Equals(object obj) => obj is PackageHandle other && this == other;

            public override int GetHashCode() => (int)(_manager.GetHashCode() ^ _index.GetHashCode());

            public override string ToString() => name;

            public static bool operator ==(PackageHandle a, PackageHandle b) => a._manager == b._manager && a._index == b._index;

            public static bool operator !=(PackageHandle a, PackageHandle b) => a._manager != b._manager || a._index != b._index;
        }
    }
}