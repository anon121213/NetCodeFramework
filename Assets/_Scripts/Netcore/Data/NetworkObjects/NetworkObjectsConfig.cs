using System.Collections.Generic;
using UnityEngine;

namespace _Scripts.Netcore.Data.NetworkObjects
{
    [CreateAssetMenu(menuName = "Network/Configs/NetworkObjects", fileName = "NetworkObjectsConfig")]
    public class NetworkObjectsConfig : ScriptableObject
    {
        [SerializeField] private List<GameObject> _NetworkObjects = new ();

        public bool GetNetworkObject(int PrefabId, out GameObject gameObject)
        {
            if (PrefabId >= 0 && PrefabId < _NetworkObjects.Count)
            {
                gameObject = _NetworkObjects[PrefabId];
                return true;
            }
            
            Debug.LogError("Invalid PrefabId");
            gameObject = null;
            return false;
        }

        public bool TryGetNetworkObjectId(GameObject prefab, out int id)
        {
            int index = _NetworkObjects.FindIndex(obj => obj == prefab);

            id = index;
            
            if (index != -1)
                return true;
            
            Debug.LogError("Prefab not found in the list");
            return false;

        }
    }
}