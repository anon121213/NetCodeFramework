using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Skynet.NetworkComponents.RpcComponents;
using Skynet.RpcSystem;
using Skynet.RpcSystem.ProcessorsData;

namespace Skynet.Spawner.ObjectsSyncer
{
    public class NetworkObjectsSyncer : INetworkObjectSyncer
    {
        private readonly List<(int, int, NetworkObject)> _networkObjects = new();
        private MethodInfo _spawnMethodInfo;
        
        public void AddNetworkObject(int prefabId, NetworkObject networkObject)
        {
            _networkObjects.Add((prefabId, networkObject.NetworkObjectId, networkObject));
        }

        public void RemoveNetworkObject(NetworkObject networkObject)
        {
        }
        
        public bool CheckSyncObject(int prefabId, int uniqueId) =>
            _networkObjects.Any(networkObject => 
                networkObject.Item1 == prefabId && networkObject.Item2 == uniqueId);

        public void Sync(NetworkSpawner networkSpawner)
        {
            _spawnMethodInfo = typeof(NetworkSpawner).GetMethod("SpawnClientRpc");
            
            foreach (var networkObject in _networkObjects)
                RpcInvoker.InvokeServiceRpc<NetworkSpawner>(networkSpawner, _spawnMethodInfo,
                    NetProtocolType.Tcp, networkObject.Item1, networkObject.Item2, networkObject.Item3.transform.position,
                    networkObject.Item3.transform.rotation, networkObject.Item3.transform.localScale);
        }
    }

    public interface INetworkObjectSyncer
    {
        void Sync(NetworkSpawner networkSpawner);
        void AddNetworkObject(int prefabId, NetworkObject gameObject);
        bool CheckSyncObject(int prefabId, int uniqueId);
    }
}