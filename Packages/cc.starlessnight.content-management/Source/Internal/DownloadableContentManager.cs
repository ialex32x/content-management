using System;
using System.Collections.Generic;

namespace Iris.ContentManagement.Internal
{
    public class DownloadableContentManager : IContentManager
    {
        private ContentLibrary _library;
        private PackageManager _packageManager;

        private EdSimulatedPackage _defaultPackage;
        private Dictionary<string, WeakReference<ManagedPackage>> _cachedPackages = new();
        private Dictionary<string, WeakReference<IAsset>> _cachedAssets = new();

        public DownloadableContentManager(ContentLibrary library, PackageManager packageManager)
        {
            _library = library;
            _packageManager = packageManager;
        }

        public void Shutdown()
        {
        }

        private EdSimulatedPackage GetDefaultPackage()
        {
            if (_defaultPackage == null)
            {
                _defaultPackage = new EdSimulatedPackage();
            }
            return _defaultPackage;
        }

        private IPackage GetPackage(in ContentLibrary.EntryInfo entryInfo) => entryInfo.isValid ? GetPackage(entryInfo.package) : GetDefaultPackage();

        private ManagedPackage GetPackage(in ContentLibrary.PackageInfo packageInfo)
        {
            if (!packageInfo.isValid)
            {
                return ManagedPackage.Null;
            }
            if (_cachedPackages.TryGetValue(packageInfo.name, out var weakReference) && weakReference.TryGetTarget(out var package))
            {
                return package;
            }
            package = new ManagedPackage(_packageManager.CreatePackage(packageInfo));
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
            var package = GetPackage(entryInfo);
            asset = new ManagedAsset(package, assetPath);
            _cachedAssets[assetPath] = new(asset);
            return asset;
        }
    }
}