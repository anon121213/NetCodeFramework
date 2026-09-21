using System;

namespace Skynet.Diagnostics
{
    /// <summary>
    /// No-op logger. Default when no sink is registered — keeps Skynet silent instead of throwing.
    /// Thread-safe by virtue of doing nothing.
    /// </summary>
    public sealed class NullSkynetLogger : ISkynetLogger
    {
        public static readonly NullSkynetLogger Instance = new NullSkynetLogger();

        private NullSkynetLogger() { }

        public void Info(string message) { }
        public void Warn(string message) { }
        public void Error(string message) { }
        public void Exception(string message, Exception exception) { }
        public void Exception(Exception exception) { }
    }
}
