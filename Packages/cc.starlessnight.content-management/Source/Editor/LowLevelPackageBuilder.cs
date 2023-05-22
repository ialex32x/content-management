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

        [MenuItem("UnityFS/Test")]
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
            });
            BuildAssetBundles();
            BuildZipArchives();
        }

        public void AddAssetBundle(string name, Func<string[]> entries) => _assetBundles.Add(new TestSource(name, entries));
        public void AddZipArchive(string name, Func<string[]> entries) => _zipArchives.Add(new TestSource(name, entries));

        private void BuildAssetBundles()
        {
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
            BuildPipeline.BuildAssetBundles(outputPath, builds, options, target);
            //TODO encrypt source into the next stage 
            //TODO calculate checksum
        }

        private void BuildZipArchives()
        {
            // generate zip files
            var dirPath = Path.Combine(_settings.stagingPath, "Win64");
            var packageCount = _zipArchives.Count;
            var tasks = new Task[packageCount];
            for (var packageIndex = 0; packageIndex < packageCount; ++packageIndex)
            {
                var packageInfo = _zipArchives[packageIndex];
                var assetPaths = packageInfo.entries;
                var outputPath = Path.Combine(dirPath, packageInfo.name + _settings.fileExtension);
                tasks[packageIndex] = Task.Run(() => WriteZipArchive(outputPath, assetPaths));
            }
            Task.WaitAll(tasks);
            //TODO encrypt source into the next stage 
            //TODO calculate checksum
        }

        private FileStream OpenWrite(string outputPath)
        {
            var fileStream = File.Open(outputPath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Write);
            fileStream.SetLength(0);
            return fileStream;
        }

        private async Task WriteZipArchive(string outputPath, string[] assetPaths)
        {
            using var zipStream = new ICSharpCode.SharpZipLib.Zip.ZipOutputStream(OpenWrite(outputPath));
            zipStream.IsStreamOwner = true;
            foreach (var assetPath in assetPaths)
            {
                var fileInfo = new FileInfo(assetPath);
                var name = ICSharpCode.SharpZipLib.Zip.ZipEntry.CleanName(assetPath);
                var entry = new ICSharpCode.SharpZipLib.Zip.ZipEntry(name) { DateTime = fileInfo.LastWriteTimeUtc, Size = fileInfo.Length };
                zipStream.PutNextEntry(entry);
                using var sourceStream = fileInfo.OpenRead();
                const int BufSize = 1024 * 4;
                var buf = new byte[BufSize];
                do
                {
                    var read = await sourceStream.ReadAsync(buf, 0, BufSize);
                    if (read <= 0)
                    {
                        break;
                    }
                    zipStream.Write(buf, 0, read);
                } while (true);
                zipStream.CloseEntry();
            }
        }
    }
}