using System;
using Skynet.RpcSystem;
using Skynet.Runner;
using UnityEngine;

namespace Skynet.NetworkComponents
{
    public class NetworkObject : MonoBehaviour, INetworkComponent
    {
        public const int ServerOwnerId = -1;

        [SerializeField] private NetworkBehaviour[] _networkBehaviours;
        [SerializeField] private bool _needToCollectBehaviours;

        public int NetworkObjectId { get; private set; }
        public int PrefabId { get; private set; }
        internal NetworkRunner NetworkRunner { get; private set; }
        
        public int OwnerClientId { get; private set; } = ServerOwnerId;
        
        public event Action<int /*old*/, int /*new*/> OwnerChanged;

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

        internal void SetOwner(int newOwnerId)
        {
            if (OwnerClientId == newOwnerId) return;
            int oldOwner = OwnerClientId;
            OwnerClientId = newOwnerId;
    
            foreach (var b in _networkBehaviours)
                b.OnOwnerChanged(oldOwner, newOwnerId);
            
            OwnerChanged?.Invoke(oldOwner, newOwnerId);
        }

        public void InitializeBehaviours(int networkObjectUniqueId, int prefabId, NetworkRunner networkRunner,
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
            {
                networkBehaviour.OnNetworkUpdate(tick);
                networkBehaviour.SyncVariables(tick);
            }
        }

        public virtual void OnNetworkShutdown()
        {
            foreach (var networkBehaviour in _networkBehaviours) 
                networkBehaviour.OnNetworkShutdown();
        }
    }
}