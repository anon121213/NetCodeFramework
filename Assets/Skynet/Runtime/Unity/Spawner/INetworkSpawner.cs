using Skynet.NetworkComponents;
using UnityEngine;

namespace Skynet.Spawner
{
    public interface INetworkSpawner
    {
        NetworkObject Spawn(NetworkObject prefab, Transform transform = null, int ownerClientId = -1);
        NetworkObject Spawn(NetworkObject prefab, Vector3 position, Transform transform = null, int ownerClientId = -1);
        NetworkObject Spawn(NetworkObject prefab, Vector3 position, Quaternion rotation, Transform transform = null, int ownerClientId = -1);
        NetworkObject Spawn(NetworkObject prefab, Vector3 position, Quaternion rotation, Vector3 scale, Transform transform = null, int ownerClientId = -1);
        void Despawn(NetworkObject networkObject);
        void TransferOwnership(NetworkObject networkObject, int ownerClientId);
    }
}