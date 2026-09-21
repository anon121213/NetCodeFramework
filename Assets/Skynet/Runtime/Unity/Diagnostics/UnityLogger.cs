using System;
using Skynet.Diagnostics;
using UnityEngine;

namespace Skynet.Unity.Diagnostics
{
    public sealed class UnityLogger : ISkynetLogger
    {
        public void Info(string message) => Debug.Log(message);
        public void Warn(string message) => Debug.LogWarning(message);
        public void Error(string message) => Debug.LogError(message);
        public void Exception(Exception message) => Debug.LogException(message);

        public void Exception(string message, Exception exception)
        {
            Debug.LogError(message);
            Debug.LogException(exception);
        }
    }
}
