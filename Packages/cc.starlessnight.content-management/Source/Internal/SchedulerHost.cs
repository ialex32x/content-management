using System;
using System.Threading;
using System.Collections.Generic;

namespace Iris.ContentManagement.Internal
{
    using UnityEngine;

    public class SchedulerHost : MonoBehaviour
    {
        private Scheduler _scheduler;

        internal void Bind(Scheduler scheduler) => _scheduler = scheduler;

        void Update() => _scheduler.OnUpdate();
    }
}