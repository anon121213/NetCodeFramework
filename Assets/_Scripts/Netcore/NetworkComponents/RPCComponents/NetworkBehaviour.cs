using System.Threading;
using UnityEngine;

namespace _Scripts.Netcore.NetworkComponents.RPCComponents
{
    public abstract class NetworkBehaviour: MonoBehaviour, IRPCCaller, INetworkComponent
    {
        private static int _instanceCounter;

        public int InstanceId { get; private set; } 

        public void InitializeNetworkBehaviour() => 
            InstanceId = Interlocked.Increment(ref _instanceCounter);
    }

    public interface INetworkComponent
    {
    }
}