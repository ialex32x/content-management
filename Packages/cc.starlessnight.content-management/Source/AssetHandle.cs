namespace Iris.ContentManagement
{
    using Iris.ContentManagement.Internal;

    public struct AsyncLoadInstruction<T>
    where T : class
    {
        private IAsset _asset;

        public AsyncLoadInstruction(IAsset asset)
        {
            _asset = asset;
        }

        public AssetLoadAwaiter<T> GetAwaiter() => new AssetLoadAwaiter<T>(_asset);
    }

    public struct AsyncInstantiateInstruction<T>
    where T : UnityEngine.Object
    {
        private IAsset _asset;

        public AsyncInstantiateInstruction(IAsset asset)
        {
            _asset = asset;
        }

        public AssetInstantiateAwaiter<T> GetAwaiter() => new AssetInstantiateAwaiter<T>(_asset);
    }

    public struct AssetHandle
    {
        private IAsset _asset;

        public bool isCompleted => _asset == null || _asset.isCompleted;

        internal AssetHandle(IAsset asset) => _asset = asset;

        public T Get<T>() where T : class => (T)_asset?.Get();

        public T Instantiate<T>() where T : UnityEngine.Object => _asset?.Get() is T obj ? UnityEngine.Object.Instantiate<T>(obj) : default;

        public AsyncLoadInstruction<T> LoadAsync<T>() where T : class => new AsyncLoadInstruction<T>(_asset);

        public AsyncLoadInstruction<object> LoadAsync() => new AsyncLoadInstruction<object>(_asset);

        public AsyncInstantiateInstruction<T> InstantiateAsync<T>() where T : UnityEngine.Object => new AsyncInstantiateInstruction<T>(_asset);

        public AsyncInstantiateInstruction<UnityEngine.GameObject> InstantiateAsync() => new AsyncInstantiateInstruction<UnityEngine.GameObject>(_asset);

        public void Invalidate() => _asset = null;

        public override string ToString() => _asset != null ? _asset.ToString() : "None";

        public static implicit operator UnityEngine.Object(AssetHandle handle) => handle.Get<UnityEngine.Object>();
        public static implicit operator UnityEngine.GameObject(AssetHandle handle) => handle.Get<UnityEngine.GameObject>();


        public bool Equals(AssetHandle other) => this == other;

        public override bool Equals(object obj) => obj is AssetHandle other && this == other;

        public override int GetHashCode() => _asset != null ? _asset.GetHashCode() : 0;

        public static implicit operator string(AssetHandle a) => a.ToString();

        public static bool operator ==(AssetHandle a, AssetHandle b) => a._asset == b._asset;

        public static bool operator !=(AssetHandle a, AssetHandle b) => a._asset != b._asset;
    }
}