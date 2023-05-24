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
        private PackageManager _assetBundleManager;

        private Dictionary<string, WeakReference<IPackage>> _cachedPackages = new();

        public DownloadableContentManager(ContentLibrary library, IFileCache fileCache, LocalStorage storage, IWebRequestQueue downloader)
        {
            _library = library;
            _storage = storage;
            _assetBundleManager = new PackageManager(fileCache, _storage, downloader);
        }

        public void Shutdown()
        {
            _storage.Shutdown();
        }

        public string[] GetDependencies(string packageName)
        {
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
        }

        public IPackage GetPackage(string packageName)
        {
            if (string.IsNullOrEmpty(packageName))
            {
                return default;
            }

            if (_cachedPackages.TryGetValue(packageName, out var weakReference) && weakReference.TryGetTarget(out var package))
            {
                return package;
            }

            var packageInfo = _library.GetPackage(packageName);
            if (packageInfo.isValid)
            {
                switch (packageInfo.type)
                {
                    case EPackageType.AssetBundle:
                        {
                            var assetBundlePackage = new AssetBundlePackage(_assetBundleManager.CreateAssetBundle(packageInfo));

                            package = assetBundlePackage;
                            _cachedPackages.Add(packageInfo.name, new(package));
                            foreach (var dependency in GetDependencies(packageInfo.name))
                            {
                                if (GetPackage(dependency) is AssetBundlePackage other)
                                {
                                    assetBundlePackage.AddDependency(other);
                                }
                                else
                                {
                                    Utility.Assert.Never("unsupported");
                                }
                            }
                            return package;
                        }
                    case EPackageType.Zip:
                        {
                            //TODO Zip 包访问
                            throw new NotImplementedException();
                        }
                    default: throw new ArgumentException();
                }
            }

            return default;
        }

        public IAsset GetAsset(string assetPath)
        {
            var package = GetPackage(_library.GetEntryPackage(assetPath).name);
            return package != null ? package.GetAsset(assetPath) : NullAsset.Default;
        }
    }
}