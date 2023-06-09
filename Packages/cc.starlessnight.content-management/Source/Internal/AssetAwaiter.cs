using System;
using System.Runtime.CompilerServices;

namespace Iris.ContentManagement.Internal
{
    public class AssetAwaiterRequest : IRequestHandler
    {
        public IAsset asset;
        public Exception exception;
        public Action continuation;

        public void OnRequestCompleted() => continuation?.Invoke();
    }

    public struct AssetLoadAwaiter<T> : ICriticalNotifyCompletion
    where T : class
    {
        private AssetAwaiterRequest _request;

        public bool IsCompleted => _request.asset.isCompleted;

        public AssetLoadAwaiter(IAsset asset)
        {
            _request = new();
            _request.asset = asset;

            if (!this.IsCompleted)
            {
                var index = Utility.SIndex.None;
                _request.asset.RequestAsyncLoad(ref index, _request);
            }
        }

        public void OnCompleted(Action continuation)
        {
            Utility.SAssert.Debug(_request != null);
            _request.continuation = continuation;
        }

        public void UnsafeOnCompleted(Action continuation)
        {
            Utility.SAssert.Debug(_request != null);
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
        private AssetAwaiterRequest _request;

        public bool IsCompleted => _request.asset.isCompleted;

        public AssetInstantiateAwaiter(IAsset asset)
        {
            _request = new();
            _request.asset = asset;

            if (!this.IsCompleted)
            {
                var index = Utility.SIndex.None;
                _request.asset.RequestAsyncLoad(ref index, _request);
            }
        }

        public void OnCompleted(Action continuation)
        {
            Utility.SAssert.Debug(_request != null);
            _request.continuation = continuation;
        }

        public void UnsafeOnCompleted(Action continuation)
        {
            Utility.SAssert.Debug(_request != null);
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
