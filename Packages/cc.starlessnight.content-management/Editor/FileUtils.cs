using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Iris.ContentManagement.Editor
{
    public static class FileUtils
    {
        public static FileStream OpenWrite(string outputPath)
        {
            var fileStream = File.Open(outputPath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Write);
            fileStream.SetLength(0);
            return fileStream;
        }

        public static async Task CreateZipArchive(string outputPath, string[] assetPaths)
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
