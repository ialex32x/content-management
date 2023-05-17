using System;
using System.IO;
using System.Collections.Generic;

namespace Iris.ContentManagement.Internal
{
    using Iris.ContentManagement.Utility;

    public partial class ContentLibrary
    {
#if UNITY_EDITOR
        public void Export(Stream stream)
        {
            using var writer = new System.IO.StreamWriter(stream, System.Text.Encoding.UTF8, 1024, true);
            var remap = new Dictionary<SIndex, HashSet<SIndex>>(_packages.Count);

            ExportMeta(writer);
            CollectPackages(remap);
            foreach (var kv in remap)
            {
                if (kv.Value.Count != 0)
                {
                    ref readonly var package = ref _packages.UnsafeGetValueByRef(kv.Key);
                    ExportPackage(writer, package, kv.Value);
                }
            }
            stream.SetLength(stream.Position);
        }

        private void ExportMeta(StreamWriter writer)
        {
            writer.WriteLine("@meta");
            writer.WriteLine("1"); // version
            writer.WriteLine();
        }

        private void ExportPackage(StreamWriter writer, in PackageState package, HashSet<SIndex> entrySet)
        {
            switch (package.type)
            {
                case EPackageType.AssetBundle:
                    writer.WriteLine("@assetbundle");
                    writer.WriteLine("{0},{1},{2}", package.digest.size, package.digest.checksum, package.name);
                    var n = package.dependencies != null ? package.dependencies.Length : 0;
                    if (n > 0)
                    {
                        for (var i = 0; i < n; ++i)
                        {
                            writer.WriteLine(package.dependencies[i]);
                        }
                    }
                    writer.WriteLine("@entries");
                    foreach (var index in entrySet)
                    {
                        ref readonly var entry = ref _entries.UnsafeGetValueByRef(index);
                        writer.WriteLine(entry.entryPath);
                    }
                    writer.WriteLine("@end");
                    writer.WriteLine();
                    break;
                default: throw new NotSupportedException();
            }
        }
#endif // UNITY_EDITOR

        public void Import(Stream stream)
        {
            using var reader = new System.IO.StreamReader(stream, System.Text.Encoding.UTF8, false, 1024, true);

            ImportMeta(reader);
            while (!reader.EndOfStream)
            {
                var line = reader.ReadLine();
                switch (line)
                {
                    case "@assetbundle": ImportPackage(reader, EPackageType.AssetBundle); break;
                    default: break;
                }
            }
        }

        private void ImportMeta(StreamReader reader)
        {
            do
            {
                var line = reader.ReadLine();
                if (line == "@meta")
                {
                    break;
                }
            } while (true);
            var version = reader.ReadLine();
            Utility.Logger.Debug("import contentlibrary v{0}", version);
        }

        private void ImportPackage(StreamReader reader, EPackageType type)
        {
            var head = reader.ReadLine().Split(',');
            var size = uint.Parse(head[0]);
            var checksum = new Utility.Checksum(ushort.Parse(head[1]));
            var name = head[2];
            var dependencies = new List<string>();
            do
            {
                var line = reader.ReadLine();
                if (line == "@entries")
                {
                    break;
                }
                dependencies.Add(line);
            } while (true);
            var packageInfo = AddPackage(name, type, new ContentDigest(size, checksum), dependencies.Count != 0 ? dependencies.ToArray() : null);
            do
            {
                var line = reader.ReadLine();
                if (line == "@end")
                {
                    break;
                }
                AddEntry(packageInfo, line);
            } while (true);
        }
    }
}