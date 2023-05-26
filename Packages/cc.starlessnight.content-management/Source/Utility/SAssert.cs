using System.Diagnostics;

namespace Iris.ContentManagement.Utility
{
    public static class SAssert
    {
        /// <summary>
        /// 发布期断言, 在定义为 CONTENTMANAGEMENT_RELEASE 时仅报错, 不暂停, 否则等价于 Debug 断言 
        /// </summary>
        [Conditional("CONTENTMANAGEMENT_DEBUG")]
        [Conditional("CONTENTMANAGEMENT_RELEASE")]
        public static void Never(string message = "")
        {
            var stackTrace = new StackTrace(1, true);
            var text = "[ASSERT_FAILED][NEVER] ";

            if (!string.IsNullOrEmpty(message))
            {
                text += message + "\n";
            }
            text += stackTrace.ToString();

#if CONTENTMANAGEMENT_DEBUG
            SLogger.Fatal(text);
#else 
            Logger.Error(text);
#endif
        }

        /// <summary>
        /// 发布期断言, 在定义为 CONTENTMANAGEMENT_RELEASE 时仅报错, 不暂停, 否则等价于 Debug 断言 
        /// </summary>
        [Conditional("CONTENTMANAGEMENT_DEBUG")]
        [Conditional("CONTENTMANAGEMENT_RELEASE")]
        public static void Release(bool condition, string message = "")
        {
            if (condition)
            {
                return;
            }
            var stackTrace = new StackTrace(1, true);
            var text = "[ASSERT_FAILED][RELEASE] ";

            if (!string.IsNullOrEmpty(message))
            {
                text += message + "\n";
            }
            text += stackTrace.ToString();

#if CONTENTMANAGEMENT_DEBUG
            SLogger.Fatal(text);
#else 
            Logger.Error(text);
#endif
        }

        /// <summary>
        /// 调试期断言, 触发时将暂停编辑器运行
        /// </summary>
        [Conditional("CONTENTMANAGEMENT_DEBUG")]
        public static void Debug(bool condition, string message = "")
        {
            if (condition)
            {
                return;
            }
            var stackTrace = new StackTrace(1, true);
            var text = "[ASSERT_FAILED][DEBUG] ";

            if (!string.IsNullOrEmpty(message))
            {
                text += message + "\n";
            }
            text += stackTrace.ToString();

            SLogger.Fatal(text);
        }

        public static bool Ensure(bool condition, string message = "")
        {
            if (condition)
            {
                return true;
            }

#if CONTENTMANAGEMENT_DEBUG
            var stackTrace = new StackTrace(1, true);
            var text = "[ASSERT_FAILED] ";

            if (!string.IsNullOrEmpty(message))
            {
                text += message + "\n";
            }
            text += stackTrace.ToString();

            SLogger.Fatal(text);
#endif
            return false;
        }
    }
}
