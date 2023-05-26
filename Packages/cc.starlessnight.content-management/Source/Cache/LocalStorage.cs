using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;

namespace Iris.ContentManagement.Cache
{
    using Iris.ContentManagement.Utility;
    using Stream = System.IO.Stream;
    using SeekOrigin = System.IO.SeekOrigin;

    /// <summary>
    /// 可读写本地缓存
    /// </summary>
    public class LocalStorage : IFileCache
    {
        public const string CacheFileName = "cache.pkg";

        private bool _dirty = false;
        private readonly string _rootPath;
        private readonly string _extension;
        private readonly IFileSystem _fileSystem;
        private readonly ReaderWriterLockSlim _lock = new();

        private readonly SList<SFileInfo> _files = new();

        // alias => store(filename+digest)
        private readonly Dictionary<string, SIndex> _mappings = new();

        /// <summary>
        /// 缓存信息中的文件大小总和
        /// </summary>
        public long totalSize => GetFastTotalSize();

        public LocalStorage() : this(new Utility.OSFileSystem("LocalStorage"))
        {
        }

        public LocalStorage(IFileSystem fileSystem, string extension = ".pak")
        {
            _fileSystem = fileSystem;
            _extension = extension;
            LoadState();
        }

        public static bool TryParseState(string line, out string entryName, out string fileName, out ContentDigest digest)
        {
            entryName = fileName = null;
            digest = default;

            var i0 = line.IndexOf(',');
            if (i0 <= 0) return false;
            var i1 = line.IndexOf(',', i0 + 1);
            if (i1 <= i0) return false;
            var i2 = line.IndexOf(',', i1 + 1);
            if (i2 <= i1) return false;
            if (!uint.TryParse(line.Substring(i1 + 1, i2 - i1 - 1), out var size) || !Utility.Checksum.TryParse(line.Substring(i2 + 1, line.Length - i2 - 1), out var checksum))
            {
                return false;
            }
            entryName = line.Substring(0, i0);
            fileName = line.Substring(i0 + 1, i1 - i0 - 1);
            digest = new(size, checksum);
            return true;
        }

        public static string ToStateString(string entryName, string fileName, in ContentDigest digest)
        {
            return string.Format("{0},{1},{2},{3}", entryName, fileName, digest.size, digest.checksum);
        }

        public void LoadState()
        {
            if (_fileSystem.Exists(CacheFileName))
            {
                using var reader = new System.IO.StreamReader(_fileSystem.OpenRead(CacheFileName), System.Text.Encoding.UTF8);
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    if (!TryParseState(line, out var entryName, out var fileName, out var digest))
                    {
                        Utility.SLogger.Warning("unable to parse storage entry: {0}", line);
                        continue;
                    }
                    if (!_mappings.TryGetValue(entryName, out var index))
                    {
                        if (!_fileSystem.Exists(fileName))
                        {
                            Utility.SLogger.Warning("not existed storage entry: {0}", line);
                            continue;
                        }
                        index = _files.Add(new(this, fileName, digest));
                        _mappings.Add(entryName, index);
                        Utility.SLogger.Debug("load storage entry {0}, {1}, {2}", entryName, fileName, digest);
                    }
                }
            }
        }

        public void SaveState()
        {
            try
            {
                _lock.EnterUpgradeableReadLock();
                if (!_dirty)
                {
                    return;
                }

                try
                {
                    _lock.EnterWriteLock();
                    using var writer = new System.IO.StreamWriter(_fileSystem.OpenWrite(CacheFileName), System.Text.Encoding.UTF8);
                    foreach (var map in _mappings)
                    {
                        if (_files.TryGetValue(map.Value, out var info))
                        {
                            var entryName = map.Key;
                            var line = ToStateString(entryName, info.fileName, info.digest);
                            if (!string.IsNullOrEmpty(line))
                            {
                                writer.WriteLine(line);
                                Utility.SLogger.Debug("save storage entry {0}", line);
                            }
                        }
                    }
                    writer.BaseStream.SetLength(writer.BaseStream.Position);
                    _dirty = false;
                }
                catch (Exception exception)
                {
                    Utility.SLogger.Exception(exception, "failed to save local storage state");
                }
                finally
                {
                    _lock.ExitWriteLock();
                }
            }
            finally
            {
                _lock.ExitUpgradeableReadLock();
            }
        }

