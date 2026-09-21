using System.Collections.Generic;
using Skynet.Data.Attributes;
using Skynet.NetworkComponents.NetworkVariableComponent.Data;
using Skynet.NetworkComponents.RpcComponents;
using Skynet.Runner;
using UnityEngine;

namespace Skynet.NetworkComponents.NetworkVariableComponent.Processor
{
    // NOTE (Chunk 4c-4): the NetworkVariable subsystem is on ice until the source generator
    // lands in Chunk 4d and gives us a real RPC pipeline for these messages. The class stays
    // so NetworkVariable<T> compiles; TrySyncVariable is a no-op that just updates the local
    // dictionary. Do NOT register this in the DI container yet.
    public partial class NetworkVariableProcessor : NetworkService
    {
        private readonly Dictionary<string, object> _networkVariables = new();

        private INetworkRunner _networkRunner;

        private static NetworkVariableProcessor _instance;

        public static NetworkVariableProcessor Instance => _instance ??= new NetworkVariableProcessor();

        public void Initialize(INetworkRunner networkRunner)
        {
            _networkRunner = networkRunner;
            // TODO(Chunk 4d): register RPC handlers via source generator.
        }

        public void RegisterNetworkVariable<T>(string name, NetworkVariable<T> networkVariable)
        {
            if (_networkVariables.TryAdd(name, networkVariable))
                return;

            Debug.LogWarning($"Variable {name} is already registered.");
        }

        public NetworkVariable<T> GetNetworkVariable<T>(string name)
        {
            if (_networkVariables.TryGetValue(name, out var variable))
                return variable as NetworkVariable<T>;

            return null;
        }

        public bool TrySyncVariable<T>(string name, T newValue)
        {
            if (!_networkRunner.IsServer)
            {
                Debug.LogWarning("Only the server can modify network variables.");
                return false;
            }

            if (_networkVariables.TryGetValue(name, out var variable))
                if (variable is INetworkVariableRoot<T> networkVariable)
                    networkVariable.ValueRoot = newValue;

            // TODO(Chunk 4d): broadcast NetworkVariableMessage to clients via generated sender.
            return true;
        }

        [ServerRpc]
        public void SyncVariableRPC(NetworkVariableMessage message)
        {
            // TODO(Chunk 4d): reintroduce sync via generated dispatcher.
        }

        [ClientRpc]
        public void SyncVariableOnClients(NetworkVariableMessage message)
        {
            // TODO(Chunk 4d): reintroduce sync via generated dispatcher.
        }
    }
}
