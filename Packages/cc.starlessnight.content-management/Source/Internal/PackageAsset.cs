using System;

namespace Iris.ContentManagement.Internal
{
    using Iris.ContentManagement.Utility;

    public sealed class PackageAsset : IAsset, IPackageAssetRequestHandler
    {
        private string _assetPath;
        private EAssetState _state;
        private object _cached;
        private UPackage _package;
        private SIndex _assetRequestHandlerIndex;
        private SList<IAssetRequestHandler> _handlers = new();

        public bool isCompleted => _state == EAssetState.Loaded || _state == EAssetState.Invalid;

        internal PackageAsset(UPackage package, string assetPath)
        {
            _assetPath = assetPath;
            _package = package;
        }

        public object Get()
        {
            RequestSyncLoad();
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
                    ((IPackageAssetRequestHandler)this).OnRequestCompleted(_package.LoadAssetSync(_assetPath));
                    return;
                case EAssetState.Loading:
                    {
                        while (!isCompleted)
                        {
                            Utility.Logger.Debug("wait for pending request {0}", _assetPath);
                            Scheduler.ForceUpdate();
                        }
                        return;
                    }
                default: Utility.Assert.Never(); return;
            }
        }

        public void CancelRequest(ref SIndex index)
        {
            _handlers.RemoveAt(index);
            index = SIndex.None;
        }

        public void RequestAsyncLoad(ref SIndex index, IAssetRequestHandler handler)
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
                    Utility.Assert.Never();
                    return;
            }
        }

        void IPackageAssetRequestHandler.OnRequestCompleted(object target)
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
            return $"{nameof(PackageAsset)}({_assetPath} {_state})";
        }
    }
}
