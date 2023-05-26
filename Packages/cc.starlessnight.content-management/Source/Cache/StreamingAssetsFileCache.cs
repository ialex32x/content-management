using System;
using System.IO;

namespace Iris.ContentManagement.Cache
{
    using UnityEngine;

    // 管理 StreamingAssets 内容加载 
    public class StreamingAssetsFileCache : IFileCache
    {
        public StreamingAssetsFileCache()
        {
            // 读取摘要记录 (可选, 没有的话就当场校验)
        }

        public virtual void Load()
        {

        }

        public virtual void Unload()
        {

        }

        public virtual Stream OpenRead(string filePath, in ContentDigest digest)
        {
            var path = Path.Combine(Application.streamingAssetsPath, filePath);
            if (!File.Exists(path))
            {
                return null;
            }
            return File.OpenRead(path);
        }

        public static StreamingAssetsFileCache Create()
        {
#if UNITY_ANDROID
            return new AndroidStreamingAssetsFileCache();
#else
            return new StreamingAssetsFileCache();
#endif
        }
    }

    public class AndroidStreamingAssetsFileCache : StreamingAssetsFileCache
    {
        private FileStream _fs;
        private ICSharpCode.SharpZipLib.Zip.ZipFile _zipFile;

        public AndroidStreamingAssetsFileCache()
        {
        }

        public override void Load()
        {
            // 读取摘要记录 (可选, 没有的话就当场校验)
            _fs = File.OpenRead(Application.dataPath);
            _zipFile = new ICSharpCode.SharpZipLib.Zip.ZipFile(_fs);
        }

        public override void Unload()
        {
            _zipFile.Close();
            _fs.Close();
        }

        public override Stream OpenRead(string filePath, in ContentDigest digest)
        {
            var path = "assets/" + filePath;
            if (_zipFile.FindEntry(path, true) < 0)
            {
                return null;
            }
            throw new NotImplementedException();
        }
    }
}
