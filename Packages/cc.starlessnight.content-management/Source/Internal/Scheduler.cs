using System;
using System.Threading;
using System.Collections.Generic;

namespace Iris.ContentManagement.Internal
{
    using Iris.ContentManagement.Utility;
    using UnityEngine;

    public class Scheduler
    {
        private int _mainThreadId;
        private ReaderWriterLockSlim _threadedActionsLock = new();
        private Queue<Action> _threadedActions = new();
        private Queue<Action> _actions = new();

        internal Scheduler()
        {
            _mainThreadId = Thread.CurrentThread.ManagedThreadId;
#if UNITY_EDITOR
            if (!UnityEditor.EditorApplication.isPlaying)
            {
                UnityEditor.EditorApplication.update += OnUpdate;
                return;
            }
#endif
            new GameObject(nameof(Scheduler)).AddComponent<SchedulerHost>().Bind(this);
        }

        public void Shutdown()
        {
#if UNITY_EDITOR
            if (!UnityEditor.EditorApplication.isPlaying)
            {
                UnityEditor.EditorApplication.update -= OnUpdate;
            }
#endif
        }

        public void ForceUpdate()
        {
            OnUpdate();
            Thread.Sleep(50);
        }

        public void ForceUpdate(Func<bool> isContinueFunc)
        {
            while (isContinueFunc())
            {
                OnUpdate();
                Thread.Sleep(50);
            }
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

        internal void OnUpdate()
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
