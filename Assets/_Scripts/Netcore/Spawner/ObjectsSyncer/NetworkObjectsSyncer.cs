using System.Collections.Generic;
using System.Reflection;
using _Scripts.Netcore.RPCSystem;
using _Scripts.Netcore.RPCSystem.ProcessorsData;
using UnityEngine;

namespace _Scripts.Netcore.Spawner.ObjectsSyncer
{
    public class NetworkObjectsSyncer : INetworkObjectSyncer
    {
        private readonly List<(int, GameObject)> _networkObjects = new();
        private  MethodInfo _spawnMethodInfo;

        public void AddNetworkObject(int objectId, GameObject gameObject)
        {
            _networkObjects.Add((objectId, gameObject));
        }

        public void RemoveNetworkObject(int objectId, GameObject gameObject)
        {
            
        }

        public void Sync(NetworkSpawner networkSpawner)
        {
            _spawnMethodInfo = typeof(NetworkSpawner).GetMethod("SpawnClientRpc");
            
            foreach (var networkObject in _networkObjects)
            {
                RPCInvoker.InvokeServiceRPC<NetworkSpawner>(networkSpawner, _spawnMethodInfo,
                    NetProtocolType.Tcp, networkObject.Item1, networkObject.Item2.transform.position,
                    networkObject.Item2.transform. rotation, networkObject.Item2.transform.localScale);
            }
        }
    }

    public interface INetworkObjectSyncer
    {
        void Sync(NetworkSpawner networkSpawner);
        void AddNetworkObject(int objectId, GameObject gameObject);
    }
}