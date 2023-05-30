using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Iris.ContentManagement.Editor
{
    using UnityEditor;

    public class PackagePostProcessor
    {
        [MenuItem("UnityFS/PostProcess Test")]
        private static void Test()
        {
            new PackagePostProcessor().Run();
        }

        private static byte[] GenerateBytes(string passphrase) => System.Security.Cryptography.MD5.Create().ComputeHash(System.Text.Encoding.UTF8.GetBytes(passphrase));

        private static async Task EncryptStream(string inPath, string outPath, Utility.ContentLibrary.PackageInfo packInfo, string passphrase)
        {
            using var outStream = FileUtils.OpenWrite(Path.Combine(outPath, packInfo.name));
            using var inStream = File.OpenRead(Path.Combine(inPath, packInfo.name));
            var key = GenerateBytes(passphrase + packInfo.name);
            var iv = GenerateBytes(passphrase + packInfo.digest);

            var transform = Utility.DefaultTransform.GetEncryptor(key, iv);
            var chunkSize = Utility.ChunkedStream.GetChunkSize(0);
            var buffer = new byte[chunkSize];
            var read = 0;

            while (true)
            {
                read = await inStream.ReadAsync(buffer, 0, buffer.Length);
                if (read <= 0)
                {
                    break;
                }

                var outBuffer = transform.Transform(buffer, 0, read);
                await outStream.WriteAsync(outBuffer, 0, outBuffer.Length);
            }
        }

        public void Run()
        {
            var inPath = "Build/Content/Staging/Win64";
            var outPath = "Build/Content/ArtifactsWin64";
            var passphrase = "hello";

            Directory.CreateDirectory(outPath);
            var lib = new Utility.ContentLibrary();
            using var libStream = File.OpenRead(Path.Combine(inPath, Utility.ContentLibrary.kFileName));
            var tasks = new List<Task>(lib.PackageCount);
            lib.Import(libStream);
            lib.EnumeratePackages(packInfo => tasks.Add(Task.Run(() => EncryptStream(inPath, outPath, packInfo, passphrase))));
            Task.WaitAll(tasks.ToArray());

            // update content library
            var libTasks = new List<Task<ContentDigest>>(lib.PackageCount);
            lib.EnumeratePackages(packInfo => libTasks.Add(Task.Run(() => UpdatePackageDigest(outPath, packInfo))));
            Task.WaitAll(libTasks.ToArray());
            using var outStream = FileUtils.OpenWrite(Path.Combine(outPath, Utility.ContentLibrary.kFileName));
            var index = 0;
            lib.EnumeratePackages(packInfo => lib.SetPackageDigest(packInfo, libTasks[index++].Result));
            lib.Export(outStream);
        }

        private async Task<ContentDigest> UpdatePackageDigest(string outPath, Utility.ContentLibrary.PackageInfo packageInfo)
        {
            using var stream = File.OpenRead(Path.Combine(outPath, packageInfo.name));
            var checksum = await Utility.Checksum.ComputeChecksumAsync(stream);
            var size = stream.Length;
            return new((uint)size, checksum);
        }
    }
}
