using System;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using _Scripts.Netcore.Data;
using _Scripts.Netcore.Data.Attributes;
using _Scripts.Netcore.NetworkComponents.RootComponents;
using _Scripts.Netcore.Proxy.Callers;
using _Scripts.Netcore.Proxy.Processors;
using MessagePack;
using UnityEngine;

namespace _Scripts.Netcore.Proxy
{
    public class RPCInvoker
    {
        private static ICallerService _callerService;
        private static IRPCSendProcessor _sendProcessor;

        public static void Initialize(IRPCSendProcessor sendProcessor,
            ICallerService callerService)
        {
            _sendProcessor = sendProcessor;
            _callerService = callerService;
        }
        
        public static void RegisterRPCInstance<T>(NetworkService caller) where T : IRPCCaller
        {
            caller.InitializeNetworkService();
            _callerService.AddCaller(typeof(T), caller);
        }

        public static void RegisterRPCInstance<T>(NetworkBehaviour caller) where T : IRPCCaller
        {
            caller.InitializeNetworkBehaviour();
            _callerService.AddCaller(typeof(T), caller);
        }
        
        public static void InvokeBehaviourRPC<TObject>(NetworkBehaviour networkBehaviour, MethodInfo methodInfo,
            ProtocolType protocolType, params object[] parameters) where TObject : NetworkBehaviour =>
            InvokeRPC<TObject>(networkBehaviour.InstanceId, methodInfo, protocolType, parameters);

        public static void InvokeServiceRPC<TObject>(NetworkService networkService, MethodInfo methodInfo,
            ProtocolType protocolType, params object[] parameters) where TObject : NetworkService =>
            InvokeRPC<TObject>(networkService.InstanceId, methodInfo, protocolType, parameters);

        private static void InvokeRPC<TObject>(int instanceID, MethodInfo methodInfo, ProtocolType protocolType,
            params object[] parameters) where TObject : class
        {
            if (methodInfo.GetCustomAttribute<ClientRPC>() == null &&
                methodInfo.GetCustomAttribute<ServerRPC>() == null)
            {
                Debug.LogError($"Method: {methodInfo.Name} must have RPC attributes.");
                return;
            }

            if (!_callerService.Callers.ContainsKey((typeof(TObject), instanceID)))
            {
                Debug.LogError($"{typeof(TObject)} must be registered.");
                return;
            }

            var serializedParameters = parameters.Select(param => MessagePackSerializer.Serialize(param)).ToArray();
            var serializedParamTypes = parameters.Select(param => param.GetType()).ToArray();
            var serializedParamTypesBytes = MessagePackSerializer.Serialize(serializedParamTypes);

            var message = new RpcMessage
            {
                MethodName = methodInfo.Name,
                Parameters = serializedParameters,
                ClassType = typeof(TObject).ToString(),
                MethodParam = serializedParamTypesBytes,
                InstanceId = instanceID
            };

            byte[] data = SerializeMessage(message);

            try
            {
                EnqueueMessage(protocolType, data);
            }
            catch (Exception e)
            {
                Debug.LogError($"Error while queuing RPC: {e.Message}");
            }
        }
        
        private static void EnqueueMessage(ProtocolType protocolType, byte[] data)
        {
            switch (protocolType)
            {
                case ProtocolType.Tcp:
                    _sendProcessor.TcpSendQueue.Enqueue(data);
                    break;
                case ProtocolType.Udp:
                    _sendProcessor.UdpSendQueue.Enqueue(data);
                    break;
                default:
                    _sendProcessor.TcpSendQueue.Enqueue(data);
                    break;
            }
        }

        private static byte[] SerializeMessage(RpcMessage message) =>
            MessagePackSerializer.Serialize(message);
    }
}