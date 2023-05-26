using System;
using System.IO;
using System.Collections.Generic;

namespace Iris.ContentManagement.Internal
{
    using Iris.ContentManagement.Utility;

    //TODO entry guid 快速映射关系在外部实现, 如 FastEntryFinder

    /// <summary>
    /// 内容库 (最新版本信息)
    /// </summary>
    public partial class ContentLibrary
    {
        private const char PathSeparator = '/';

        private SIndex _root;
        private SList<DirectoryState> _directories = new();
        private SList<EntryState> _entries = new();
        private SList<PackageState> _packages = new();

        // entryPath => fileIndex
        private Dictionary<string, SIndex> _cachedEntryMap = new();

        // packageName => packageIndex
        private Dictionary<string, SIndex> _cachedPackageMap = new();

        /// <summary>
        /// 当前文件总数
        /// </summary>
        public int Count => _entries.Count;

        public int PackageCount => _packages.Count;

        public ContentLibrary()
        {
            _root = _directories.Add(new DirectoryState(SIndex.None, string.Empty));
        }

        private void CollectPackages(Dictionary<SIndex, HashSet<SIndex>> remap)
        {
            var e = _entries.GetStableIndexEnumerator();
            while (e.MoveNext())
            {
                var package = e.Value.package;
                Utility.SAssert.Debug(package.isValid, "entry not included in any package " + e.Value.entryPath);
                if (!remap.TryGetValue(package, out var list))
                {
                    list = remap[package] = new HashSet<SIndex>();
                }
                list.Add(e.Current);
            }
        }

        public PackageInfo AddPackage(string packageName, EPackageType type, in ContentDigest digest, string[] dependencies = null)
        {
            Utility.SAssert.Debug(!string.IsNullOrEmpty(packageName));
            if (!_cachedPackageMap.TryGetValue(packageName, out var packageIndex))
            {
                packageIndex = _packages.Add(new PackageState(packageName, type, digest, dependencies));
                _cachedPackageMap.Add(packageName, packageIndex);
            }
            else
            {
                Utility.SLogger.Warning("package already exists {0}", packageName);
            }
            return new(this, packageIndex);
        }

        public EntryInfo AddEntry(in PackageInfo packageInfo, string entryPath)
        {
            Utility.SAssert.Debug(packageInfo.isValid);
            return SetEntryPackage(AddEntry(entryPath), packageInfo);
        }

        public EntryInfo AddEntry(string entryPath)
        {
            if (TryGetEntryIndex(entryPath, out var entryIndex))
            {
                return new(this, entryIndex);
            }
            else
            {
                if (TryParseEntryPath(entryPath, out var entryDirectory, out var entryName))
                {
                    var directoryIndex = GetDirectoryIndex(entryDirectory);

                    entryIndex = _entries.Add(new(entryName, entryPath, directoryIndex, SIndex.None));
                    _cachedEntryMap.Add(entryPath, entryIndex);
                    return new(this, entryIndex);
                }
            }
            return new(this, SIndex.None);
        }

        private EntryInfo SetEntryPackage(in EntryInfo entryInfo, in PackageInfo packageInfo)
        {
            if (_entries.IsValidIndex(entryInfo.index))
            {
                ref var entryState = ref _entries.UnsafeGetValueByRef(entryInfo.index);
                if (entryState.package != packageInfo.index)
                {
                    Utility.SLogger.Debug("{0} changed package {1} => {2}",
                        entryState.entryPath,
                        GetPackageState(entryState.package), GetPackageState(packageInfo.index));
                    entryState.package = packageInfo.index;
                }
            }
            return entryInfo;
        }

        /// <summary>
        /// 通过名字查找匹配项
        /// </summary>
        public EntryInfo FindEntry(string entryName)
        {
            var e = _entries.GetStableIndexEnumerator();
            while (e.MoveNext())
            {
                if (e.Value.name == entryName)
                {
                    return new(this, e.Current);
                }
            }
            return new(this, SIndex.None);
        }

        public PackageInfo GetPackage(string packageName) => _cachedPackageMap.TryGetValue(packageName, out var packageIndex) ? new(this, packageIndex) : default;

        public EntryInfo GetEntry(string entryPath) => TryGetEntryIndex(entryPath, out var entryIndex) ? new(this, entryIndex) : default;

        public PackageInfo GetEntryPackage(string entryPath) => TryGetEntryState(entryPath, out var state) ? new(this, state.package) : default;

        public DirectoryInfo GetDirectory(string directoryPath) => new(this, GetDirectoryIndex(directoryPath));

        public DirectoryInfo RootDirectory => new(this, GetDirectoryIndex(string.Empty));

        public string[] GetPackageDependencies(string packageName)
        {
            if (_cachedPackageMap.TryGetValue(packageName, out var packageIndex))
            {
                if (_packages.TryGetValue(packageIndex, out var state))
                {
                    return state.dependencies;
                }
            }

            return default;
        }

        protected string GetPackageName(in SIndex packageIndex) => _packages.TryGetValue(packageIndex, out var state) ? state.name : default;

