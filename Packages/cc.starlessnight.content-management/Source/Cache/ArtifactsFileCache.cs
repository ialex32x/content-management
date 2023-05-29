using System.IO;

namespace Iris.ContentManagement.Cache
{
    // 用于直接使用打包结果模拟缓存
    internal class ArtifactsFileCache : IFileCache
    {
        private IFileSystem _fs;
        private Internal.ContentLibrary _embed;

        public ArtifactsFileCache()
        {
        }

        public void Bind(string path)
        {
            Utility.SAssert.Debug(!string.IsNullOrEmpty(path) && Directory.Exists(path));
            _fs = new Utility.OSFileSystem(path);
            _embed = new Internal.ContentLibrary();
            using var stream = _fs.OpenRead(Internal.ContentLibrary.kFileName);
            _embed.Import(stream);
        }

        public Stream OpenRead(string filePath, in ContentDigest digest)
        {
            if (_embed.GetPackage(filePath).digest == digest)
            {
                return _fs.OpenRead(filePath);
            }
            return null;
        }
    }
}