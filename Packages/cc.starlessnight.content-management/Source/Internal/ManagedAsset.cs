using System;

namespace Iris.ContentManagement.Internal
{
    using Iris.ContentManagement.Utility;

    public sealed class ManagedAsset : IAsset, IManagedAssetRequestHandler
    {
        private enum EAssetState
        {
            Created,
            Loading,
            Loaded,
            Invalid,
        }

        private string _assetPath;
        private EAssetState _state;
        private object _cached;
        private IPackage _package;
        private SIndex _assetRequestHandlerIndex;
        private SList<IRequestHandler> _handlers = new();

        public bool isCompleted => _state == EAssetState.Loaded || _state == EAssetState.Invalid;

        internal ManagedAsset(IPackage package, string assetPath)
        {
            _assetPath = assetPath;
            _package = package;
        }

        public object Get()
        {
            RequestSyncLoad();
            //TODO 暂时硬编码 ZipStream 的处理
            if (_cached is IManagedStream mstream)
            {
                return mstream.Open(_assetPath);
            }
            return _cached;
        }

        public void RequestSyncLoad()
        {
            switch (_state)
            {
                case EAssetState.Loaded:
                    return;
                case EAssetState.Created:
                    _state = EAssetState.Loading;
                    ((IManagedAssetRequestHandler)this).OnRequestCompleted(_package.LoadAssetSync(_assetPath));
                    return;
                case EAssetState.Loading:
                    {
                        Utility.SLogger.Debug("wait for pending request {0}", _assetPath);
                        ContentSystem.Scheduler.WaitUntilCompleted(() => isCompleted);
                        Utility.SLogger.Debug("finish pending request {0}", _assetPath);
                        return;
                    }
                default: Utility.SAssert.Never(); return;
            }
        }

        public void CancelRequest(ref SIndex index)
        {
            _handlers.RemoveAt(index);
            index = SIndex.None;
        }

        public void RequestAsyncLoad(ref SIndex index, IRequestHandler handler)
        {
            _handlers.RemoveAt(index);
            switch (_state)
            {
                case EAssetState.Created:
                    _state = EAssetState.Loading;
                    index = _handlers.Add(handler);
                    _package.RequestAssetAsync(ref _assetRequestHandlerIndex, _assetPath, this);
                    return;
                case EAssetState.Loaded:
                    index = SIndex.None;
                    handler.OnRequestCompleted();
                    return;
                case EAssetState.Loading:
                    index = _handlers.Add(handler);
                    return;
                default:
                    Utility.SAssert.Never();
                    return;
            }
        }

        void IManagedAssetRequestHandler.OnRequestCompleted(object target)
        {
            if (_state != EAssetState.Loaded)
            {
                _state = EAssetState.Loaded;
                _cached = target;
                while (_handlers.TryRemoveAt(0, out var handler))
                {
                    handler.OnRequestCompleted();
                }
            }
        }

        public override string ToString()
        {
            if (_state == EAssetState.Loaded && _cached == null)
            {
                return $"{_assetPath} (FAILED)";
            }
            return $"{_assetPath} ({_state})";
        }
    }
}
