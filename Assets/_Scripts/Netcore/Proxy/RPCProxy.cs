using System;
using System.Buffers;
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
        private static readonly Dictionary<(Type, int), IRPCCaller> _callers = new();

        private static readonly ConcurrentQueue<byte[]> TcpSendQueue = new();
        private static readonly ConcurrentQueue<byte[]> UdpSendQueue = new();

        private static readonly ConcurrentQueue<byte[]> TcpReceiveQueue = new();
        private static readonly ConcurrentQueue<byte[]> UdpReceiveQueue = new();

        private static readonly CancellationTokenSource _cancellationTokenSource = new();
        
        private static INetworkRunner _runner;

        public static void Initialize(INetworkRunner runner)
        {
            _runner = (NetworkRunner)runner;

            ProcessTcpReceiveQueue(_cancellationTokenSource.Token).Forget();
            ProcessTcpReceiveQueue(_cancellationTokenSource.Token).Forget();
            ProcessTcpReceiveQueue(_cancellationTokenSource.Token).Forget();
            ProcessTcpReceiveQueue(_cancellationTokenSource.Token).Forget();
            ProcessTcpReceiveQueue(_cancellationTokenSource.Token).Forget();
            
            ProcessUdpReceiveQueue(_cancellationTokenSource.Token).Forget();
            ProcessUdpReceiveQueue(_cancellationTokenSource.Token).Forget();
            ProcessUdpReceiveQueue(_cancellationTokenSource.Token).Forget();
            ProcessUdpReceiveQueue(_cancellationTokenSource.Token).Forget();
            
            ProcessTcpSendQueue(_cancellationTokenSource.Token).Forget();
            ProcessTcpSendQueue(_cancellationTokenSource.Token).Forget();
            ProcessTcpSendQueue(_cancellationTokenSource.Token).Forget();
            ProcessTcpSendQueue(_cancellationTokenSource.Token).Forget();
            ProcessTcpSendQueue(_cancellationTokenSource.Token).Forget();
            
            ProcessUdpSendQueue(_cancellationTokenSource.Token).Forget();
            ProcessUdpSendQueue(_cancellationTokenSource.Token).Forget();
            ProcessUdpSendQueue(_cancellationTokenSource.Token).Forget();
            ProcessUdpSendQueue(_cancellationTokenSource.Token).Forget();
        }

        public static void RegisterRPCInstance<T>(NetworkService caller) where T : IRPCCaller
        {
            caller.InitializeNetworkService();
            _callers[(typeof(T), caller.InstanceId)] = caller;
        }

        public static void RegisterRPCInstance<T>(NetworkBehaviour caller) where T : IRPCCaller
        {
            caller.InitializeNetworkBehaviour();
            _callers[(typeof(T), caller.InstanceId)] = caller;
        }

        public static bool TryInvokeBehaviourRPC<TObject>(NetworkBehaviour networkBehaviour, MethodInfo methodInfo, 
            ProtocolType protocolType, params object[] parameters) where TObject : NetworkBehaviour =>
                InvokeRPC<TObject>(networkBehaviour.InstanceId, methodInfo, protocolType, parameters);

        public static bool TryInvokeServiceRPC<TObject>(NetworkService networkService, MethodInfo methodInfo,
            ProtocolType protocolType, params object[] parameters) where TObject : NetworkService =>
                InvokeRPC<TObject>(networkService.InstanceId, methodInfo, protocolType, parameters);

        private static bool InvokeRPC<TObject>(int instanceID, MethodInfo methodInfo, ProtocolType protocolType,
            params object[] parameters) where TObject : class
        {
            if (methodInfo.GetCustomAttribute<ClientRPC>() == null &&
                methodInfo.GetCustomAttribute<ServerRPC>() == null)
            {
                Debug.LogError($"Method: {methodInfo.Name} must have RPC attributes.");
                return false;
            }

            if (!_callers.ContainsKey((typeof(TObject), instanceID)))
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

        public static async UniTaskVoid ListenForTcpRpcCalls(Socket socket, CancellationToken cancellationToken)
        {
            byte[] buffer = ArrayPool<byte>.Shared.Rent(1024 * 64); 
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), SocketFlags.None);

                        if (result <= 0)
                            continue;
                        
                        var receivedData = new byte[result];
                        Array.Copy(buffer, receivedData, result);
                        TcpReceiveQueue.Enqueue(receivedData);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"TCP Receive Exception: {ex.Message}");
                        await UniTask.Delay(60, cancellationToken: cancellationToken);
                    }
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }


        public static async UniTaskVoid ListenForUdpRpcCalls(Socket socket, IPEndPoint ipEndPoint, CancellationToken cancellationToken)
        {
            byte[] buffer = ArrayPool<byte>.Shared.Rent(1024 * 128); 
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        var result = await socket.ReceiveFromAsync(new ArraySegment<byte>(buffer), SocketFlags.None, ipEndPoint);
                        
                        if (result.ReceivedBytes <= 0)
                            continue;
                        
                        var receivedData = new byte[result.ReceivedBytes];
                        Array.Copy(buffer, receivedData, result.ReceivedBytes);
                        UdpReceiveQueue.Enqueue(receivedData);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"UDP Receive Exception: {ex.Message}");
                        await UniTask.Delay(60, cancellationToken: cancellationToken); 
                    }
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
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

            if (!_callers.TryGetValue((callerType, message.InstanceId), out IRPCCaller rpcCaller))
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
    
    public abstract class NetworkService : IRPCCaller
    {
        private static int _instanceCounter;

        public int InstanceId { get; private set; } // Только базовый класс может задавать.

        public void InitializeNetworkService()
        {
            InstanceId = Interlocked.Increment(ref _instanceCounter);
        }
    }
    
    public abstract class NetworkBehaviour: MonoBehaviour, IRPCCaller
    {
        private static int _instanceCounter;

        public int InstanceId { get; private set; } // Только базовый класс может задавать.

        public void InitializeNetworkBehaviour()
        {
            InstanceId = Interlocked.Increment(ref _instanceCounter);
        }
    }
}