using System;
using System.Threading;
using System.Collections.Generic;

namespace Iris.ContentManagement.Internal
{
    using UnityEngine;

    public class SchedulerHost : MonoBehaviour
    {
        void Update()
        {
            Scheduler.Update();
        }

        void OnDestroy()
        {
            Scheduler.Shutdown();
        }
    }
}