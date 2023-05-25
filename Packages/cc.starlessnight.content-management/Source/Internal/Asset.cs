
using Iris.ContentManagement.Utility;

namespace Iris.ContentManagement.Internal
{
    public interface IAsset
    {
        bool isCompleted { get; }

        object Get();
        void RequestSyncLoad();
        void RequestAsyncLoad(ref Utility.SIndex index, IAssetRequestHandler handler);
        void CancelRequest(ref Utility.SIndex index);
    }
}
