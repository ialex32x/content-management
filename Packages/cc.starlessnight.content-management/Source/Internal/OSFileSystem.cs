using System;
using System.IO;

namespace Iris.ContentManagement.Internal
{
    public class OSFileSystem : IFileSystem
    {
        private string _basePath;

        public OSFileSystem(string basePath = "")
        {
            _basePath = basePath;
        }

        public void Load()
        {

        }

        public void Unload()
        {

        }

        public bool Exists(string filePath)
        {
            var osFilePath = GetOSFilePath(filePath);
            return File.Exists(osFilePath);
        }

        public bool DeleteFile(string filePath)
        {
            var osFilePath = GetOSFilePath(filePath);
            try { File.Delete(osFilePath); }
            catch (Exception exception)
            {
                Utility.Logger.Exception(exception, "{0} failed to delete file", nameof(OSFileSystem));
            }
            return true;
        }

        public Stream OpenRead(string filePath)
        {
            var osFilePath = GetOSFilePath(filePath);
            return File.Open(osFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        }

        public Stream OpenWrite(string filePath)
        {
            var osFilePath = GetOSFilePath(filePath);
            if (!File.Exists(osFilePath))
            {
                EnsureFileDirectory(osFilePath);
                return File.Create(osFilePath);
            }
            else
            {
                return File.Open(osFilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            }
        }

        private string GetOSFilePath(string filePath)
        {
            return string.IsNullOrEmpty(_basePath) ? filePath : Path.Combine(_basePath, filePath);
        }

        private void EnsureFileDirectory(string filePath)
        {
            var index = filePath.LastIndexOf('/');
            if (index < 0)
            {
                index = filePath.LastIndexOf('\\');
                if (index < 0)
                {
                    return;
                }
            }

            var directoryPath = filePath.Substring(0, index);
            if (directoryPath.Length > 0 && !Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }
        }
    }
}
