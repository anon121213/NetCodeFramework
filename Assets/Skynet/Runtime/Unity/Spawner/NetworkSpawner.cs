using System;
using Skynet.Configuration;
using Skynet.Data.Attributes;
using Skynet.Diagnostics;
using Skynet.NetworkComponents;
using Skynet.RpcSystem;
using Skynet.Runner;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Skynet.Spawner
{
    public partial class NetworkSpawner : NetworkService, INetworkSpawner
    {
        private readonly NetworkObjectsConfig _networkObjectsConfig;
        private readonly INetworkObjectContainer _networkObjectContainer;
        private readonly ISkynetLogger _logger;

        public NetworkSpawner(
            NetworkRunner runner,
            NetworkObjectsConfig networkObjectsConfig,
            INetworkObjectContainer networkObjectContainer,
            ISkynetLogger logger) : base(runner)
        {
            _networkObjectsConfig = networkObjectsConfig;
            _networkObjectContainer = networkObjectContainer;
            _logger = logger;

            Runner.OnPlayerConnected += SyncTo;
            Runner.OnPlayerDisconnected += HandleClientDisconnected;
        }

        public NetworkObject Spawn(NetworkObject prefab, Transform transform = null, int ownerClientId = -1) =>
            SpawnLocal(prefab, Vector3.zero, Quaternion.identity, Vector3.one, transform, ownerClientId);

        public NetworkObject Spawn(NetworkObject prefab, Vector3 position, Transform transform = null, int ownerClientId = -1) =>
            SpawnLocal(prefab, position, Quaternion.identity, Vector3.one, transform, ownerClientId);

        public NetworkObject Spawn(NetworkObject prefab, Vector3 position, Quaternion rotation, Transform transform = null, int ownerClientId = -1) =>
            SpawnLocal(prefab, position, rotation, Vector3.one, transform, ownerClientId);

        public NetworkObject Spawn(NetworkObject prefab, Vector3 position, Quaternion rotation, Vector3 scale, Transform transform = null, int ownerClientId = -1) =>
            SpawnLocal(prefab, position, rotation, scale, transform, ownerClientId);

        public void Despawn(NetworkObject networkObject) =>
            DespawnLocal(networkObject);

        public void TransferOwnership(NetworkObject networkObject, int ownerClientId) =>
            TransferOwnershipLocal(networkObject, ownerClientId);
        
        private void HandleClientDisconnected(int disconnectedId)
        {
            if (!Runner.IsServer) return;
    
            foreach (var netObj in _networkObjectContainer.NetworkObjects)
            {
                if (netObj.OwnerClientId == disconnectedId)
                    TransferOwnership(netObj, NetworkObject.ServerOwnerId);
            }
        }
        
        private void TransferOwnershipLocal(NetworkObject networkObject, int ownerClientId)
        {
            if (!Runner.IsServer)
            {
                _logger.Exception(new InvalidOperationException("TransferOwnership is server-only"));
                return;
            }
            
            networkObject.SetOwner(ownerClientId);
            ChangeOwnership(networkObject.NetworkObjectId, ownerClientId);
        }

        private void SyncTo(int clientId)
        {
            foreach (var netObj in _networkObjectContainer.NetworkObjects)
            {
                ReplicateSpawnTo(clientId, netObj.PrefabId, netObj.NetworkObjectId, netObj.OwnerClientId,
                    netObj.transform.position, netObj.transform.rotation, netObj.transform.localScale);
            }
        }

        private NetworkObject SpawnLocal(NetworkObject prefab, Vector3 position, Quaternion rotation, Vector3 scale, Transform transform, int ownerId)
        {
            if (!Runner.IsServer)
                return null;

            if (prefab.GetComponentInChildren<INetworkComponent>() == null)
                return null;

            if (!_networkObjectsConfig.TryGetNetworkObjectId(prefab, out int id))
                return null;

            NetworkObject networkObject = Object.Instantiate(prefab, position, rotation, transform);
            networkObject.transform.localScale = scale;

            int uniqueId = networkObject.GetHashCode();
            networkObject.InitializeBehaviours(uniqueId, id, Runner, RpcRegistry, RpcSender);

            _networkObjectContainer.AddNetworkObject(networkObject);

            networkObject.OnNetworkReady();

            ReplicateSpawn(id, uniqueId, ownerId, position, rotation, scale);

            return networkObject;
        }

        private void DespawnLocal(NetworkObject networkObject)
        {
            if (!Runner.IsServer && networkObject.OwnerClientId != Runner.LocalPlayerId)
            {
                _logger.Warn($"Only server and owner: {networkObject.OwnerClientId} " +
                             $"can despawn NetworkObject id: {networkObject.NetworkObjectId}");
                return;
            }

            if (!_networkObjectContainer.RemoveNetworkObject(networkObject))
                return;

            int netId = networkObject.NetworkObjectId;

            networkObject.OnNetworkShutdown();
            Object.Destroy(networkObject.gameObject);
            ReplicateDespawn(netId);
        }

        private void ApplyIncomingSpawn(int prefabId, int uniqueId, int ownerId,
            Vector3 pos, Quaternion rot, Vector3 scale)
        {
            if (_networkObjectContainer.TryGetNetworkObject(uniqueId, out _))
                return;

            if (!_networkObjectsConfig.TryGetNetworkObject(prefabId, out NetworkObject prefab))
                return;

            NetworkObject networkObject = Object.Instantiate(prefab, pos, rot);
            networkObject.transform.localScale = scale;

            networkObject.InitializeBehaviours(uniqueId, prefabId, Runner, RpcRegistry, RpcSender);

            if (ownerId != NetworkObject.ServerOwnerId)
                networkObject.SetOwner(ownerId);

            _networkObjectContainer.AddNetworkObject(networkObject);

            networkObject.OnNetworkReady();
        }

        [ClientRpc]
        private void HandleReplicateDespawn(int networkObjectId)
        {
            if (!_networkObjectContainer.TryGetNetworkObject(networkObjectId, out var networkObject))
                return;

            networkObject.OnNetworkShutdown();

            Object.Destroy(networkObject.gameObject);
        }

        [ClientRpc]
        private void HandleReplicateSpawn(int prefabId, int uniqueId, int ownerId,
            Vector3 position, Quaternion rotation, Vector3 scale) =>
            ApplyIncomingSpawn(prefabId, uniqueId, ownerId, position, rotation, scale);

        [ClientRpc(target: RpcTarget.Client)]
        private void HandleReplicateSpawnTo(int prefabId, int uniqueId, int ownerId,
            Vector3 position, Quaternion rotation, Vector3 scale) =>
            ApplyIncomingSpawn(prefabId, uniqueId, ownerId, position, rotation, scale);

        [ClientRpc]
        private void HandleChangeOwnership(int uniqueId, int ownerId)
        {
            if (!_networkObjectContainer.TryGetNetworkObject(uniqueId, out var networkObject))
                return;
            
            networkObject.SetOwner(ownerId);
        }
        
        protected override void OnDispose()
        {
            Runner.OnPlayerConnected -= SyncTo;
            Runner.OnPlayerDisconnected -= HandleClientDisconnected;
        }
    }
}
