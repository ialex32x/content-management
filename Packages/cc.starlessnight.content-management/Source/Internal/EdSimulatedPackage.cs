using System;

namespace Iris.ContentManagement.Internal
{
    using Iris.ContentManagement.Utility;

    internal class EdSimulatedPackage : IPackage
    {
        internal class ManagedStream : IManagedStream
        {
            public System.IO.Stream Open(string assetPath) => System.IO.File.OpenRead(assetPath);

            public override string ToString() => $"SimulatedStream";
        }

        private SList<bool> _requests = new();

        //TODO make it configurable 
        //TODO remove it if asset/file are treated as different request
        private static bool IsUnityObject(string assetPath)
        {
            return !assetPath.EndsWith(".txt")
                && !assetPath.EndsWith(".json");
        }

        private object LoadObject(string assetPath)
        {
#if UNITY_EDITOR
            if (IsUnityObject(assetPath))
            {
                Utility.SLogger.Debug("SIMULATED read as asset {0}", assetPath);
                return UnityEditor.AssetDatabase.LoadMainAssetAtPath(assetPath);
            }
#endif
            if (System.IO.File.Exists(assetPath))
            {
                Utility.SLogger.Debug("SIMULATED read as file stream {0}", assetPath);
                return new ManagedStream();
            }
            Utility.SLogger.Debug("SIMULATED read nonexistent object {0}", assetPath);
            return null;
        }

        public object LoadAssetSync(string assetName) => LoadObject(assetName);

        public void CancelAssetRequest(in SIndex index) => _requests.RemoveAt(index);

        public void RequestAssetAsync(ref SIndex index, string assetName, IManagedAssetRequestHandler handler)
        {
            _requests.RemoveAt(index);
            index = _requests.Add(default);
            var capturedIndex = index;
            var capturedHandler = new WeakReference<IManagedAssetRequestHandler>(handler);
            var capturedAssetPath = assetName;
            ContentSystem.Scheduler.Post(() =>
            {
                if (_requests.RemoveAt(capturedIndex) && capturedHandler.TryGetTarget(out var target))
                {
                    target.OnRequestCompleted(LoadObject(capturedAssetPath));
                }
            });
        }
    }
}
