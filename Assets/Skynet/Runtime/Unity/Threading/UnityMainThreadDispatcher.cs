using System;
using System.Threading;
using Skynet.Threading;

namespace Skynet.Unity.Threading
{
    /// <summary>
    /// Bridges Skynet.Core's <see cref="IMainThreadDispatcher"/> to Unity's main thread via the
    /// <see cref="SynchronizationContext"/> Unity installs on startup. Must be constructed on the main thread —
    /// throws otherwise, because the ctor captures the context that will later be used to hop.
    /// </summary>
    public sealed class UnityMainThreadDispatcher : IMainThreadDispatcher
    {
        private readonly SynchronizationContext _mainThreadContext;
        private readonly int _mainThreadId;

        public UnityMainThreadDispatcher()
        {
            _mainThreadContext = SynchronizationContext.Current
                ?? throw new InvalidOperationException(
                    "UnityMainThreadDispatcher must be constructed on the Unity main thread " +
                    "(no SynchronizationContext.Current available).");
            _mainThreadId = Thread.CurrentThread.ManagedThreadId;
        }

        public bool IsOnMainThread => Thread.CurrentThread.ManagedThreadId == _mainThreadId;

        public void Post(Action action)
        {
            if (IsOnMainThread)
                action();
            else
                _mainThreadContext.Post(static state => ((Action)state!)(), action);
        }
    }
}
