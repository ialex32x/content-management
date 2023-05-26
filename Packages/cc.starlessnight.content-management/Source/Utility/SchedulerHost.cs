namespace Iris.ContentManagement.Utility
{
    internal class SchedulerHost : UnityEngine.MonoBehaviour
    {
        private IScheduler _scheduler;

        internal void Bind(IScheduler scheduler) => _scheduler = scheduler;

        void Update() => _scheduler.OnUpdate();
    }
}