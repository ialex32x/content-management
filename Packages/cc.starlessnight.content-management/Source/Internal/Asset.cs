namespace Iris.ContentManagement.Internal
{
    public interface IAsset
    {
        bool isCompleted { get; }

        object Get();
        void RequestSyncLoad();
        void RequestAsyncLoad(ref Utility.SIndex index, IRequestHandler handler);
        void CancelRequest(ref Utility.SIndex index);
    }
}
