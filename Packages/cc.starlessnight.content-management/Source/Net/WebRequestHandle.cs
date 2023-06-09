namespace Iris.ContentManagement.Net
{
    using Internal;
    using Iris.ContentManagement.Utility;

    public struct WebRequestHandle
    {
        private SIndex _callback;

        private readonly WebRequestInfo _info;
        private readonly IDownloader _downloader;

        public WebRequestInfo info => _info;

        public bool isValid => _downloader != null ? _downloader.IsValidRequest(_info, _callback) : false;

        internal WebRequestHandle(IDownloader downloader, in WebRequestInfo info, in SIndex callback)
        {
            _info = info;
            _downloader = downloader;
            _callback = callback;
        }

        public WebRequestHandle Bind(WebRequestAction action)
        {
            _downloader.RegisterCallback(_info, ref _callback, action);
            return this;
        }

        public void Unbind() => _downloader.UnregisterCallback(_info, _callback);

        public WebRequestHandle WaitUntilCompleted()
        {
            _downloader.WaitUntilCompleted(_info.name);
            return this;
        }

        public bool Equals(in WebRequestHandle other) => this == other;

        public bool Equals(WebRequestHandle other) => this == other;

        public override bool Equals(object obj) => obj is WebRequestHandle other && this == other;

        public override int GetHashCode() => _callback.GetHashCode();

        public override string ToString() => $"{nameof(WebRequestHandle)} {_info.id} {_info.name}";

        public static bool operator ==(WebRequestHandle a, WebRequestHandle b) => a._downloader == b._downloader && a._info == b._info && a._callback == b._callback;

        public static bool operator !=(WebRequestHandle a, WebRequestHandle b) => a._downloader != b._downloader || a._info != b._info || a._callback != b._callback;
    }
}