        protected EPackageType GetPackageType(in SIndex packageIndex) => _packages.TryGetValue(packageIndex, out var state) ? state.type : default;

        protected SIndex GetEntryPackageIndex(in SIndex entryIndex) => _entries.TryGetValue(entryIndex, out var state) ? state.package : default;

        protected SIndex GetEntryDirectoryIndex(in SIndex entryIndex) => _entries.TryGetValue(entryIndex, out var state) ? state.directory : default;

        protected bool IsValidPackage(in SIndex packageIndex) => _packages.IsValidIndex(packageIndex);

        protected bool IsValidDirectory(in SIndex directoryIndex) => _directories.IsValidIndex(directoryIndex);

        protected bool IsValidEntry(in SIndex entryIndex) => _entries.IsValidIndex(entryIndex);

        private SIndex GetDirectoryIndex(string directory)
        {
            if (string.IsNullOrEmpty(directory))
            {
                return _root;
            }

            if (directory[0] == PathSeparator)
            {
                throw new ArgumentException("invalid path, starting with slash is not allowed");
            }

            var lastIndex = 0;
            var nextIndex = directory.IndexOf(PathSeparator);
            var parentIndex = _root;

            while (nextIndex >= 0)
            {
                if (nextIndex == lastIndex + 1)
                {
                    throw new ArgumentException("invalid path, double slash is not allowed");
                }
                var childDirectory = directory.Substring(lastIndex, nextIndex - lastIndex);
                parentIndex = GetDirectoryIndex(parentIndex, childDirectory);
                lastIndex = nextIndex + 1;
                nextIndex = directory.IndexOf(PathSeparator, lastIndex);
            }

            return GetDirectoryIndex(parentIndex, directory.Substring(lastIndex));
        }

        private string GetDirectoryPath(in SIndex directoryIndex)
        {
            if (_directories.TryGetValue(directoryIndex, out var info))
            {
                if (info.parent.isValid)
                {
                    return GetDirectoryPath(info.parent) + PathSeparator + info.name;
                }

                return info.name;
            }

            throw new DirectoryNotFoundException(directoryIndex.ToString());
        }

        private SIndex GetDirectoryIndex(in SIndex parentIndex, string directoryName)
        {
            var e = _directories.GetStableIndexEnumerator();
            while (e.MoveNext())
            {
                var directoryInfo = e.Value;
                if (directoryInfo.parent == parentIndex && directoryInfo.name == directoryName)
                {
                    return e.Current;
                }
            }

            return _directories.Add(new DirectoryState(parentIndex, directoryName));
        }

        private PackageState GetPackageState(in SIndex packageIndex)
        {
            return _packages.TryGetValue(packageIndex, out var info) ? info : default;
        }

        private string GetEntryPath(in SIndex entryIndex)
        {
            if (_entries.TryGetValue(entryIndex, out var fileInfo))
            {
                return fileInfo.entryPath;
            }

            throw new FileNotFoundException();
        }

        private bool TryGetPackageState(in EntryState entryState, out PackageState packageState)
        {
            if (_packages.TryGetValue(entryState.directory, out packageState))
            {
                return true;
            }

            return false;
        }

        private bool TryGetEntryIndex(string entryPath, out SIndex entryIndex)
        {
            if (_cachedEntryMap.TryGetValue(entryPath, out entryIndex))
            {
                return true;
            }
            entryIndex = SIndex.None;
            return false;
        }

        private bool TryGetEntryState(string entryPath, out EntryState entryState)
        {
            if (TryGetEntryIndex(entryPath, out var entryIndex))
            {
                if (_entries.TryGetValue(entryIndex, out entryState))
                {
                    return true;
                }
            }

            entryState = default;
            return false;
        }

        private bool TryParseEntryPath(string entryPath, out string entryDirectory, out string entryName)
        {
            if (string.IsNullOrEmpty(entryPath))
            {
                entryDirectory = string.Empty;
                entryName = string.Empty;
                return false;
            }

            var lastIndex = entryPath.LastIndexOf(PathSeparator);
            if (lastIndex <= 0)
            {
                entryDirectory = string.Empty;
                entryName = entryPath;
                return true;
            }

            entryDirectory = entryPath.Substring(0, lastIndex);
            entryName = entryPath.Substring(lastIndex + 1);
            return true;
        }

        public void EnumerateEntries(Action<EntryInfo> fn)
        {
            var e = _entries.GetStableIndexEnumerator();
            while (e.MoveNext())
            {
                fn(new(this, e.Current));
            }
        }

        // 迭代所有包
        public void EnumeratePackages(Action<PackageInfo> fn)
        {
            var e = _packages.GetStableIndexEnumerator();
            while (e.MoveNext())
            {
                fn(new(this, e.Current));
            }
        }

#if UNITY_EDITOR
        public void SetPackageDigest(in PackageInfo packageInfo, in ContentDigest digest)
        {
            ref var state = ref _packages.UnsafeGetValueByRef(packageInfo.index);
            var newValue = new PackageState(state.name, state.type, digest, state.dependencies);
            _packages.UnsafeSetValue(packageInfo.index, newValue);
        }
#endif
    }
}
