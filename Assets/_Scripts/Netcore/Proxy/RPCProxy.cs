using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using _Scripts.Netcore.Data;
using _Scripts.Netcore.Data.Attributes;
using _Scripts.Netcore.Runner;
using Cysharp.Threading.Tasks;
using MessagePack;
using UnityEngine;

namespace _Scripts.Netcore.Proxy
{
    public static class RpcProxy
    {
        private static readonly Dictionary<Type, IRPCCaller> _callers = new();
        private static INetworkRunner _runner;

        // Очереди сообщений
        private static readonly ConcurrentQueue<byte[]> TcpSendQueue = new();
        private static readonly ConcurrentQueue<byte[]> UdpSendQueue = new();

        private static readonly ConcurrentQueue<byte[]> TcpReceiveQueue = new();
        private static readonly ConcurrentQueue<byte[]> UdpReceiveQueue = new();

        private static readonly CancellationTokenSource _cancellationTokenSource = new();

        public static void Initialize(INetworkRunner runner)
        {
            _runner = (NetworkRunner)runner;

            ProcessTcpReceiveQueue(_cancellationTokenSource.Token).Forget();
            ProcessUdpReceiveQueue(_cancellationTokenSource.Token).Forget();
            
            ProcessTcpSendQueue(_cancellationTokenSource.Token).Forget();
            ProcessUdpSendQueue(_cancellationTokenSource.Token).Forget();
        }

        public static void RegisterRPCInstance<T>(IRPCCaller caller) where T : IRPCCaller =>
            _callers[typeof(T)] = caller;

        public static bool TryInvokeRPC<TObject>(MethodInfo methodInfo, ProtocolType protocolType,
            params object[] parameters) where TObject : class
        {
            if (methodInfo.GetCustomAttribute<ClientRPC>() == null &&
                methodInfo.GetCustomAttribute<ServerRPC>() == null)
            {
                Debug.LogError($"Method: {methodInfo.Name} must have RPC attributes.");
                return false;
            }

            if (!_callers.ContainsKey(typeof(TObject)))
            {
                Debug.LogError($"{typeof(TObject)} must be registered.");
                return false;
            }

            var serializedParameters = parameters.Select(param => MessagePackSerializer.Serialize(param)).ToArray();
            var serializedParamTypes = parameters.Select(param => param.GetType()).ToArray();
            var serializedParamTypesBytes = MessagePackSerializer.Serialize(serializedParamTypes);

            var message = new RpcMessage
            {
                MethodName = methodInfo.Name,
                Parameters = serializedParameters,
                ClassType = typeof(TObject).ToString(),
                MethodParam = serializedParamTypesBytes
            };

            byte[] data = SerializeMessage(message);

            try
            {
                EnqueueMessage(protocolType, data);
            }
            catch (Exception e)
            {
                Debug.LogError($"Error while queuing RPC: {e.Message}");
                return false;
            }

            return true;
        }

        private static void EnqueueMessage(ProtocolType protocolType, byte[] data)
        {
            switch (protocolType)
            {
                case ProtocolType.Tcp:
                    TcpSendQueue.Enqueue(data);
                    break;
                case ProtocolType.Udp:
                    UdpSendQueue.Enqueue(data);
                    break;
                default:
                    TcpSendQueue.Enqueue(data);
                    break;
            }
        }

        private static async UniTaskVoid ProcessTcpSendQueue(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (TcpSendQueue.TryDequeue(out var data))
                {
                    if (_runner.IsServer)
                    {
                        foreach (var socket in _runner.TcpClientSockets.Where(socket => socket.Connected))
                        {
                            try
                            {
                                await socket.SendAsync(new ArraySegment<byte>(data), SocketFlags.None);
                            }
                            catch (Exception ex)
                            {
                                Debug.LogError($"TCP Send Error: {ex.Message}");
                            }
                        }
                    }
                    else
                    {
                        if (_runner.TcpServerSocket.Connected)
                        {
                            try
                            {
                                await _runner.TcpServerSocket.SendAsync(new ArraySegment<byte>(data), SocketFlags.None);
                            }
                            catch (Exception ex)
                            {
                                Debug.LogError($"TCP Send Error: {ex.Message}");
                            }
                        }
                    }
                }

                await UniTask.Yield();
            }
        }

