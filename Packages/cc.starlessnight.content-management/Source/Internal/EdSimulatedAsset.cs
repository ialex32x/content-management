using System;

namespace Iris.ContentManagement.Internal
{
    using Iris.ContentManagement.Utility;

    public sealed class EdSimulatedAsset : IAsset
    {
        public readonly static EdSimulatedAsset Null = new(default, true);

        private string _assetPath;
        private bool _loaded;
        private object _cache;
        private SList<bool> _requests = new();

        public bool isCompleted => _loaded;

        public EdSimulatedAsset(string assetPath, bool loaded = false)
        {
            _assetPath = assetPath;
            _loaded = loaded;
        }

        //TODO make it configurable 
        //TODO remove it if asset/file are treated as different request
        private static bool IsUnityObject(string assetPath)
        {
            return !assetPath.EndsWith(".txt")
                && !assetPath.EndsWith(".json");
        }

        private void TryLoad()
        {
            if (!_loaded)
            {
                _loaded = true;
#if UNITY_EDITOR
                if (IsUnityObject(_assetPath))
                {
                    _cache = UnityEditor.AssetDatabase.LoadMainAssetAtPath(_assetPath);
                    Utility.Logger.Debug("{0} read as asset {1}", nameof(EdSimulatedAsset), _assetPath);
                    return;
                }
#endif
                _cache = System.IO.File.OpenRead(_assetPath);
                Utility.Logger.Debug("{0} read as file stream {1}", nameof(EdSimulatedAsset), _assetPath);
            }
        }

        public object Get()
        {
            TryLoad();
            return _cache;
        }

        public override string ToString() => _assetPath;

        public void RequestSyncLoad()
        {
            TryLoad();
        }

        public void RequestAsyncLoad(ref SIndex index, IAssetRequestHandler handler)
        {
            _requests.RemoveAt(index);
            if (_loaded)
            {
                handler.OnRequestCompleted();
                return;
            }
            index = _requests.Add(default);
            var capturedIndex = index;
            var capturedHandler = new WeakReference<IAssetRequestHandler>(handler);
            ContentSystem.Scheduler.Post(() =>
            {
                if (_requests.RemoveAt(capturedIndex) && capturedHandler.TryGetTarget(out var target))
                {
                    target.OnRequestCompleted();
                }
            });
        }

        public void CancelRequest(ref SIndex index)
        {
            _requests.RemoveAt(index);
        }
    }
}
