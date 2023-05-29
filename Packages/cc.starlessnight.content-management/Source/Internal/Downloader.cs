using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Iris.ContentManagement.Internal
{
    using Cache;
    using Iris.ContentManagement.Utility;

    public class Downloader : IWebRequestQueue
    {
        private readonly int _mainThreadId;
        private readonly IUriResolver _uriResolver;
        private int _idgen;
        private int _maxConcurrentRequests = 3;

        private LinkedList<WebRequestImpl> _activeRequests = new();
        private LinkedList<WebRequestImpl> _waitingRequests = new();

        public int count => _activeRequests.Count + _waitingRequests.Count;

        public bool isCompleted => _activeRequests.Count == 0 && _waitingRequests.Count == 0;

        public Downloader(IUriResolver uriResolver)
        {
            Utility.SAssert.Debug(uriResolver != null);
            _uriResolver = uriResolver;
            _mainThreadId = Thread.CurrentThread.ManagedThreadId;
        }

        public WebRequestHandle Enqueue(LocalStorage storage, string entryName) => Enqueue(storage, entryName, 0);

        /// <summary>
        /// 请求将指定文件下载到 local storage 中
        /// </summary>
        /// <param name="expectedSize">非0时将确保下载文件的大小必须满足指定值</param>
        public WebRequestHandle Enqueue(LocalStorage storage, string entryName, uint expectedSize)
        {
            CheckMainThreadAccess();
            var lastRequest = Find(entryName);
            if (lastRequest != null)
            {
                Utility.SLogger.Warning("enqueued an existed entry", lastRequest);
                return new(this, lastRequest.info, default);
            }
            else
            {
                var info = new WebRequestInfo(++_idgen, entryName, expectedSize);
                var request = new WebRequestImpl(this, storage, info);
                if (_activeRequests.Count < _maxConcurrentRequests)
                {
                    _activeRequests.AddLast(request);
                    //NOTE 因为需要中途以同步方式等待, 所以必须在后台线程执行
                    Task.Run(() => request.SendRequest());
                }
                else
                {
                    _waitingRequests.AddLast(request);
                }
                return new(this, info, default);
            }
        }

        /// <summary>
        /// 同步等待所有未完成的请求
        /// </summary>
        public void WaitUntilAllCompleted()
        {
            CheckMainThreadAccess();
            Utility.SLogger.Warning("wait all requests begin");
            ContentSystem.Scheduler.WaitUntilCompleted(() => _activeRequests.Count == 0 && _waitingRequests.Count == 0);
            Utility.SLogger.Warning("wait all requests end");
        }

        /// <summary>
        /// [main thread only] 同步等待指定的任务完成, 不存在该任务时不等待
        /// </summary>
        /// <param name="entryName"></param>
        public void WaitUntilCompleted(string entryName)
        {
            CheckMainThreadAccess();
            var request = Find(entryName);

            if (request != null)
            {
                Utility.SLogger.Warning("wait request begin {0}", entryName);
                ContentSystem.Scheduler.WaitUntilCompleted(() => request.isCompleted);
                Utility.SLogger.Warning("wait request end {0}", entryName);
            }
            else
            {
                Utility.SLogger.Warning("failed to wait request {0}", entryName);
            }
        }

        public void Shutdown()
        {
            CheckMainThreadAccess();
            _waitingRequests.Clear();
            while (_activeRequests.Count > 0)
            {
                var request = _activeRequests.First.Value;
                _activeRequests.RemoveFirst();
                request.Cancel();
            }
        }

        public void RegisterCallback(in WebRequestInfo info, ref SIndex callback, WebRequestAction action)
        {
            var impl = Find(info.name);
            if (impl == null)
            {
                action(new());
                return;
            }
            impl.UnregisterCallback(callback);
            if (impl.isCompleted)
            {
                action(new(impl.info, impl.statusCode));
                return;
            }
            callback = impl.RegisterCallback(action);
        }

        public void UnregisterCallback(in WebRequestInfo info, in SIndex callback)
        {
            var impl = Find(info.name);
            if (impl != null)
            {
                impl.UnregisterCallback(callback);
            }
        }

        public bool IsValidRequest(in WebRequestInfo info, in SIndex callback)
        {
            var impl = Find(info.name);
            return impl != null ? impl.IsValidCallback(callback) : false;
        }

        private WebRequestImpl Find(string entryName)
        {
            var node = _waitingRequests.First;
            while (node != null)
            {
                var request = node.Value;
                if (request.entryName == entryName)
                {
                    return request;
                }
                node = node.Next;
            }
            node = _activeRequests.First;
            while (node != null)
            {
                var request = node.Value;
                if (request.entryName == entryName)
                {
                    return request;
                }
                node = node.Next;
            }

            return default;
        }

        private void CheckMainThreadAccess()
        {
            if (_mainThreadId != Thread.CurrentThread.ManagedThreadId)
            {
                throw new InvalidOperationException("call in background thread is not supported");
            }
        }

        private void OnRequestCompletedThreading(WebRequestImpl request)
        {
            if (_mainThreadId == Thread.CurrentThread.ManagedThreadId)
            {
                OnRequestCompleted(request);
            }
            else
            {
                ContentSystem.Scheduler.Post(() => OnRequestCompleted(request));
            }
        }

        private void OnRequestCompleted(WebRequestImpl request)
        {
            if (_activeRequests.Remove(request))
            {
                request.SendEvents();
            }

            while (_activeRequests.Count < _maxConcurrentRequests)
            {
                var first = _waitingRequests.First;
                if (first == null)
                {
                    break;
                }
                var nextRequest = first.Value;
                _waitingRequests.Remove(first);
                _activeRequests.AddLast(nextRequest);
                Task.Run(() => nextRequest.SendRequest());
            }
        }

        private class WebRequestImpl
        {
            private const int _timeout = 1000 * 3;
            private const int _recvSpeed = 1024 * 512;
            private const int _recvNapTime = 250;
            private readonly WebRequestInfo _info;
            private readonly LocalStorage _storage;
            private readonly Downloader _downloader;

            private volatile bool _isDone;
            private volatile bool _cancelled;
            private long _requestedByteCount;
            private WebRequest _rawRequest;
            private HttpStatusCode _statusCode;
            private SList<WebRequestAction> _callbacks = new();

            public bool isCompleted => _isDone;
            public HttpStatusCode statusCode => _statusCode;
            public string entryName => _info.name;
            public WebRequestInfo info => _info;

            // 当前已完成字节数
            public long requestedByteCount => _requestedByteCount;
            public long totalByteCount => _info.expectedSize;

            public WebRequestImpl(Downloader downloader, LocalStorage storage, in WebRequestInfo info)
            {
                _downloader = downloader;
                _storage = storage;
                _info = info;
            }

            public override string ToString() => _info.ToString();

            public void Cancel()
            {
                if (!_cancelled && !_isDone)
                {
                    _cancelled = true;
                    _isDone = true;
                    _callbacks.Clear();
                    try { _rawRequest?.Abort(); }
                    catch (Exception) { }
                }
            }

            public SIndex RegisterCallback(WebRequestAction action) => _callbacks.Add(action);

            public void UnregisterCallback(in SIndex index) => _callbacks.RemoveAt(index);

            public bool IsValidCallback(in SIndex index) => _callbacks.IsValidIndex(index);

            public void SendEvents()
            {
                try
                {
                    var e = _callbacks.GetUnsafeEnumerator();
                    while (e.MoveNext())
                    {
                        try
                        {
                            e.Current.Invoke(new(_info, _statusCode));
                        }
                        catch (Exception exception)
                        {
                            Utility.SLogger.Exception(exception, "callback error");
                        }
                        e.Remove();
                    }
                }
                catch (Exception exception)
                {
                    Utility.SLogger.Exception(exception);
                }
            }

            private async Task<HttpWebResponse> GetResponse(string uriString, long range, bool acceptRedirect)
            {
                var webRequest = WebRequest.CreateHttp(uriString);
                webRequest.Method = WebRequestMethods.Http.Get;
                webRequest.ContentType = "application/octet-stream";
                webRequest.Timeout = _timeout;
                webRequest.AllowAutoRedirect = false;
                webRequest.UserAgent = _downloader._uriResolver.GetUserAgent();
                webRequest.AddRange(range);

                Utility.SLogger.Debug("requesting {0} from {1}", _info.name, uriString);
                var response = await webRequest.GetResponseAsync() as HttpWebResponse;
                Utility.SLogger.Debug("response {0} ({1})", response.StatusCode, (int)response.StatusCode);
                if (acceptRedirect && response.StatusCode == HttpStatusCode.Redirect)
                {
                    var location = response.GetResponseHeader("location");
                    Utility.SLogger.Warning("redirecting {0} to {1}", _info.name, location);
                    response.Dispose();
                    return await GetResponse(location, range, false);
                }
                // 206 = partial, 200 = ok
                _rawRequest = webRequest;
                return response;
            }

            private bool InitializeWriter(System.IO.Stream writer, out long fileSize)
            {
                if (!writer.CanWrite)
                {
                    fileSize = 0;
                    Utility.SLogger.Warning("invalid file {0}", _info.ToString());
                    return false;
                }
                fileSize = writer.Length;
                if (_info.expectedSize == 0)
                {
                    return true;
                }
                // corrupted if local size is bigger than expected
                if (fileSize > _info.expectedSize)
                {
                    Utility.SLogger.Warning("corrupted file {0}", _info.ToString());
                    writer.SetLength(0);
                    fileSize = 0;
                    return true;
                }
                // is partial?
                if (fileSize < _info.expectedSize)
                {
                    return true;
                }
                return false;
            }

            private bool CheckResponse(HttpWebResponse response)
            {
                if (response == null)
                {
                    return false;
                }
                if (response.StatusCode != HttpStatusCode.OK && response.StatusCode != HttpStatusCode.PartialContent)
                {
                    return false;
                }
                if (_info.expectedSize != 0 && _info.expectedSize != response.ContentLength)
                {
                    Utility.SLogger.Debug("got invalid content-length from http response {0} {1} expected {2}",
                        _info.ToString(), response.ContentLength, _info.expectedSize);
                    return false;
                }
                return true;
            }

            private async Task WriteResponse(HttpWebResponse response, System.IO.Stream writer)
            {
                try
                {
                    //TODO make buffer managed (expose as interface)
                    const int BufferSize = 4096;
                    var buffer = new byte[BufferSize];
                    Utility.SLogger.Debug("getting response stream {0}", _info.name);
                    await using var responseStream = response.GetResponseStream();
                    if (responseStream == null)
                    {
                        return;
                    }
                    responseStream.ReadTimeout = _timeout;
                    var receivedCalc = 0L;
                    Utility.SLogger.Debug("got response stream {0} {1}", _info.name,
                        response.ContentLength);
                    while (!_cancelled && _requestedByteCount < response.ContentLength)
                    {
                        var lastTick = Environment.TickCount;
                        var received = await responseStream.ReadAsync(buffer, 0, BufferSize);
                        if (_cancelled || received <= 0)
                        {
                            break;
                        }

                        _requestedByteCount += received;
                        await writer.WriteAsync(buffer, 0, received);
                        receivedCalc += received;
                        // throttling
                        if (receivedCalc >= _recvSpeed)
                        {
                            var thisTick = Environment.TickCount;
                            var milliSecs = thisTick < lastTick
                                ? (thisTick - int.MaxValue) + (int.MaxValue - lastTick)
                                : thisTick - lastTick;

                            receivedCalc -= _recvSpeed;
                            if (milliSecs < _recvNapTime)
                            {
                                await Task.Delay(_recvNapTime - milliSecs);
                            }
                        }
                    }
                    Utility.SLogger.Debug("finish stream {0}", _info.name);
                }
                catch (Exception exception)
                {
                    Utility.SLogger.Exception(exception);
                }
            }

            public async Task SendRequest()
            {
                Utility.SAssert.Debug(!_isDone);
                Utility.SLogger.Debug("run request {0} on thread {1}", _info.ToString(), Thread.CurrentThread.ManagedThreadId);
                var writer = _storage.OpenWrite(_info.name);
                try
                {
                    if (!InitializeWriter(writer, out _requestedByteCount))
                    {
                        return;
                    }
                    var uriString = _downloader._uriResolver.GetUriString(_info.name);
                    using var response = await GetResponse(uriString, _requestedByteCount, true) as HttpWebResponse;
                    if (!CheckResponse(response))
                    {
                        return;
                    }
                    _statusCode = response.StatusCode;
                    await WriteResponse(response, writer);
                    Utility.SLogger.Debug("ending: {0}", _info.name);
                }
                catch (Exception exception)
                {
                    if (exception is WebException webException && webException.Response is HttpWebResponse response)
                    {
                        _statusCode = response.StatusCode;
                        Utility.SLogger.Warning("WebException Status {0} {1}", _info.name, webException.Status);
                    }
                    Utility.SLogger.Exception(exception, "failed to download {0}", _info.name, _statusCode);
                }
                finally
                {
                    _isDone = true;
                    _rawRequest = null;
                    Utility.SLogger.Debug("request completed: {0}", _info.name);
                    writer.Close();
                    _downloader.OnRequestCompletedThreading(this);
                }
            } // end SendRequest()
        }
    }
}
