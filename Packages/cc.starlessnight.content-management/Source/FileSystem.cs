using System;
using System.IO;

namespace Iris.ContentManagement
{
    // abstract file system
    // 提供基于文件的访问
    public interface IFileSystem
    {
        void Load();
        void Unload();

        bool Exists(string filePath);
        bool DeleteFile(string filePath);
        
        Stream OpenRead(string filePath);
        Stream OpenWrite(string filePath);
    }
}
