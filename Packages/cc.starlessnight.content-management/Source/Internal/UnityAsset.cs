using System;

namespace Iris.ContentManagement.Internal
{
    using Iris.ContentManagement.Utility;

    public sealed class UnityAsset : IAsset, IUnityAssetRequestHandler
    {
        private string _assetPath;
        private EAssetState _state;
        private UnityEngine.Object _cached;
        private AssetBundlePackage _package;
        private SIndex _assetRequestHandlerIndex;
        private SList<IAssetRequestHandler> _handlers = new();

        public EAssetState state => _state;

        public UnityAsset(AssetBundlePackage package, string assetPath)
        {
            _assetPath = assetPath;
            _package = package;
        }

        public void RequestSyncLoad()
        {
            switch (_state)
            {
                case EAssetState.Loaded:
                    return;
                case EAssetState.Created:
                case EAssetState.Loading:
                    {
                        _package.CancelAssetRequest(_assetRequestHandlerIndex);
                        ((IUnityAssetRequestHandler)this).OnRequestCompleted(_package.LoadAssetSync(_assetPath));
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
                    {
                        _state = EAssetState.Loading;
                        index = _handlers.Add(handler);
                        _package.RequestAssetAsync(ref _assetRequestHandlerIndex, _assetPath, this);
                        return;
                    }
                case EAssetState.Loaded:
                    {
                        index = SIndex.None;
                        handler.OnRequestCompleted();
                        return;
                    }
                case EAssetState.Loading:
                    {
                        index = _handlers.Add(handler);
                        return;
                    }
                default: Utility.Assert.Never(); return;
            }
        }

        void IUnityAssetRequestHandler.OnRequestCompleted(UnityEngine.Object asset)
        {
            if (_state != EAssetState.Loaded)
            {
                _state = EAssetState.Loaded;
                _cached = asset;

                while (_handlers.TryRemoveAt(0, out var handler))
                {
                    handler.OnRequestCompleted();
                }
            }
        }

        public UnityEngine.Object Get()
        {
            RequestSyncLoad();
            return _cached;
        }

        public override string ToString()
        {
            return $"{nameof(UnityAsset)}({_assetPath} {_state})";
        }
    }
}
