using System;
using System.IO;
using System.Threading;

namespace Iris.ContentManagement.Internal
{
    using Iris.ContentManagement.Utility;
    using UnityEngine;

    public sealed partial class PackageManager
    {
        public readonly struct PackageHandle : IEquatable<PackageHandle>
        {
            private readonly PackageManager _manager;
            private readonly Utility.SIndex _index;

            // name of the referenced package object, null if not existed
            public readonly string name => _manager.GetReferenceName(_index);

            // the referenced package object is invalid or loaded
            public readonly bool isCompleted => _manager.IsReferenceCompleted(_index);

            public PackageHandle(PackageManager manager, Utility.SIndex index) => (this._manager, this._index) = (manager, index);

            // only supported by assetbundle package
            internal UnityEngine.Object LoadAsset(string assetName) => _manager.LoadAsset(_index, assetName);

            // only supported by assetbundle package
            internal AssetBundleRequest LoadAssetAsync(string assetName) => _manager.LoadAssetAsync(_index, assetName);

            internal void LoadSync() => _manager.LoadPackageSync(_index);

            internal void LoadAsync(IPackageRequestHandler callback) => _manager.LoadPackageAsync(_index, callback);

            public void Unload() => _manager.UnloadAssetBundle(_index);

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