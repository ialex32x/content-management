using System;
using System.IO;
using System.Collections.Generic;

namespace Iris.ContentManagement.Cache
{
    public interface IFileCache
    {
        Stream OpenRead(string filePath, in ContentDigest digest);
    }

    public class FileCacheCollection : IFileCache
    {
        private List<IFileCache> _caches = new();

        public FileCacheCollection(params IFileCache[] caches)
        {
            _caches.AddRange(caches);
        }

        public Stream OpenRead(string filePath, in ContentDigest digest)
        {
            for (int i = 0, n = _caches.Count; i < n; ++i)
            {
                var cache = _caches[i];
                try
                {
                    var stream = cache.OpenRead(filePath, digest);
                    if (stream != null)
                    {
                        return stream;
                    }
                }
                catch (Exception exception)
                {
                    Utility.SLogger.Exception(exception,
                        "can't read: {0} #{1} {2}", nameof(FileCacheCollection), i, filePath);
                }
            }

            return default;
        }
    }
}
