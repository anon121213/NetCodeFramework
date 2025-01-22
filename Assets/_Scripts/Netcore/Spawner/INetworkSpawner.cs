using UnityEngine;

namespace _Scripts.Netcore.Spawner
{
    public interface INetworkSpawner
    {
        GameObject Spawn(GameObject prefab, Transform transform = null);
        GameObject Spawn(GameObject prefab, Vector3 position, Transform transform = null);
        GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, Transform transform = null);
        GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, Vector3 scale, Transform transform = null);
    }
}