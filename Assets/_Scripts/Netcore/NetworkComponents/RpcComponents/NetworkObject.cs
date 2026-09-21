using Skynet.RpcSystem;
using Skynet.Runner;
using UnityEngine;

namespace Skynet.NetworkComponents.RpcComponents
{
    public class NetworkObject : MonoBehaviour, INetworkComponent
    {
        public const int ServerOwnerId = -1;

        [SerializeField] private NetworkBehaviour[] _networkBehaviours;
        [SerializeField] private bool _needToCollectBehaviours;

        public int NetworkObjectId { get; private set; }
        public int PrefabId { get; private set; }
        internal INetworkRunner NetworkRunner { get; private set; }
        
        /// <summary>
        /// Client id that owns this object and drives its authoritative state (transform, rigidbody, gameplay).
        /// <see cref="ServerOwnerId"/> (-1) means the server itself owns it — the default for anything the
        /// server spawns without an explicit owner.
        /// </summary>
        public int OwnerClientId { get; private set; } = ServerOwnerId;

        public bool IsOwner {
            get {
                if (OwnerClientId == ServerOwnerId)
                    return NetworkRunner.IsServer;
                return OwnerClientId == NetworkRunner.LocalPlayerId;
            }
        }
        
        private void Awake()
        {
            if (_needToCollectBehaviours)
                _networkBehaviours = GetComponentsInChildren<NetworkBehaviour>();
        }

        public void SetOwner(int ownerClientId) => OwnerClientId = ownerClientId;

        public void InitializeBehaviours(int networkObjectUniqueId, int prefabId, INetworkRunner networkRunner,
            IRpcHandlerRegistry rpcRegistry, IRpcSender rpcSender)
        {
            NetworkObjectId = networkObjectUniqueId;
            PrefabId = prefabId;
            NetworkRunner = networkRunner;
            
            for (int i = 0; i < _networkBehaviours.Length; i++)
            {
                var behaviour = _networkBehaviours[i];
                var hash = NetworkHashHelper.ComputeIdForComponents(NetworkObjectId, behaviour.GetType(), i);

                if (!behaviour.TryInitNetworkId(hash))
                {
                    Debug.LogError($"Failed to init network object {GetType().FullName}. Hash: {hash}");
                    continue;
                }

                behaviour.InitializeRpc(rpcRegistry, rpcSender);
            }
        }

        public virtual void OnNetworkReady()
        {
            foreach (var networkBehaviour in _networkBehaviours) 
                networkBehaviour.OnNetworkReady();
        }

        public virtual void OnNetworkUpdate(uint tick)
        {
            foreach (var networkBehaviour in _networkBehaviours) 
                networkBehaviour.OnNetworkUpdate(tick);
        }

        public virtual void OnNetworkShutdown()
        {
            foreach (var networkBehaviour in _networkBehaviours) 
                networkBehaviour.OnNetworkShutdown();
        }
    }
}