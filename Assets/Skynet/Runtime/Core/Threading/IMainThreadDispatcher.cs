using System;

namespace Skynet.Threading
{
    public interface IMainThreadDispatcher
    {
        void Post(Action action);
        bool IsOnMainThread { get; }
    }
}