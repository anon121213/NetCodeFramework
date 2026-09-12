using UnityEngine;

namespace Skynet.NetworkComponents.RPCComponents
{
    [RequireComponent(typeof(NetworkObject))]
    public abstract class NetworkBehaviour : MonoBehaviour, IRPCCaller, INetworkComponent
    {
        public int InstanceId { get; private set; } = -1;

        public bool TryInitNetworkId(int hash)
        {
            if (InstanceId >= 0) 
                return false;
            
            InstanceId = hash;
            return true;
        }
    }

    public interface INetworkComponent
    {
    }
}
