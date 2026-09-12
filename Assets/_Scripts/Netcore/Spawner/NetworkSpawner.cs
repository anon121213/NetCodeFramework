using System.Reflection;
using Skynet.Data.Attributes;
using Skynet.Data.NetworkObjects;
using Skynet.NetworkComponents.RpcComponents;
using Skynet.RpcSystem;
using Skynet.RpcSystem.ProcessorsData;
using Skynet.Runner;
using Skynet.Spawner.ObjectsSyncer;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Skynet.Spawner
{
    public class NetworkSpawner : NetworkService, INetworkSpawner
    {
        private readonly IObjectResolver _resolver;
        private readonly NetworkObjectsConfig _networkObjectsConfig;
        private readonly INetworkObjectSyncer _networkObjectSyncer;
        private readonly INetworkRunner _networkRunner;
        private readonly MethodInfo _spawnMethodInfo;

        public NetworkSpawner(IObjectResolver resolver,
            NetworkObjectsConfig networkObjectsConfig,
            INetworkObjectSyncer networkObjectSyncer,
            INetworkRunner networkRunner)
        {
            _resolver = resolver;
            _networkObjectsConfig = networkObjectsConfig;
            _networkObjectSyncer = networkObjectSyncer;
            _networkRunner = networkRunner;
            _spawnMethodInfo = typeof(NetworkSpawner).GetMethod(nameof(SpawnClientRpc));
            
            RpcInvoker.RegisterRpcInstance<NetworkSpawner>(this);
        }

        public NetworkObject Spawn(NetworkObject prefab, Transform transform = null) => 
            SpawnLocal(prefab, Vector3.zero, Quaternion.identity, Vector3.one, transform);

        public NetworkObject Spawn(NetworkObject prefab, Vector3 position, Transform transform = null) => 
            SpawnLocal(prefab, position, Quaternion.identity, Vector3.one, transform);

        public NetworkObject Spawn(NetworkObject prefab, Vector3 position, Quaternion rotation, Transform transform = null) => 
            SpawnLocal(prefab, position, rotation, Vector3.one, transform);

        public NetworkObject Spawn(NetworkObject prefab, Vector3 position, Quaternion rotation, Vector3 scale, Transform transform = null) => 
            SpawnLocal(prefab, position, rotation, scale, transform);

        public void Sync() => 
            _networkObjectSyncer.Sync(this);

        private NetworkObject SpawnLocal(NetworkObject prefab, Vector3 position, Quaternion rotation, Vector3 scale, Transform transform)
        {
            if (!_networkRunner.IsServer)
                return null;
            
            if (prefab.GetComponentInChildren<INetworkComponent>() == null)
                return null;

            if (!_networkObjectsConfig.TryGetNetworkObjectId(prefab, out int id))
                return null;

            NetworkObject networkObject = _resolver.Instantiate(prefab, position, rotation, transform);
            networkObject.transform.localScale = scale;
            
            int uniqueId = networkObject.GetHashCode();
            networkObject.InitializeBehaviours(uniqueId);
            
            _networkObjectSyncer.AddNetworkObject(id, networkObject);

            RpcInvoker.InvokeServiceRpc<NetworkSpawner>(this, _spawnMethodInfo,
                NetProtocolType.Tcp, id, uniqueId, position, rotation, scale);

            return networkObject;
        }

        [ClientRpc]
        public void SpawnClientRpc(int prefabId, int uniqueId, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            if (_networkObjectSyncer.CheckSyncObject(prefabId, uniqueId))
                return;
            
            if (!_networkObjectsConfig.TryGetNetworkObject(prefabId, out NetworkObject prefab))
                return;

            NetworkObject networkObject = _resolver.Instantiate(prefab, position, rotation);
            networkObject.transform.localScale = scale;
            
            networkObject.InitializeBehaviours(uniqueId);
            
            _networkObjectSyncer.AddNetworkObject(prefabId, networkObject);
        }
    }
}
