using System;

namespace Skynet.Diagnostics
{
    /// <summary>
    /// Sink for framework diagnostics. Implementations must be thread-safe:
    /// Skynet writes from socket receive threads, RPC processors, and the Unity main thread.
    /// </summary>
    public interface ISkynetLogger
    {
        void Info(string message);
        void Warn(string message);
        void Error(string message);
        void Error(string message, Exception exception);
    }
}
