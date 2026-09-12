using UnityEngine;

namespace Skynet.NetworkComponents.RpcComponents
{
    [RequireComponent(typeof(NetworkObject))]
    public abstract class NetworkBehaviour : MonoBehaviour, IRpcCaller, INetworkComponent
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
