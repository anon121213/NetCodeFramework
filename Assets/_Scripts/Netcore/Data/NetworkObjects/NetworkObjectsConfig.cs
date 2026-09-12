using System.Collections.Generic;
using Skynet.NetworkComponents.RpcComponents;
using UnityEngine;

namespace Skynet.Data.NetworkObjects
{
    [CreateAssetMenu(fileName = "NetworkObjectsConfig", menuName = "Skynet/NetworkObjectsConfig")]
    public class NetworkObjectsConfig : ScriptableObject
    {
        [SerializeField] private List<NetworkObject> _networkObjects = new ();

        public bool TryGetNetworkObject(int prefabId, out NetworkObject gameObject)
        {
            if (prefabId >= 0 && prefabId < _networkObjects.Count)
            {
                gameObject = _networkObjects[prefabId];
                return true;
            }
            
            Debug.LogError("Invalid PrefabId");
            gameObject = null;
            return false;
        }

        public bool TryGetNetworkObjectId(NetworkObject prefab, out int id)
        {
            int index = _networkObjects.FindIndex(obj => obj == prefab);

            id = index;

            if (index != -1)
                return true;

            Debug.LogError($"Prefab not found. Looking for: {prefab?.name} (id={prefab?.GetInstanceID()}). List has {_networkObjects.Count} items:");
            for (int i = 0; i < _networkObjects.Count; i++)
                Debug.LogError($"  [{i}] {_networkObjects[i]?.name} (id={_networkObjects[i]?.GetInstanceID()})");

            return false;
        }
    }
}