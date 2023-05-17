using System;

namespace Iris.ContentManagement
{
    using Iris.ContentManagement.Utility;
    using Iris.ContentManagement.Internal;

    public struct AsyncLoadInstruction<T>
    where T : UnityEngine.Object
    {
        private IAsset _asset;

        public AsyncLoadInstruction(IAsset asset)
        {
            _asset = asset;
        }

        public AssetAwaiter<T> GetAwaiter() => new AssetAwaiter<T>(_asset, false);
    }

    public struct AsyncInstantiateInstruction
    {
        private IAsset _asset;

        public AsyncInstantiateInstruction(IAsset asset)
        {
            _asset = asset;
        }

        public AssetAwaiter<UnityEngine.GameObject> GetAwaiter() => new AssetAwaiter<UnityEngine.GameObject>(_asset, true);
    }

    //TODO 按照 IAsset 最终接口调整实现
    public struct AssetHandle
    {
        private IAsset _asset;

        public AssetHandle(IAsset asset)
        {
            _asset = asset;
        }

        public T Load<T>() where T : UnityEngine.Object => (T)_asset?.Get();

        public UnityEngine.Object Load() => _asset?.Get();

        public T Instantiate<T>() where T : UnityEngine.Object => Load() is T obj ? UnityEngine.Object.Instantiate<T>(obj) : default;

        // 异步加载, 如果是对一个已经完成加载或只能同步加载的资源, 该调用会立即完成
        public AsyncLoadInstruction<UnityEngine.Object> LoadAsync() => new AsyncLoadInstruction<UnityEngine.Object>(_asset);
        
        public AsyncLoadInstruction<T> LoadAsync<T>() where T : UnityEngine.Object => new AsyncLoadInstruction<T>(_asset);

        // 并不是真的异步实例化 (因为 Unity 本身不支持), 只是异步加载并实例化
        public AsyncInstantiateInstruction InstantiateAsync<T>() where T : UnityEngine.Object => new AsyncInstantiateInstruction(_asset);

        public void Invalidate()
        {
            _asset = null;
        }

        public override string ToString()
        {
            return _asset != null ? _asset.ToString() : "None";
        }
    }
}