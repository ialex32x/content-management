using System;
using System.Collections.Generic;

namespace Iris.ContentManagement.Internal
{
    using Iris.ContentManagement.Utility;

    public class DownloadableContentManager : IContentManager
    {
        private ContentLibrary _library;
        private IWebRequestQueue _downloader;
        private LocalStorage _storage;
        private PackageManager _packageManager;

        private Dictionary<string, WeakReference<UPackage>> _cachedPackages = new();
        private Dictionary<string, WeakReference<IAsset>> _cachedAssets = new();

        public DownloadableContentManager(ContentLibrary library, IFileCache fileCache, LocalStorage storage, IWebRequestQueue downloader)
        {
            _library = library;
            _storage = storage;
            _packageManager = new PackageManager(fileCache, _storage, downloader);
        }

        public void Shutdown()
        {
            _storage.Shutdown();
        }

        public string[] GetDependencies(string packageName)
        {
#if IRIS_DIRECT_DEPS_ONLY
            var openSet = new List<string>();
            var closeSet = new HashSet<string>();

            openSet.Add(packageName);
            while (openSet.Count != 0)
            {
                var first = openSet[openSet.Count - 1];
                openSet.RemoveAt(openSet.Count - 1);
                closeSet.Add(first);

                var dependencies = _library.GetPackageDependencies(first);
                if (dependencies != null)
                {
                    for (int i = 0, size = dependencies.Length; i < size; i++)
                    {
                        var item = dependencies[i];
                        if (!closeSet.Contains(item))
                        {
                            openSet.Add(item);
                            closeSet.Add(item);
                        }
                    }
                }
            }
            closeSet.Remove(packageName);

            var results = new string[closeSet.Count];
            closeSet.CopyTo(results, 0);
            return results;
#else
            var dependencies = _library.GetPackageDependencies(packageName);
            return dependencies ?? new string[0];
#endif
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
            foreach (var dependency in GetDependencies(packageInfo.name))
            {
                var dependencyPackage = GetPackage(_library.GetPackage(dependency));
                package.AddDependency(dependencyPackage);
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