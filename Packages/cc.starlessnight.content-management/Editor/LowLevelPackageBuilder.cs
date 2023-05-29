using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Iris.ContentManagement.Editor
{
    using UnityEditor;

    public class LowLevelPackageBuilder
    {
        public interface IPackageSource
        {
            string name { get; }
            string[] entries { get; }
        }

        private class TestSource : IPackageSource
        {
            private string _name;
            private Func<string[]> _entries;

            public string name => _name;
            public string[] entries => _entries();

            public TestSource(string name, Func<string[]> entries) => (_name, _entries) = (name, entries);
        }

        private ContentManagementSettings _settings;

        private List<IPackageSource> _assetBundles = new();
        private List<IPackageSource> _zipArchives = new();
        private Iris.ContentManagement.Internal.ContentLibrary _lib = new();

        [MenuItem("UnityFS/LowLevel Package Test")]
        private static void RunTest()
        {
            new LowLevelPackageBuilder().Test();
        }

        private void Test()
        {
            _settings = new ContentManagementSettings();
            _settings.stagingPath = "Build/Content/Staging";
            _settings.fileExtension = ".pkg";
            AddAssetBundle("test_ab", () => new string[]
            {
                "Assets/Examples/Prefabs/Cube 1.prefab",
                "Assets/Examples/Prefabs/Cube 2.prefab",
                "Assets/Examples/Prefabs/Cube 3.prefab",
                "Assets/Examples/Prefabs/Cube 4.prefab",
                "Assets/Examples/Prefabs/Cube 5.prefab",
            });
            AddZipArchive("test_zip1", () => new string[]
            {
                "Assets/Examples/Config/test 1.txt",
                "Assets/Examples/Config/test 2.txt",
                "Assets/Examples/Config/test.txt",
            });
            AddZipArchive("test_zip2", () => new string[]
            {
                "Assets/Examples/Files/test1.json",
                "Assets/Examples/Files/test2.json",
                "MyTestContent/System.Xml.XmlSerializer.xml",
            });
            BuildAssetBundles();
            BuildZipArchives();
            BuildContentLibrary();
        }

        public void AddAssetBundle(string name, Func<string[]> entries) => _assetBundles.Add(new TestSource(name, entries));
        public void AddZipArchive(string name, Func<string[]> entries) => _zipArchives.Add(new TestSource(name, entries));

        private void BuildAssetBundles()
        {
            // generate assetbundle files
            var builds = new AssetBundleBuild[_assetBundles.Count];
            var outputPath = Path.Combine(_settings.stagingPath, "Win64");
            var options = BuildAssetBundleOptions.AssetBundleStripUnityVersion | BuildAssetBundleOptions.DeterministicAssetBundle | BuildAssetBundleOptions.ChunkBasedCompression;
            var target = BuildTarget.StandaloneWindows64;

            for (int packageIndex = 0, count = _assetBundles.Count; packageIndex < count; ++packageIndex)
            {
                var packageInfo = _assetBundles[packageIndex];
                var assetPaths = packageInfo.entries;
                var build = new AssetBundleBuild();
                build.assetBundleName = packageInfo.name + _settings.fileExtension;
                build.assetNames = assetPaths;
                build.addressableNames = assetPaths;
                builds[packageIndex++] = build;
            }
            Directory.CreateDirectory(outputPath);
            var manifest = BuildPipeline.BuildAssetBundles(outputPath, builds, options, target);

            // update content library
            for (int packageIndex = 0, count = _assetBundles.Count; packageIndex < count; ++packageIndex)
            {
                var packageInfo = _assetBundles[packageIndex];
                var deps = manifest.GetAllDependencies(packageInfo.name + _settings.fileExtension);
                using var fileStream = File.OpenRead(Path.Combine(outputPath, packageInfo.name + _settings.fileExtension));
                var checksum = Utility.Checksum.ComputeChecksum(fileStream);
                var digest = new ContentDigest((uint)fileStream.Length, checksum);
                var libPack = _lib.AddPackage(packageInfo.name + _settings.fileExtension, Internal.EPackageType.AssetBundle, digest, deps);
                foreach (var path in packageInfo.entries)
                {
                    _lib.AddEntry(libPack, path);
                }
            }
        }

        private void BuildZipArchives()
        {
            // generate zip files
            var dirPath = Path.Combine(_settings.stagingPath, "Win64");
            var packageCount = _zipArchives.Count;
            var tasks = new Task[packageCount];
            Directory.CreateDirectory(dirPath);
            for (var packageIndex = 0; packageIndex < packageCount; ++packageIndex)
            {
                var packageInfo = _zipArchives[packageIndex];
                var assetPaths = packageInfo.entries;
                var outputPath = Path.Combine(dirPath, packageInfo.name + _settings.fileExtension);
                tasks[packageIndex] = Task.Run(() => FileUtils.CreateZipArchive(outputPath, assetPaths));
            }
            Task.WaitAll(tasks);

            // update content library
            for (var packageIndex = 0; packageIndex < packageCount; ++packageIndex)
            {
                var packageInfo = _zipArchives[packageIndex];
                var outputPath = Path.Combine(dirPath, packageInfo.name + _settings.fileExtension);
                using var fileStream = File.OpenRead(outputPath);
                var checksum = Utility.Checksum.ComputeChecksum(fileStream);
                var digest = new ContentDigest((uint)fileStream.Length, checksum);
                var libPack = _lib.AddPackage(packageInfo.name + _settings.fileExtension, Internal.EPackageType.Zip, digest);
                foreach (var assetPath in packageInfo.entries)
                {
                    _lib.AddEntry(libPack, assetPath);
                }
            }
        }

        private void BuildContentLibrary()
        {
            var outPath = Path.Combine(_settings.stagingPath, "Win64", Internal.ContentLibrary.kFileName);
            using var outStream = FileUtils.OpenWrite(outPath);
            _lib.Export(outStream);
        }
    }
}