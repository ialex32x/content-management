using System;
using System.Diagnostics;

namespace Iris.ContentManagement.Utility
{
    public static class Logger
    {
        private static ILogHandler _handler;

        public static ILogHandler handler
        {
            set { _handler = value; }
            get
            {
                if (_handler == null)
                {
                    _handler = new MinimalLogHandler();
                }
                return _handler;
            }
        }

        [Conditional("CONTENTMANAGEMENT_DEBUG")]
        public static void Debug(string text)
        {
            try { handler.Debug(text); }
            catch (System.Exception) { }
        }

        [Conditional("CONTENTMANAGEMENT_DEBUG")]
        public static void Debug(object obj)
        {
            try { handler.Debug(obj.ToString()); }
            catch (System.Exception) { }
        }

        [Conditional("CONTENTMANAGEMENT_DEBUG")]
        public static void Debug(string fmt, params object[] args)
        {
            try { handler.Debug(fmt, args); }
            catch (System.Exception) { }
        }

        [Conditional("CONTENTMANAGEMENT_DEBUG")]
        public static void Info(string text)
        {
            try { handler.Info(text); }
            catch (System.Exception) { }
        }

        [Conditional("CONTENTMANAGEMENT_DEBUG")]
        public static void Info(object obj)
        {
            try { handler.Info(obj.ToString()); }
            catch (System.Exception) { }
        }

        [Conditional("CONTENTMANAGEMENT_DEBUG")]
        public static void Info(string fmt, params object[] args)
        {
            try { handler.Info(fmt, args); }
            catch (System.Exception) { }
        }

        [Conditional("CONTENTMANAGEMENT_DEBUG")]
        public static void Warning(string text)
        {
            try { handler.Warning(text); }
            catch (System.Exception) { }
        }

        [Conditional("CONTENTMANAGEMENT_DEBUG")]
        public static void Warning(object obj)
        {
            try { handler.Warning(obj.ToString()); }
            catch (System.Exception) { }
        }

        [Conditional("CONTENTMANAGEMENT_DEBUG")]
        public static void Warning(string fmt, params object[] args)
        {
            try { handler.Warning(fmt, args); }
            catch (System.Exception) { }
        }

        [Conditional("CONTENTMANAGEMENT_DEBUG")]
        [Conditional("CONTENTMANAGEMENT_RELEASE")]
        public static void Error(string text)
        {
            try { handler.Error(text); }
            catch (System.Exception) { }
        }

        [Conditional("CONTENTMANAGEMENT_RELEASE")]
        public static void Error(object obj)
        {
            try { handler.Error(obj.ToString()); }
            catch (System.Exception) { }
        }

        [Conditional("CONTENTMANAGEMENT_DEBUG")]
        [Conditional("CONTENTMANAGEMENT_RELEASE")]
        public static void Error(string fmt, params object[] args)
        {
            try { handler.Error(fmt, args); }
            catch (System.Exception) { }
        }

        [Conditional("CONTENTMANAGEMENT_DEBUG")]
        [Conditional("CONTENTMANAGEMENT_RELEASE")]
        public static void Fatal(string text)
        {
            try { handler.Fatal(text); }
            catch (System.Exception) { }
        }

        [Conditional("CONTENTMANAGEMENT_RELEASE")]
        public static void Fatal(object obj)
        {
            try { handler.Fatal(obj.ToString()); }
            catch (System.Exception) { }
        }

        [Conditional("CONTENTMANAGEMENT_DEBUG")]
        [Conditional("CONTENTMANAGEMENT_RELEASE")]
        public static void Fatal(string fmt, params object[] args)
        {
            try { handler.Fatal(fmt, args); }
            catch (System.Exception) { }
        }

        public static void Exception(System.Exception exception)
        {
            try { handler.Exception(exception); }
            catch (System.Exception) { }
        }

        public static void Exception(System.Exception exception, string description)
        {
            try { handler.Exception(exception, description); }
            catch (System.Exception) { }
        }

        public static void Exception(System.Exception exception, string fmt, params object[] args)
        {
            try { handler.Exception(exception, fmt, args); }
            catch (System.Exception) { }
        }

        private class MinimalLogHandler : ILogHandler
        {
            private const string kLogCat = "Content";

            public void Debug(string text) => UnityEngine.Debug.LogFormat("{0} {1}", kLogCat, text);

            public void Debug(string fmt, params object[] args) => UnityEngine.Debug.LogFormat("{0} {1}", kLogCat, string.Format(fmt, args));

            public void Info(string text) => UnityEngine.Debug.LogFormat("{0} {1}", kLogCat, text);

            public void Info(string fmt, params object[] args) => UnityEngine.Debug.LogFormat("{0} {1}", kLogCat, string.Format(fmt, args));

            public void Warning(string text) => UnityEngine.Debug.LogWarningFormat("{0} {1}", kLogCat, text);

            public void Warning(string fmt, params object[] args) => UnityEngine.Debug.LogWarningFormat("{0} {1}", kLogCat, string.Format(fmt, args));

            public void Error(string text) => UnityEngine.Debug.LogErrorFormat("{0} {1}", kLogCat, text);

            public void Error(string fmt, params object[] args) => UnityEngine.Debug.LogErrorFormat("{0} {1}", kLogCat, string.Format(fmt, args));

            public void Fatal(string text)
            {
                UnityEngine.Debug.LogErrorFormat("{0} {1}", kLogCat, text);
                UnityEngine.Debug.Break();
            }

            public void Fatal(string fmt, params object[] args)
            {
                UnityEngine.Debug.LogErrorFormat("{0} {1}", kLogCat, string.Format(fmt, args));
                UnityEngine.Debug.Break();
            }

            public void Exception(Exception exception)
            {
                UnityEngine.Debug.LogErrorFormat("{0} {1}\n{2}", kLogCat, exception.Message, exception.StackTrace);
                UnityEngine.Debug.Break();
            }

            public void Exception(Exception exception, string description)
            {
                UnityEngine.Debug.LogErrorFormat("{0} {1} {2}\n{3}", kLogCat, description, exception.Message, exception.StackTrace);
                UnityEngine.Debug.Break();
            }

            public void Exception(Exception exception, string fmt, params object[] args)
            {
                UnityEngine.Debug.LogErrorFormat("{0} {1} {2}\n{3}", kLogCat, string.Format(fmt, args), exception.Message, exception.StackTrace);
                UnityEngine.Debug.Break();
            }
        }
    }
}