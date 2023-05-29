namespace Iris.ContentManagement.Utility
{
    public interface ILogHandler
    {
        void Debug(string text);
        void Debug(string fmt, params object[] args);
        void Info(string text);
        void Info(string fmt, params object[] args);
        void Warning(string text);
        void Warning(string fmt, params object[] args);
        void Error(string text);
        void Error(string fmt, params object[] args);
        void Fatal(string text);
        void Fatal(string fmt, params object[] args);
        void Exception(System.Exception exception);
        void Exception(System.Exception exception, string description);
        void Exception(System.Exception exception, string fmt, params object[] args);
        void OnInternalError(System.Exception exception);
    }
}