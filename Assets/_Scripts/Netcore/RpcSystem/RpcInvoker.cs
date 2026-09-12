using System;
using System.Linq;
using System.Reflection;
using Skynet.RpcSystem.Processors;
using Skynet.Data.Attributes;
using Skynet.Data.Message;
using Skynet.NetworkComponents.RpcComponents;
using Skynet.RpcSystem.Callers;
using Skynet.RpcSystem.Processors;
using Skynet.RpcSystem.ProcessorsData;
using MessagePack;
using UnityEngine;

namespace Skynet.RpcSystem
{
    public static class RpcInvoker
    {
        private static ICallerService _callerService;
        private static IRpcSendProcessor _sendProcessor;

        public static void Initialize(IRpcSendProcessor sendProcessor,
            ICallerService callerService)
        {
            _sendProcessor = sendProcessor;
            _callerService = callerService;
        }
        
        public static void RegisterRpcInstance<T>(NetworkService caller) where T : IRpcCaller => 
            _callerService.AddCaller(typeof(T), caller);

        public static void RegisterRpcInstance<T>(NetworkBehaviour caller) where T : IRpcCaller => 
            _callerService.AddCaller(typeof(T), caller);

        public static void InvokeBehaviourRpc<TObject>(NetworkBehaviour networkBehaviour, MethodInfo methodInfo,
            NetProtocolType protocolType, params object[] parameters) where TObject : NetworkBehaviour =>
            InvokeRPC<TObject>(networkBehaviour.InstanceId, CallerTypes.Behaviour, methodInfo, protocolType, parameters);

        public static void InvokeServiceRpc<TObject>(NetworkService networkService, MethodInfo methodInfo,
            NetProtocolType protocolType, params object[] parameters) where TObject : NetworkService =>
            InvokeRPC<TObject>(networkService.InstanceId, CallerTypes.Service, methodInfo, protocolType, parameters);
        
        private static void InvokeRPC<TObject>(int instanceID, CallerTypes callerType, MethodInfo methodInfo, NetProtocolType protocolType,
            params object[] parameters) where TObject : class
        {
            if (methodInfo.GetCustomAttribute<ClientRpc>() == null &&
                methodInfo.GetCustomAttribute<ServerRpc>() == null)
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