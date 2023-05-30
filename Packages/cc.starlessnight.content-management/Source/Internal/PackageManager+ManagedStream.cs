namespace Iris.ContentManagement.Internal
{
    using Cache;
    using Iris.ContentManagement.Utility;

    public sealed partial class PackageManager
    {

        // forward read only (not supported by ZipStream)
        private class ManagedStream : IManagedStream
        {
            private PackageManager _packageManager;
            private SIndex _referenceIndex;

            public ManagedStream(PackageManager packageManager, in SIndex referenceIndex)
            {
                _packageManager = packageManager;
                _referenceIndex = referenceIndex;
            }

            public System.IO.Stream Open(string assetPath) => _packageManager.LoadStream(_referenceIndex, assetPath);

            public override string ToString() => $"{nameof(ManagedStream)} {_referenceIndex}";
        }
    }
}