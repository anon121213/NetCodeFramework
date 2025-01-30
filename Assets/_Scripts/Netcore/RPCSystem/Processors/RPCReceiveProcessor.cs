using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using System.Threading;
using _Scripts.Netcore.Data.Message;
using _Scripts.Netcore.NetworkComponents.RPCComponents;
using _Scripts.Netcore.RPCSystem.Callers;
using Cysharp.Threading.Tasks;
using MessagePack;
using UnityEngine;

namespace _Scripts.Netcore.RPCSystem.Processors
{
    public class RpcReceiveReceiveProcessor : IRpcReceiveProcessor
    {
        private readonly ICallerService _callerService;

        public ConcurrentQueue<byte[]> TcpReceiveQueue { get; } = new();
        public ConcurrentQueue<byte[]> UdpReceiveQueue { get; } = new();

        public RpcReceiveReceiveProcessor(ICallerService callerService) => 
            _callerService = callerService;

        public async UniTask ProcessTcpReceiveQueue(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (TcpReceiveQueue.TryDequeue(out var data)) 
                    ProcessReceivedData(data);

                await UniTask.Yield();
            }
        }

        public async UniTask ProcessUdpReceiveQueue(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (UdpReceiveQueue.TryDequeue(out var data))
                    ProcessReceivedData(data);

                await UniTask.Yield();
            }
        }

        private void ProcessReceivedData(byte[] data)
        {
            var message = DeserializeMessage(data);
            if (message != null)
                ProcessRpcMessage(Type.GetType(message.ClassType), message);
        }

        private void ProcessRpcMessage(Type callerType, RpcMessage message)
        {
            if (callerType == null)
                return;

            var method = GetRpcMethod(callerType, message);

            if (method == null)
                return;

            var parameters = ConvertParameters(message.Parameters, method.GetParameters());

            if (message.CallerType == CallerTypes.Behaviour)
            {
                if (_callerService.CallerBehaviours.TryGetValue((callerType, message.InstanceId), out IRPCCaller rpcCaller))
                    method.Invoke(rpcCaller, parameters);
                else
                    Debug.LogError($"You try invoke method: {method.Name} in {callerType} who not register as behaviour caller" );
            }
            else
            {
                if (_callerService.CallerServices.TryGetValue((callerType, message.InstanceId), out IRPCCaller rpcCaller))
                    method.Invoke(rpcCaller, parameters);
                else
                    Debug.LogError($"You try invoke method: {method.Name} in {callerType} who not register as service caller" );
            }
        }

        private MethodInfo GetRpcMethod(Type callerType, RpcMessage message)
        {
            var paramTypes = MessagePackSerializer.Deserialize<Type[]>(message.MethodParam);
            return callerType.GetMethods().FirstOrDefault(m =>
                m.Name == message.MethodName && ParametersMatch(m.GetParameters(), paramTypes));
        }

        private bool ParametersMatch(ParameterInfo[] parameterInfos, Type[] paramTypes)
        {
            if (parameterInfos.Length != paramTypes.Length)
                return false;

            return !parameterInfos.Where((t, i) =>
                t.ParameterType != paramTypes[i]).Any();
        }

        private object[] ConvertParameters(byte[][] serializedParameters, ParameterInfo[] parameterInfos)
        {
            var parameters = new object[serializedParameters.Length];

            for (int i = 0; i < serializedParameters.Length; i++)
                parameters[i] = MessagePackSerializer.Deserialize
                    (parameterInfos[i].ParameterType, serializedParameters[i]);

            return parameters;
        }

        private RpcMessage DeserializeMessage(byte[] data) =>
            MessagePackSerializer.Deserialize<RpcMessage>(data);
    }
}