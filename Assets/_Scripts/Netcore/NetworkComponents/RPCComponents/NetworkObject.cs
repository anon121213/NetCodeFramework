using UnityEngine;

namespace Skynet.NetworkComponents.RPCComponents
{
    public class NetworkObject : MonoBehaviour, INetworkComponent
    {
        [SerializeField] private NetworkBehaviour[] _networkBehaviours;
        [SerializeField] private bool _needToCollectBehaviours;

        public int NetworkObjectId { get; private set; }

        private void Awake()
        {
            if (_needToCollectBehaviours) 
                _networkBehaviours = GetComponentsInChildren<NetworkBehaviour>();
        }
        
        public void InitializeBehaviours(int networkObjectUniqueId)
        {
            NetworkObjectId = networkObjectUniqueId;
            
            for (int i = 0; i < _networkBehaviours.Length; i++)
            {
                var hash = NetworkHashHelper.ComputeIdForComponents(NetworkObjectId, _networkBehaviours[i].GetType(), i);
                
                if (!_networkBehaviours[i].TryInitNetworkId(hash)) 
                    Debug.LogError($"Failed to init network object {GetType().FullName}. Hash: {hash}");
            }
        }
    }
}