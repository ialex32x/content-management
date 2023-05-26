using System;
using System.Collections.Generic;

namespace Iris.ContentManagement.Internal
{
    public class DownloadableContentManager : IContentManager
    {
        private ContentLibrary _library;
        private PackageManager _packageManager;

        private Dictionary<string, WeakReference<UPackage>> _cachedPackages = new();
        private Dictionary<string, WeakReference<IAsset>> _cachedAssets = new();

        public DownloadableContentManager(ContentLibrary library, PackageManager packageManager)
        {
            _library = library;
            _packageManager = packageManager;
        }

        public void Shutdown()
        {
        }

        private UPackage GetPackage(in ContentLibrary.PackageInfo packageInfo)
        {
            if (!packageInfo.isValid)
            {
                return UPackage.Null;
            }
            if (_cachedPackages.TryGetValue(packageInfo.name, out var weakReference) && weakReference.TryGetTarget(out var package))
            {
                return package;
            }
            package = new UPackage(_packageManager.CreatePackage(packageInfo));
            _cachedPackages.Add(packageInfo.name, new(package));
            foreach (var dependency in packageInfo.dependencies)
            {
                package.AddDependency(GetPackage(dependency));
            }
            return package;
        }

        public IAsset GetAsset(string assetPath)
        {
            if (_cachedAssets.TryGetValue(assetPath, out var weakReference) && weakReference.TryGetTarget(out var asset))
            {
                return asset;
            }
            var entryInfo = _library.GetEntry(assetPath);
            if (!entryInfo.isValid)
            {
                return EdSimulatedAsset.Null;
            }
            var package = GetPackage(entryInfo.package);
            asset = new PackageAsset(package, assetPath);
            _cachedAssets[assetPath] = new(asset);
            return asset;
        }
    }
}