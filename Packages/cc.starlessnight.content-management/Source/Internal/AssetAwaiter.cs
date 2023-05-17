using System;
using System.Runtime.CompilerServices;

namespace Iris.ContentManagement.Internal
{
    public struct AssetAwaiter<T> : ICriticalNotifyCompletion
    where T : UnityEngine.Object
    {
        private bool _instantiate;
        private IAsset _asset;
        private AssetRequest _request;

        public bool IsCompleted => _asset.state == EAssetState.Loaded || _asset.state == EAssetState.Invalid;

        public AssetAwaiter(IAsset asset, bool instantiate)
        {
            _asset = asset;
            _instantiate = instantiate;
            _request = null;

            if (!this.IsCompleted)
            {
                //TODO 发起异步加载请求
                // _request = _asset.RequestAsyncLoad(_asset);
            }
        }

        public T GetResult()
        {
            return _instantiate ? UnityEngine.Object.Instantiate<T>((T)_asset.Get()) : (T)_asset.Get();
        }

        public void OnCompleted(Action continuation)
        {
            Utility.Assert.Debug(_request != null);
            _request.continuation = continuation;
        }

        public void UnsafeOnCompleted(Action continuation)
        {
            Utility.Assert.Debug(_request != null);
            _request.continuation = continuation;
        }
    }
}
