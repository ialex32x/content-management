using System;
using System.Threading;
using System.Collections.Generic;

namespace Iris.ContentManagement.Internal
{
    using Iris.ContentManagement.Utility;
    using UnityEngine;

    public class Scheduler
    {
        private static Scheduler _instance;
        private int _mainThreadId;
        private ReaderWriterLockSlim _threadedActionsLock = new();
        private Queue<Action> _threadedActions = new();
        private Queue<Action> _actions = new();

        public static void Initialize()
        {
            _instance = new Scheduler();
            _instance._mainThreadId = Thread.CurrentThread.ManagedThreadId;


#if UNITY_EDITOR
            if (!UnityEditor.EditorApplication.isPlaying)
            {
                UnityEditor.EditorApplication.update += Update;
            }
            else
            {
                new GameObject(nameof(Scheduler)).AddComponent<SchedulerHost>();
            }
#else
                new GameObject(nameof(Scheduler)).AddComponent<SchedulerHost>();
#endif
        }

        public static Scheduler Get() => _instance;

        public static void Shutdown()
        {
            if (_instance != null)
            {
#if UNITY_EDITOR
                if (!UnityEditor.EditorApplication.isPlaying)
                {
                    UnityEditor.EditorApplication.update -= Update;
                }
#endif
                _instance = null;
            }
        }

        public static void Update()
        {
            if (_instance != null)
            {
                _instance.OnUpdate();
            }
        }

        public static void ForceUpdate()
        {
            if (_instance != null)
            {
                _instance.OnUpdate();
            }
            Thread.Sleep(50);
        }

        public void Post(Action action)
        {
            if (_mainThreadId != Thread.CurrentThread.ManagedThreadId)
            {
                _threadedActionsLock.EnterWriteLock();
                _threadedActions.Enqueue(action);
                _threadedActionsLock.ExitWriteLock();
            }
            else
            {
                _actions.Enqueue(action);
            }
        }

        private void OnUpdate()
        {
            var count = _actions.Count;
            while (count-- > 0)
            {
                try
                {
                    _actions.Dequeue()();
                }
                catch (Exception exception)
                {
                    Utility.Logger.Exception(exception);
                    break;
                }
            }
            _threadedActionsLock.EnterUpgradeableReadLock();
            count = _threadedActions.Count;
            if (count > 0)
            {
                _threadedActionsLock.EnterWriteLock();
                do
                {
                    try
                    {
                        _threadedActions.Dequeue()();
                    }
                    catch (Exception exception)
                    {
                        Utility.Logger.Exception(exception);
                        break;
                    }
                } while (--count > 0);
                _threadedActionsLock.ExitWriteLock();
            }
            _threadedActionsLock.ExitUpgradeableReadLock();
        }
    }
}
