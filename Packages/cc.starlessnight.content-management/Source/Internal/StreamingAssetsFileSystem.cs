using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;

namespace Iris.ContentManagement.Internal
{
    using UnityEngine;

    // 管理 StreamingAssets 内容加载 
    public class StreamingAssetsFileSystem : IFileSystem
    {
        public StreamingAssetsFileSystem()
        {
            // 读取摘要记录 (可选, 没有的话就当场校验)
        }

        // not supported
        public bool DeleteFile(string filePath) => false;

        public virtual void Load()
        {

        }

        public virtual void Unload()
        {

        }

        public virtual bool Validate(string filePath, in ContentDigest digest)
        {
            throw new NotImplementedException();
        }

        public virtual bool Exists(string filePath)
        {
            var path = Path.Combine(Application.streamingAssetsPath, filePath);
            return File.Exists(path);
        }

        //TODO iris-content: android 借助zip进行读操作
        public virtual Stream OpenRead(string filePath)
        {
            throw new NotImplementedException();
        }

        // 不支持写操作
        public Stream OpenWrite(string filePath)
        {
            throw new NotSupportedException();
        }
    }

    public class AndroidStreamingAssetsFileSystem : StreamingAssetsFileSystem
    {
        private FileStream _fs;
        private ICSharpCode.SharpZipLib.Zip.ZipFile _zipFile;

        public AndroidStreamingAssetsFileSystem()
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

        public override bool Exists(string filePath)
        {
            var path = "assets/" + filePath;
            return _zipFile.FindEntry(path, true) >= 0;
        }

        public override bool Validate(string filePath, in ContentDigest digest)
        {
            throw new NotImplementedException();
        }

        public override Stream OpenRead(string filePath)
        {
            throw new NotImplementedException();
        }
    }
}
