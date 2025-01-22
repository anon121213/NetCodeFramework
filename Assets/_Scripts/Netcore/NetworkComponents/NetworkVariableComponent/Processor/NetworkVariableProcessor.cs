using System.Collections.Generic;
using System.Net.Sockets;
using System.Reflection;
using _Scripts.Netcore.Data.Attributes;
using _Scripts.Netcore.NetworkComponents.NetworkVariableComponent.Data;
using _Scripts.Netcore.NetworkComponents.RPCComponents;
using _Scripts.Netcore.RPCSystem;
using _Scripts.Netcore.RPCSystem.DynamicProcessor;
using _Scripts.Netcore.RPCSystem.ProcessorsData;
using _Scripts.Netcore.Runner;
using MessagePack;
using UnityEngine;

namespace _Scripts.Netcore.NetworkComponents.NetworkVariableComponent.Processor
{
    public class NetworkVariableProcessor : NetworkService
    {
        private readonly Dictionary<string, object> _networkVariables = new();
        
        private INetworkRunner _networkRunner;

        private static NetworkVariableProcessor _instance;

        public static NetworkVariableProcessor Instance
        {
            get
            {
                if (_instance != null)
                    return _instance;

                _instance = new NetworkVariableProcessor();
                return _instance;
            }
        }

        public void Initialize(INetworkRunner networkRunner)
        {
            _networkRunner = networkRunner;
            RPCInvoker.RegisterRPCInstance<NetworkVariableProcessor>(this);
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

            var message = new NetworkVariableMessage
            {
                VariableName = name,
                SerializedValue = MessagePackSerializer.Serialize(newValue)
            };

            MethodInfo methodInfo = typeof(NetworkVariableProcessor).GetMethod(nameof(SyncVariableOnClients));
            RPCInvoker.InvokeServiceRPC<NetworkVariableProcessor>(this, methodInfo, NetProtocolType.Tcp, message);
            
            return true;
        }

        [ServerRPC]
        public void SyncVariableRPC(NetworkVariableMessage message)
        {
            if (!_networkVariables.TryGetValue(message.VariableName, out var variable))
                return;

            var variableType = variable.GetType().GetGenericArguments()[0];
            var deserializedValue = MessagePackSerializer.Deserialize(variableType, message.SerializedValue);
            var method = variable.GetType().GetProperty(nameof(INetworkVariableRoot<object>.ValueRoot))?.SetMethod;

            method?.Invoke(variable, new[] { deserializedValue });

            var clientMethod = typeof(NetworkVariableProcessor).GetMethod(nameof(SyncVariableOnClients));
            RPCInvoker.InvokeServiceRPC<NetworkVariableProcessor>(this, clientMethod, NetProtocolType.Tcp, message);
        }

        [ClientRPC]
        public void SyncVariableOnClients(NetworkVariableMessage message)
        {
            if (!_networkVariables.TryGetValue(message.VariableName, out var variable))
                return;
            
            var variableType = variable.GetType().GetGenericArguments()[0];
            var deserializedValue = MessagePackSerializer.Deserialize(variableType, message.SerializedValue);
            
            var property = variable.GetType().GetProperty(nameof(INetworkVariableRoot<object>.ValueRoot));
            property?.SetValue(variable, deserializedValue);
        }
    }
}
