using System.Collections.Generic;
using Skynet.NetworkComponents;

namespace Skynet.Spawner
{
    public interface INetworkObjectContainer
    {
        IReadOnlyList<NetworkObject> NetworkObjects { get; }
        bool AddNetworkObject(NetworkObject networkObject);
        bool RemoveNetworkObject(NetworkObject networkObject);
        bool TryGetNetworkObject(int networkObjectId, out NetworkObject networkObject);
    }
}
