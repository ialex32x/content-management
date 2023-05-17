namespace Iris.ContentManagement.Internal
{
    using Iris.ContentManagement.Utility;
    
    public sealed class EdUnityAsset : IAsset
    {
        private string _assetPath;
        private EAssetState _state;
        private UnityEngine.Object _cache;

        public EAssetState state => _state;

        public EdUnityAsset(string assetPath)
        {
            _assetPath = assetPath;
            _state = EAssetState.Created;
        }

        public IPackage GetPackage() => null;

        private void TryLoad()
        {
            if (_state == EAssetState.Created)
            {
                _state = EAssetState.Loaded;
#if UNITY_EDITOR
                _cache = UnityEditor.AssetDatabase.LoadMainAssetAtPath(_assetPath);

#endif
            }
        }

        public UnityEngine.Object Get()
        {
            RequestSyncLoad();
            return _cache;
        }

        public override string ToString()
        {
            return _assetPath;
        }

        public void RequestSyncLoad()
        {
            TryLoad();
        }

        public void RequestAsyncLoad(ref SIndex index, IAssetRequestHandler handler)
        {
            TryLoad();
            Scheduler.Get().Post(() => handler.OnRequestCompleted());
        }

        public void CancelRequest(ref SIndex index)
        {
        }
    }
}
