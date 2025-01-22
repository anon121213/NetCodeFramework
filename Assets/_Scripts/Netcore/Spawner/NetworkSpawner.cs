using System.Reflection;
using _Scripts.Netcore.Data.Attributes;
using _Scripts.Netcore.Data.NetworkObjects;
using _Scripts.Netcore.NetworkComponents.RPCComponents;
using _Scripts.Netcore.RPCSystem;
using _Scripts.Netcore.RPCSystem.ProcessorsData;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace _Scripts.Netcore.Spawner
{
    public class NetworkSpawner : NetworkService, INetworkSpawner
    {
        private readonly IObjectResolver _resolver;
        private readonly NetworkObjectsConfig _networkObjectsConfig;
        private readonly MethodInfo _spawnMethodInfo;

        public NetworkSpawner(IObjectResolver resolver,
            NetworkObjectsConfig networkObjectsConfig)
        {
            _resolver = resolver;
            _networkObjectsConfig = networkObjectsConfig;
            _spawnMethodInfo = typeof(NetworkSpawner).GetMethod(nameof(SpawnClientRpc));
            
            RPCInvoker.RegisterRPCInstance<NetworkSpawner>(this);
        }

        public GameObject Spawn(GameObject prefab, Transform transform = null) => 
            SpawnLocal(prefab, Vector3.zero, Quaternion.identity, Vector3.one, transform);

        public GameObject Spawn(GameObject prefab, Vector3 position, Transform transform = null) => 
            SpawnLocal(prefab, position, Quaternion.identity, Vector3.one, transform);

        public GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, Transform transform = null) => 
            SpawnLocal(prefab, position, rotation, Vector3.one, transform);

        public GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, Vector3 scale, Transform transform = null) => 
            SpawnLocal(prefab, position, rotation, Vector3.one, transform);
        
        private GameObject SpawnLocal(GameObject prefab, Vector3 position, Quaternion rotation, Vector3 scale, Transform transform)
        {
            if (prefab.GetComponentInChildren<INetworkComponent>() == null)
                return null;

            if (!_networkObjectsConfig.TryGetNetworkObjectId(prefab, out int id))
                return null;

            GameObject go = _resolver.Instantiate(prefab, position, rotation, transform);
            go.transform.localScale = scale;

            RPCInvoker.InvokeServiceRPC<NetworkSpawner>(this, _spawnMethodInfo,
                NetProtocolType.Tcp, id, position, rotation, scale);

            return go;
        }

        [ClientRPC]
        public void SpawnClientRpc(int gameObjectId, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            if (!_networkObjectsConfig.GetNetworkObject(gameObjectId, out GameObject networkObject))
                return;

            GameObject networkObj = _resolver.Instantiate(networkObject, position, rotation);
            networkObj.transform.localScale = scale;
        }
    }
}
