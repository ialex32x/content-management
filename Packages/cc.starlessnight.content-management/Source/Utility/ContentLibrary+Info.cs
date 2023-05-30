using System;
using System.Collections.Generic;

namespace Iris.ContentManagement.Utility
{
    using EPackageType = Internal.EPackageType;
    public partial class ContentLibrary
    {
        public readonly struct PackageInfo : IEquatable<PackageInfo>
        {
            private readonly ContentLibrary _db;
            private readonly SIndex _index;

            public readonly string name => _db.GetPackageName(_index);

            public readonly bool isValid => _db.IsValidPackage(_index);

            public readonly EPackageType type => _db.GetPackageType(_index);

            internal readonly SIndex index => _index;

            public readonly ContentDigest digest => _db.GetPackageState(_index).digest;

            public readonly PackageInfo[] dependencies => GetDependencies();

            public readonly EntryInfo[] entries
            {
                get
                {
                    var list = new List<EntryInfo>();
                    EnumerateEntries(entry => list.Add(entry));
                    return list.ToArray();
                }
            }

            public PackageInfo(ContentLibrary db, SIndex index) => (this._db, this._index) = (db, index);

            public PackageInfo[] GetDependencies()
            {
#if IRIS_DIRECT_DEPS_ONLY
                var openSet = new List<PackageInfo>();
                var closeSet = new HashSet<PackageInfo>();

                openSet.Add(this);
                while (openSet.Count != 0)
                {
                    var first = openSet[openSet.Count - 1];
                    openSet.RemoveAt(openSet.Count - 1);
                    closeSet.Add(first);

                    var dependencies = _db.GetPackageDependencies(first);
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
                closeSet.Remove(this);

                var results = new PackageInfo[closeSet.Count];
                closeSet.CopyTo(results, 0);
                return results;
#else
                return _db.GetPackageDependencies(this);
#endif
            }

            // 迭代包中的所有文件
            public void EnumerateEntries(Action<EntryInfo> fn)
            {
                if (!isValid)
                {
                    return;
                }

                var e = _db._entries.GetStableIndexEnumerator();
                while (e.MoveNext())
                {
                    if (e.Value.package == _index)
                    {
                        fn(new(_db, e.Current));
                    }
                }
            }

            public int GetEntriesCount()
            {
                if (!isValid)
                {
                    return 0;
                }

                var n = 0;
                var e = _db._entries.GetStableIndexEnumerator();
                while (e.MoveNext())
                {
                    if (e.Value.package == _index)
                    {
                        ++n;
                    }
                }
                return n;
            }

            public bool Equals(in PackageInfo other) => this == other;

            public bool Equals(PackageInfo other) => this == other;

            public override bool Equals(object obj) => obj is PackageInfo other && this == other;

            public override int GetHashCode() => (int)(_db.GetHashCode() ^ _index.GetHashCode());

            public override string ToString() => $"{name} ({type})";

            public static bool operator ==(PackageInfo a, PackageInfo b) => a._db == b._db && a._index == b._index;

            public static bool operator !=(PackageInfo a, PackageInfo b) => a._db != b._db || a._index != b._index;
        }

        public readonly struct DirectoryInfo : IEquatable<DirectoryInfo>
        {
            private readonly ContentLibrary _db;
            private readonly SIndex _index;

            public readonly bool isValid => _db.IsValidDirectory(_index);

            public readonly string name => _db._directories.TryGetValue(_index, out var state) ? state.name : default;

            /// <summary>
            /// all children entries in this directory
            /// </summary>
            public readonly EntryInfo[] entries
            {
                get
                {
                    var list = new List<EntryInfo>();
                    EnumerateEntries(entry => list.Add(entry));
                    return list.ToArray();
                }
            }

            /// <summary>
            /// parent directory
            /// </summary>
            public readonly DirectoryInfo parent => _db && _db._directories.TryGetValue(_index, out var state) ? new(_db, state.parent) : default;

            /// <summary>
            /// all sub-directoreis in this directory
            /// </summary>
            public readonly DirectoryInfo[] directories
            {
                get
                {
                    var list = new List<DirectoryInfo>();
                    EnumerateDirectories(directory => list.Add(directory));
                    return list.ToArray();
                }
            }

            public DirectoryInfo(ContentLibrary db, SIndex index) => (this._db, this._index) = (db, index);

            // 迭代目录下的文件
            public void EnumerateEntries(Action<EntryInfo> fn)
            {
                if (!isValid)
                {
                    return;
                }

                var e = _db._entries.GetStableIndexEnumerator();
                while (e.MoveNext())
                {
                    if (e.Value.directory == _index)
                    {
                        fn(new(_db, e.Current));
                    }
                }
            }

            public EntryInfo FindEntry(string entryName) => FindEntry(entryInfo => entryInfo.name == entryName);

            public EntryInfo FindEntry(Func<EntryInfo, bool> filter)
            {
                if (isValid)
                {
                    var e = _db._entries.GetStableIndexEnumerator();
                    while (e.MoveNext())
                    {
                        if (e.Value.directory != _index)
                        {
                            continue;
                        }
                        var entry = new EntryInfo(_db, e.Current);
                        if (filter(entry))
                        {
                            return entry;
                        }
                    }
                }
                return new(_db, SIndex.None);
            }

            // 迭代目录下的子目录
            public void EnumerateDirectories(Action<DirectoryInfo> fn)
            {
                if (!isValid)
                {
                    return;
                }

                var e = _db._directories.GetStableIndexEnumerator();
                while (e.MoveNext())
                {
                    if (e.Value.parent == _index)
                    {
                        fn(new(_db, e.Current));
                    }
                }
            }

            public DirectoryInfo FindDirectory(string directoryName) => FindDirectory(directoryInfo => directoryInfo.name == directoryName);

            public DirectoryInfo FindDirectory(Func<DirectoryInfo, bool> filter)
            {
                if (isValid)
                {
                    var e = _db._directories.GetStableIndexEnumerator();
                    while (e.MoveNext())
                    {
                        if (e.Value.parent != _index)
                        {
                            continue;
                        }
                        var directoryInfo = new DirectoryInfo(_db, e.Current);
                        if (filter(directoryInfo))
                        {
                            return directoryInfo;
                        }
                    }
                }
                return new(_db, SIndex.None);
            }

            public bool Equals(in DirectoryInfo other) => this == other;

            public bool Equals(DirectoryInfo other) => this == other;

            public override bool Equals(object obj) => obj is DirectoryInfo other && this == other;

            public override int GetHashCode() => (int)(_db.GetHashCode() ^ _index.GetHashCode());

            public override string ToString() => _db.GetDirectoryPath(_index);

            public static bool operator ==(DirectoryInfo a, DirectoryInfo b) => a._db == b._db && a._index == b._index;

            public static bool operator !=(DirectoryInfo a, DirectoryInfo b) => a._db != b._db || a._index != b._index;
        }

        public readonly struct EntryInfo : IEquatable<EntryInfo>
        {
            private readonly ContentLibrary _db;
            private readonly SIndex _index;

            public readonly bool isValid => _db && _db.IsValidEntry(_index);

            // file name with extension
            public readonly string name => _db && _db._entries.TryGetValue(_index, out var state) ? state.name : default;

            // full path
            public readonly string fullName => _db && _db._entries.TryGetValue(_index, out var state) ? state.entryPath : default;

            public readonly SIndex index => _index;

            public readonly PackageInfo package => _db ? new(_db, _db.GetEntryPackageIndex(_index)) : default;

            public readonly DirectoryInfo directory => _db ? new(_db, _db.GetEntryDirectoryIndex(_index)) : default;

            public EntryInfo(ContentLibrary db, SIndex index) => (this._db, this._index) = (db, index);

            public bool Equals(in EntryInfo other) => this == other;

            public bool Equals(EntryInfo other) => this == other;

            public override bool Equals(object obj) => obj is EntryInfo other && this == other;

            public override int GetHashCode() => _db ? (int)(_db.GetHashCode() ^ _index.GetHashCode()) : _index.GetHashCode();

            public override string ToString() => _db ? _db.GetEntryPath(_index) : "null";

            public static bool operator ==(EntryInfo a, EntryInfo b) => a._db == b._db && a._index == b._index;

            public static bool operator !=(EntryInfo a, EntryInfo b) => a._db != b._db || a._index != b._index;
        }
    }
}
