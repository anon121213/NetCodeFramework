using UnityEngine;

namespace Skynet.Unity
{
    // Sanity check that Skynet.Unity assembly can reference UnityEngine
    // and Skynet.Core. Remove once real adapter code lands.
    internal static class UnitySanityCheck
    {
        public static string Describe() => $"{CoreSanityCheck.Name} + {Application.unityVersion}";
    }
}
