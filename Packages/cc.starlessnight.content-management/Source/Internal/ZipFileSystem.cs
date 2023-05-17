using System;
using System.IO;

namespace Iris.ContentManagement.Internal
{
    using ICSharpCode.SharpZipLib.Zip;

    public class ZipFileSystem : IFileSystem
    {
        private Stream _stream;
        private ZipFile _zipFile;

        public ZipFileSystem(Stream stream)
        {
            _stream = stream;
        }

        public void Load()
        {
            _zipFile = new ZipFile(_stream);
        }

        public void Unload()
        {
            _zipFile.Close();
            _stream.Close();
        }

        public bool Exists(string filePath)
        {
            return _zipFile.FindEntry(filePath, false) >= 0;
        }
        
        // not supported
        public bool DeleteFile(string filePath) => false;
        
        public Stream OpenRead(string filePath) => throw new NotImplementedException();

        public Stream OpenWrite(string filePath) => throw new NotImplementedException();
    }
}
