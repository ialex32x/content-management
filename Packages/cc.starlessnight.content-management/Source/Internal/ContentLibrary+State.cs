using System;
using System.Collections.Generic;

namespace Iris.ContentManagement.Internal
{
    using Iris.ContentManagement.Utility;
    
    public partial class ContentLibrary
    {
        private readonly struct PackageState
        {
            public readonly string name;

            // nullable
            public readonly string[] dependencies;

            public readonly EPackageType type;

            // latest digest info
            public readonly ContentDigest digest;

            public PackageState(string name, EPackageType type, in ContentDigest digest, string[] dependencies)
            {
                this.name = name;
                this.type = type;
                this.digest = digest;
                this.dependencies = dependencies;
            }

            public override string ToString()
            {
                return string.IsNullOrEmpty(name) ? "None" : $"{name} ({type})";
            }
        }

        private readonly struct DirectoryState
        {
            public readonly SIndex parent;
            public readonly string name;

            public DirectoryState(SIndex parent, string name)
            {
                this.parent = parent;
                this.name = name;
            }
        }

        private struct EntryState
        {
            /// <summary>
            /// file name with extension
            /// </summary>
            public readonly string name;

            /// <summary>
            /// full path
            /// </summary>
            public readonly string entryPath;

            public readonly SIndex directory;

            // package ref
            public SIndex package;

            public EntryState(string name, string entryPath, in SIndex directory, in SIndex package)
            {
                this.name = name;
                this.entryPath = entryPath;
                this.directory = directory;
                this.package = package;
            }

            public override string ToString()
            {
                return entryPath;
            }
        }
    }
}