        /// <summary>
        /// [threading] 强制关闭所有文件
        /// </summary>
        public void Shutdown()
        {
            SaveState();
            try
            {
                _lock.EnterWriteLock();
                // 强制关闭所有文件
                while (_files.TryRemoveAt(0, out var handle))
                {
                    var n = handle.CloseAnyway();
                    if (n != 0)
                    {
                        Utility.SLogger.Warning("cleaning up open stream: {0}", handle.fileName);
                    }
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        private long GetFastTotalSize()
        {
            try
            {
                _lock.EnterReadLock();
                long size = 0;
                foreach (var file in _files)
                {
                    size += file.digest.size;
                }
                return size;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        // [threading]
        private bool IsValidStream(in SIndex index, Stream stream)
        {
            try
            {
                _lock.EnterReadLock();
                if (!_files.IsValidIndex(index))
                {
                    return false;
                }
                ref var file = ref _files.UnsafeGetValueByRef(index);
                return file.IsValidStream(stream);
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        /// <summary>
        /// 分配唯一文件名用于写入
        /// </summary>
        private string GetFileName() => Guid.NewGuid().ToString("N") + _extension;

        public Stream OpenWrite(string entryName)
        {
            try
            {
                _lock.EnterWriteLock();
                _dirty = true;
                if (_mappings.TryGetValue(entryName, out var index))
                {
                    ref var file = ref _files.UnsafeGetValueByRef(index);
                    return new WriterStream(this, index, entryName, file.BeginWrite());
                }
                else
                {
                    index = _files.Add(new(this, GetFileName(), new()));
                    _mappings.Add(entryName, index);
                    ref var file = ref _files.UnsafeGetValueByRef(index);
                    return new WriterStream(this, index, entryName, file.BeginWrite());
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        private void CloseWrite(in SIndex fileIndex, Stream stream, Utility.Checksum checksum)
        {
            try
            {
                _lock.EnterWriteLock();
                _dirty = true;
                ref var state = ref _files.UnsafeGetValueByRef(fileIndex);
                state.EndWrite(stream, checksum);
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public bool DeleteFile(string entryName)
        {
            try
            {
                _lock.EnterWriteLock();
                if (!_mappings.TryGetValue(entryName, out var index) || !_files.IsValidIndex(index))
                {
                    return false;
                }
                ref var file = ref _files.UnsafeGetValueByRef(index);
                if (file.isWriting)
                {
                    Utility.SLogger.Error("can not delete file which is writing {0}", file.fileName);
                    return false;
                }
                _dirty = true;
                _files.RemoveAt(index);
                _mappings.Remove(entryName);
                file.CloseAnyway();
                Utility.SLogger.Debug("delete file {0}", file.fileName);
                if (!_fileSystem.DeleteFile(file.fileName))
                {
                    return false;
                }
                return true;
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public bool Exists(string entryName) => Exists(entryName, new(), false);

        public bool Exists(string entryName, in ContentDigest digest) => Exists(entryName, digest, true);

        public bool Exists(string entryName, in ContentDigest digest, bool check)
        {
            try
            {
                _lock.EnterReadLock();
                if (!_mappings.TryGetValue(entryName, out var index) || !_files.IsValidIndex(index))
                {
                    return false;
                }
                ref var file = ref _files.UnsafeGetValueByRef(index);
                if (check && file.digest != digest)
                {
                    Utility.SLogger.Error("can not read corrupted file {0} {1} != {2}", file.fileName, file.digest, digest);
                    return false;
                }
                if (!_fileSystem.Exists(file.fileName))
                {
                    return false;
                }
                return true;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        /// <summary>
        /// [threading] 检查文件是否有效 （且不在写入状态）
        /// </summary>
        public bool IsFileValid(string name, in ContentDigest digest)
        {
            try
            {
                _lock.EnterReadLock();
                if (!_mappings.TryGetValue(name, out var index) || !_files.IsValidIndex(index))
                {
                    return false;
                }
                ref var file = ref _files.UnsafeGetValueByRef(index);
                // Utility.Logger.Info("check {0} {1} ?? {2}", file.fileName, file.digest, digest);
                return !file.isWriting && file.digest == digest;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        // 检验文件是否损坏, 损坏的文件将被删除
        public bool VerifyFile(string entryName, in ContentDigest digest)
        {
            if (Exists(entryName, digest))
            {
                return true;
            }
            DeleteFile(entryName);
            return false;
        }

        // 打开文件读取流 (不会检查有效性)
        public Stream OpenRead(string entryName) => OpenRead(entryName, new(), false);

        // 打开文件读取流 (检查有效性)
        public Stream OpenRead(string entryName, in ContentDigest digest) => OpenRead(entryName, digest, true);

        // 打开文件读取流
        public Stream OpenRead(string entryName, in ContentDigest digest, bool check)
        {
            try
            {
                _lock.EnterWriteLock();
                if (!_mappings.TryGetValue(entryName, out var index) || !_files.IsValidIndex(index))
                {
                    return default;
                }
                ref var file = ref _files.UnsafeGetValueByRef(index);
                if (check && file.digest != digest)
                {
                    Utility.SLogger.Error("can not read corrupted file {0} {1} != {2}", file.fileName, file.digest, digest);
                    return default;
                }
                if (file.isWriting)
                {
                    Utility.SLogger.Error("can not read file which is writing {0}", file.fileName);
                    return default;
                }
                var stream = file.BeginRead();
                if (stream == null)
                {
                    Utility.SLogger.Error("can not open to read {0}", file.fileName);
                    return default;
                }
                return new ReaderStream(this, index, stream);
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        internal void CloseAnyway(string entryName)
        {
            try
            {
                _lock.EnterWriteLock();
                if (!_mappings.TryGetValue(entryName, out var index) || !_files.IsValidIndex(index))
                {
                    return;
                }
                ref var file = ref _files.UnsafeGetValueByRef(index);
                file.CloseAnyway();
                Utility.SLogger.Debug("close file anyway {0}", file.fileName);
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        private void CloseRead(in SIndex fileIndex, Stream stream)
        {
            try
            {
                _lock.EnterWriteLock();
                ref var state = ref _files.UnsafeGetValueByRef(fileIndex);
                state.EndRead(stream);
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public class WriterStream : Stream
        {
            private readonly LocalStorage _storage;
            private readonly string _entryName;
            // file index
            private readonly SIndex _index;
            // 校验值是否已与 stream 内容保持同步
            private bool _synced;
            // 当前已写入内容的校验值
            private Utility.Checksum _checksum;

            private Stream _stream;

            // 当前写入流是否有效
            public bool isValid => _stream != null && _storage != null && _storage.IsValidStream(_index, _stream);

            public WriterStream(LocalStorage storage, in SIndex index, string entryName, Stream stream)
            {
                _storage = storage;
                _index = index;
                _entryName = entryName;
                _stream = stream;
                _synced = _stream.Length == 0;
                _checksum = new();
            }

            // [threading] 强制同步校验值
            private void FlushChecksum()
            {
                if (_synced)
                {
                    return;
                }
                _synced = true;
                if (_stream.Length > 0)
                {
                    var pos = _stream.Position;
                    _stream.Seek(0, SeekOrigin.Begin);
                    _checksum = Utility.Checksum.ComputeChecksum(_stream);
                    _stream.Position = pos > _stream.Length ? _stream.Length : pos;
                    Utility.SLogger.Info("check content in cache {0} {1} ({2})", _entryName, _stream.Length, _checksum);
                }
                else
                {
                    _checksum = new();
                }
            }

            #region Stream Implementation
            public override bool CanRead => _stream.CanRead;

            public override bool CanSeek => _stream.CanSeek;

            public override bool CanWrite => isValid && _stream.CanWrite;

            // threading
            public override long Length => _stream.Length;

            public override long Position { get => _stream.Position; set => _stream.Position = value; }

            public override void Flush() => _stream.Flush();

            public override int Read(byte[] buffer, int offset, int count) => _stream.Read(buffer, offset, count);

            public override long Seek(long offset, SeekOrigin origin) => _stream.Seek(offset, origin);

            public override void SetLength(long value)
            {
                if (_stream.Length == value)
                {
                    return;
                }
                _stream.SetLength(value);
                if (value == 0)
                {
                    _synced = true;
                    _checksum = new();
                }
                else
                {
                    _synced = false;
                }
            }

            // threading
            public override void Write(byte[] buffer, int offset, int count)
            {
                var isAppending = _stream.Position == _stream.Length;
                _stream.Write(buffer, offset, count);
                // 中途写入, 只做标记
                if (!isAppending)
                {
                    _synced = false;
                    return;
                }
                // 已同步的情况下在末尾写入, 只计算增量
                if (_synced)
                {
                    _checksum = Utility.Checksum.ComputeChecksum(buffer, offset, count, _checksum);
                    return;
                }
                FlushChecksum();
            }

            // threading
            public override void Close()
            {
                if (!isValid)
                {
                    return;
                }
                FlushChecksum();
                _storage.CloseWrite(_index, _stream, _checksum);
                _stream = null;
            }
            #endregion
        }

        public class ReaderStream : Stream
        {
            private readonly LocalStorage _storage;
            // file index
            private readonly SIndex _index;

            private Stream _stream;

            public bool isValid => _storage != null && _storage.IsValidStream(_index, _stream);

            public ReaderStream(LocalStorage storage, in SIndex index, Stream stream)
            {
                _storage = storage;
                _index = index;
                _stream = stream;
            }

            /// <summary>
            /// 强制关闭此读取流
            /// </summary>
            public override void Close()
            {
                if (isValid)
                {
                    _storage.CloseRead(_index, _stream);
                    _stream = default;
                }
            }

            public override long Position { get => _stream.Position; set => _stream.Position = value; }
            public override long Length => _stream.Length;
            public override bool CanWrite => _stream.CanWrite;
            public override bool CanTimeout => _stream.CanTimeout;
            public override bool CanSeek => _stream.CanSeek;
            public override bool CanRead => _stream.CanRead;
            public override int ReadTimeout { get => _stream.ReadTimeout; set => _stream.ReadTimeout = value; }
            public override int WriteTimeout { get => _stream.WriteTimeout; set => _stream.WriteTimeout = value; }

            public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state) => _stream.BeginRead(buffer, offset, count, callback, state);
            public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state) => throw new NotSupportedException();
            public override void CopyTo(Stream destination, int bufferSize) => _stream.CopyTo(destination, bufferSize);
            public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken) => _stream.CopyToAsync(destination, bufferSize, cancellationToken);
            public override int EndRead(IAsyncResult asyncResult) => _stream.EndRead(asyncResult);
            public override void EndWrite(IAsyncResult asyncResult) => throw new NotSupportedException();
            public override Task FlushAsync(CancellationToken cancellationToken) => _stream.FlushAsync(cancellationToken);
            public override int Read(Span<byte> buffer) => _stream.Read(buffer);
            public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => _stream.ReadAsync(buffer, cancellationToken);
            public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => _stream.ReadAsync(buffer, offset, count, cancellationToken);
            public override int ReadByte() => _stream.ReadByte();
            public override int Read(byte[] buffer, int offset, int count) => _stream.Read(buffer, offset, count);
            public override void Flush() => throw new NotSupportedException();
            public override long Seek(long offset, SeekOrigin origin) => _stream.Seek(offset, origin);
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
            public override void Write(ReadOnlySpan<byte> buffer) => throw new NotSupportedException();
            public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => throw new NotSupportedException();
            public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public override void WriteByte(byte value) => throw new NotSupportedException();
            public override ValueTask DisposeAsync() => throw new NotSupportedException();
            protected override void Dispose(bool disposing) => Close();
        }

        private struct SFileInfo
        {
            private readonly LocalStorage _storage;

            private readonly string _fileName;

            private ContentDigest _digest;

            private bool _isWriting;
            private List<Stream> _streams;

            public readonly bool isWriting => _isWriting;

            public readonly ContentDigest digest => _digest;

            /// <summary>
            /// 实际存储文件的名字
            /// </summary>
            public readonly string fileName => _fileName;

            public SFileInfo(LocalStorage storage, string name, ContentDigest digest)
            {
                this._storage = storage;
                this._fileName = name;
                this._digest = digest;
                this._isWriting = false;
                this._streams = new();
            }

            public bool IsValidStream(Stream stream) => _streams.Contains(stream);

            /// <summary>
            /// 开启写入流 (同时只能有一个)
            /// </summary>
            public Stream BeginWrite()
            {
                // 写入操作是独占的
                if (_streams.Count > 0)
                {
                    return null;
                }

                var wstream = _storage._fileSystem.OpenWrite(fileName);
                _streams.Add(wstream);
                _isWriting = true;
                return wstream;
            }

            public bool EndWrite(Stream stream, Utility.Checksum checksum)
            {
                if (_streams.Remove(stream))
                {
                    var length = (uint)stream.Length;
                    var newDigest = new ContentDigest(length, checksum);
                    if (_digest != newDigest)
                    {
                        Utility.SLogger.Info("update local storage: {0} {1} => {2}", _fileName, _digest, newDigest);
                        _digest = newDigest;
                    }
                    stream.Close();
                    _isWriting = false;
                    return true;
                }
                return false;
            }

            /// <summary>
            /// 开启读取流 (可以同时存在多个读取流)
            /// </summary>
            public Stream BeginRead()
            {
                var rstream = _storage._fileSystem.OpenRead(fileName);
                _streams.Add(rstream);
                return rstream;
            }

            public bool EndRead(Stream stream)
            {
                if (_streams.Remove(stream))
                {
                    stream.Close();
                    return true;
                }
                return false;
            }

            /// <summary>
            /// 强行关闭所有打开的文件流
            /// </summary>
            /// <returns>被关闭的文件流数量</returns>
            public int CloseAnyway()
            {
                var n = _streams.Count;
                for (var i = 0; i < n; ++i)
                {
                    try
                    {
                        _streams[i].Close();
                    }
                    catch (Exception exception)
                    {
                        Utility.SLogger.Exception(exception, "failed to close local storage stream {0}", _fileName);
                    }
                }
                return n;
            }

            public override string ToString() => $"{fileName} {_digest} (w: {isWriting})";
        }
    }
}