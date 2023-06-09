namespace Iris.ContentManagement.Net
{
    using Internal;
    using Cache;
    using Iris.ContentManagement.Utility;

    public interface IDownloader
    {
        bool isCompleted { get; }
        
        void WaitUntilAllCompleted();
        void WaitUntilCompleted(string entryName);

        WebRequestHandle Enqueue(LocalStorage storage, string entryName);
        WebRequestHandle Enqueue(LocalStorage storage, string entryName, uint expectedSize);

        void RegisterCallback(in WebRequestInfo info, ref SIndex callback, WebRequestAction action);
        void UnregisterCallback(in WebRequestInfo info, in SIndex callback);

        bool IsValidRequest(in WebRequestInfo info, in SIndex callback);

        void Shutdown();
    }
}
