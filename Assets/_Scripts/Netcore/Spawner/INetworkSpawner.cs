using Skynet.NetworkComponents.RpcComponents;
using UnityEngine;

namespace Skynet.Spawner
{
    public interface INetworkSpawner
    {
        NetworkObject Spawn(NetworkObject prefab, Transform transform = null);
        NetworkObject Spawn(NetworkObject prefab, Vector3 position, Transform transform = null);
        NetworkObject Spawn(NetworkObject prefab, Vector3 position, Quaternion rotation, Transform transform = null);
        NetworkObject Spawn(NetworkObject prefab, Vector3 position, Quaternion rotation, Vector3 scale, Transform transform = null);
        void Sync();
    }
}