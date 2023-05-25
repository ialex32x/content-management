using System;
using System.Runtime.CompilerServices;

namespace Iris.ContentManagement.Internal
{
    internal class AssetRequest : IAssetRequestHandler
    {
        public IAsset asset;
        public Exception exception;
        public Action continuation;

        public void OnRequestCompleted()
        {
            continuation?.Invoke();
        }
    }

    public struct AssetLoadAwaiter<T> : ICriticalNotifyCompletion
    where T : class
    {
        private AssetRequest _request;

        public bool IsCompleted => _request.asset.isCompleted;

        public AssetLoadAwaiter(IAsset asset)
        {
            _request = new AssetRequest();
            _request.asset = asset;

            if (!this.IsCompleted)
            {
                var index = Utility.SIndex.None;
                //TODO 使用不同的接口处理 stream/asset, 在哪一层区分 Zip File Stream 和 Unity Asset ?
                if (typeof(T).IsSubclassOf(typeof(System.IO.Stream)))
                {
                    
                }
                _request.asset.RequestAsyncLoad(ref index, _request);
            }
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

        public T GetResult()
        {
            if (_request.exception != null)
            {
                throw _request.exception;
            }
            return (T)_request.asset.Get();
        }
    }

    public struct AssetInstantiateAwaiter<T> : ICriticalNotifyCompletion
    where T : UnityEngine.Object
    {
        private AssetRequest _request;

        public bool IsCompleted => _request.asset.isCompleted;

        public AssetInstantiateAwaiter(IAsset asset)
        {
            _request = new AssetRequest();
            _request.asset = asset;

            if (!this.IsCompleted)
            {
                var index = Utility.SIndex.None;
                _request.asset.RequestAsyncLoad(ref index, _request);
            }
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

        public T GetResult()
        {
            if (_request.exception != null)
            {
                throw _request.exception;
            }
            return UnityEngine.Object.Instantiate<T>((T)_request.asset.Get());
        }
    }
}
