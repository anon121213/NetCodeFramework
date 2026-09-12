using System;
using System.Linq;
using System.Reflection;
using _Scripts.Netcore.RPCSystem.Processors;
using Skynet.Data.Attributes;
using Skynet.Data.Message;
using Skynet.NetworkComponents.RPCComponents;
using Skynet.RPCSystem.Callers;
using Skynet.RPCSystem.Processors;
using Skynet.RPCSystem.ProcessorsData;
using MessagePack;
using UnityEngine;

namespace Skynet.RPCSystem
{
    public static class RPCInvoker
    {
        private static ICallerService _callerService;
        private static IRPCSendProcessor _sendProcessor;

        public static void Initialize(IRPCSendProcessor sendProcessor,
            ICallerService callerService)
        {
            _sendProcessor = sendProcessor;
            _callerService = callerService;
        }
        
        public static void RegisterRPCInstance<T>(NetworkService caller) where T : IRPCCaller => 
            _callerService.AddCaller(typeof(T), caller);

        public static void RegisterRPCInstance<T>(NetworkBehaviour caller) where T : IRPCCaller => 
            _callerService.AddCaller(typeof(T), caller);

        public static void InvokeBehaviourRPC<TObject>(NetworkBehaviour networkBehaviour, MethodInfo methodInfo,
            NetProtocolType protocolType, params object[] parameters) where TObject : NetworkBehaviour =>
            InvokeRPC<TObject>(networkBehaviour.InstanceId, CallerTypes.Behaviour, methodInfo, protocolType, parameters);

        public static void InvokeServiceRPC<TObject>(NetworkService networkService, MethodInfo methodInfo,
            NetProtocolType protocolType, params object[] parameters) where TObject : NetworkService =>
            InvokeRPC<TObject>(networkService.InstanceId, CallerTypes.Service, methodInfo, protocolType, parameters);
        
        private static void InvokeRPC<TObject>(int instanceID, CallerTypes callerType, MethodInfo methodInfo, NetProtocolType protocolType,
            params object[] parameters) where TObject : class
        {
            if (methodInfo.GetCustomAttribute<ClientRPC>() == null &&
                methodInfo.GetCustomAttribute<ServerRPC>() == null)
            {
                Debug.LogError($"Method: {methodInfo.Name} must have RPC attributes.");
                return;
            }

            if (!_callerService.CallerServices.ContainsKey(new CallerKey(typeof(TObject), instanceID)) &&
                !_callerService.CallerBehaviours.ContainsKey(new CallerKey(typeof(TObject), instanceID)))
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
                InstanceId = instanceID,
                CallerType = callerType
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
        
        private static void EnqueueMessage(NetProtocolType protocolType, byte[] data)
        {
            switch (protocolType)
            {
                case NetProtocolType.Tcp:
                    _sendProcessor.TcpSendQueue.Enqueue(data);
                    break;
                case NetProtocolType.Udp:
                    _sendProcessor.UdpSendQueue.Enqueue(data);
                    break;
            }
        }

        private static byte[] SerializeMessage(RpcMessage message) =>
            MessagePackSerializer.Serialize(message);
    }
}