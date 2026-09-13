using System;

namespace Skynet.Threading
{
    public sealed class NullMainThreadDispatcher : IMainThreadDispatcher
    {
        public static readonly NullMainThreadDispatcher Instance = new();
        
        public bool IsOnMainThread => true;

        private NullMainThreadDispatcher() { }
        
        public void Post(Action action) => action();
    }
}