        private static async UniTaskVoid ProcessUdpSendQueue(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (UdpSendQueue.TryDequeue(out var data))
                {
                    if (_runner.IsServer)
                    {
                        foreach (var socket in _runner.UdpClientSockets)
                        {
                            try
                            {
                                if (socket.RemoteEndPoint is IPEndPoint remoteEndPoint)
                                    await socket.SendToAsync(new ArraySegment<byte>(data), SocketFlags.None,
                                            remoteEndPoint);
                            }
                            catch (Exception ex)
                            {
                                Debug.LogError($"UDP Send Error: {ex.Message}");
                            }
                        }
                    }
                    else
                    {
                        try
                        {
                            IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 5057);
                            // if (_runner.UdpServerSocket.RemoteEndPoint is IPEndPoint remoteEndPoint)
                            await _runner.UdpServerSocket.SendToAsync(new ArraySegment<byte>(data), SocketFlags.None,
                                remoteEndPoint);

                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"UDP Send Error: {ex.Message}");
                        }
                    }
                }

                await UniTask.Yield();
            }
        }

        public static async UniTaskVoid ListenForTcpRpcCalls(Socket socket)
        {
            byte[] buffer = new byte[1024 * 64];
            while (true)
            {
                try
                {
                    int bytesRead = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), SocketFlags.None);
                    if (bytesRead > 0)
                    {
                        TcpReceiveQueue.Enqueue(buffer.Take(bytesRead).ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"TCP Receive Exception: {ex.Message}");
                    await UniTask.Delay(1000);
                }
            }
        }

        public static async UniTaskVoid ListenForUdpRpcCalls(Socket socket, IPEndPoint ipEndPoint)
        {
            byte[] buffer = new byte[1024 * 128];
            while (true)
            {
                try
                {
                    var result =
                        await socket.ReceiveFromAsync(new ArraySegment<byte>(buffer), SocketFlags.None, ipEndPoint);
                    if (result.ReceivedBytes > 0)
                    {
                        UdpReceiveQueue.Enqueue(buffer.Take(result.ReceivedBytes).ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"UDP Receive Exception: {ex.Message}");
                    await UniTask.Delay(1000);
                }
            }
        }

        private static async UniTaskVoid ProcessTcpReceiveQueue(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (TcpReceiveQueue.TryDequeue(out var data))
                {
                    ProcessReceivedData(data);
                }

                await UniTask.Yield();
            }
        }

        private static async UniTaskVoid ProcessUdpReceiveQueue(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (UdpReceiveQueue.TryDequeue(out var data)) 
                    ProcessReceivedData(data);

                await UniTask.Yield();
            }
        }

        private static void ProcessReceivedData(byte[] data)
        {
            try
            {
                var message = DeserializeMessage(data);
                if (message != null) 
                    ProcessRpcMessage(Type.GetType(message.ClassType), message);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error processing received data: {ex.Message}");
            }
        }

        private static void ProcessRpcMessage(Type callerType, RpcMessage message)
        {
            if (callerType == null)
                return;

            var method = GetRpcMethod(callerType, message);

            if (method == null)
                return;

            var parameters = ConvertParameters(message.Parameters, method.GetParameters());

            if (!_callers.TryGetValue(callerType, out IRPCCaller rpcCaller))
                return;

            method.Invoke(rpcCaller, parameters);
        }

        private static MethodInfo GetRpcMethod(Type callerType, RpcMessage message)
        {
            var paramTypes = MessagePackSerializer.Deserialize<Type[]>(message.MethodParam);
            return callerType.GetMethods().FirstOrDefault(m =>
                m.Name == message.MethodName && ParametersMatch(m.GetParameters(), paramTypes));
        }

        private static bool ParametersMatch(ParameterInfo[] parameterInfos, Type[] paramTypes)
        {
            if (parameterInfos.Length != paramTypes.Length)
                return false;

            return !parameterInfos.Where((t, i) =>
                t.ParameterType != paramTypes[i]).Any();
        }

        private static object[] ConvertParameters(byte[][] serializedParameters, ParameterInfo[] parameterInfos)
        {
            var parameters = new object[serializedParameters.Length];
            
            for (int i = 0; i < serializedParameters.Length; i++)
                parameters[i] = MessagePackSerializer.Deserialize
                    (parameterInfos[i].ParameterType, serializedParameters[i]);

            return parameters;
        }

        private static byte[] SerializeMessage(RpcMessage message) =>
            MessagePackSerializer.Serialize(message);

        private static RpcMessage DeserializeMessage(byte[] data) =>
            MessagePackSerializer.Deserialize<RpcMessage>(data);
    }

    public interface IRPCCaller
    {
    }
}