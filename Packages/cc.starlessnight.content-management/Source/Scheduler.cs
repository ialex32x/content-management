using System;

namespace Iris.ContentManagement
{
    public interface IScheduler
    {
        void ForceUpdate();

        void OnUpdate();

        void WaitUntilCompleted(Func<bool> isCompletedFunc);

        void Post(Action action);

        void Shutdown();
    }
}
