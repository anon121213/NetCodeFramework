using System.Threading;
using UnityEngine;

namespace _Scripts.Netcore.NetworkComponents.RootComponents
{
    public abstract class NetworkBehaviour: MonoBehaviour, IRPCCaller
    {
        private static int _instanceCounter;

        public int InstanceId { get; private set; } 

        public void InitializeNetworkBehaviour() => 
            InstanceId = Interlocked.Increment(ref _instanceCounter);
    }
}