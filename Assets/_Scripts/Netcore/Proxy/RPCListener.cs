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
using _Scripts.Netcore.NetworkComponents.RootComponents;
using _Scripts.Netcore.Runner;
using Cysharp.Threading.Tasks;
using MessagePack;
using UnityEngine;

namespace _Scripts.Netcore.Proxy
{
    public class RPCListener : IRpcListener, IDisposable
    {
        private readonly Dictionary<(Type, int), IRPCCaller> _callers = new();

        private readonly ConcurrentQueue<byte[]> TcpReceiveQueue = new();
        private readonly ConcurrentQueue<byte[]> UdpReceiveQueue = new();

        private readonly List<CancellationTokenSource> _tcpCancellationTokens = new();
        private readonly List<CancellationTokenSource> _udpCancellationTokens = new();

        public void Initialize()
        {
            for (int i = 0; i < 5; i++)
            {
                var tcpTokenSource = new CancellationTokenSource();
                _tcpCancellationTokens.Add(tcpTokenSource);
                ProcessTcpReceiveQueue(tcpTokenSource.Token).Forget();
            }

            for (int i = 0; i < 5; i++)
            {
                var udpTokenSource = new CancellationTokenSource();
                _udpCancellationTokens.Add(udpTokenSource);
                ProcessUdpReceiveQueue(udpTokenSource.Token).Forget();
            }
        }

        public void AddCaller(Type type, NetworkService service)
        {
            _callers[(type, service.InstanceId)] = service;
        }
        
        public void AddCaller(Type type, NetworkBehaviour service)
        {
            _callers[(type, service.InstanceId)] = service;
        }
        
        public async UniTaskVoid ListenForTcpRpcCalls(Socket socket, CancellationToken cancellationToken)
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


        public async UniTaskVoid ListenForUdpRpcCalls(Socket socket, IPEndPoint ipEndPoint,
            CancellationToken cancellationToken)
        {
            byte[] buffer = ArrayPool<byte>.Shared.Rent(1024 * 128);
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        var result = await socket.ReceiveFromAsync(new ArraySegment<byte>(buffer), SocketFlags.None,
                            ipEndPoint);

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

        private async UniTaskVoid ProcessTcpReceiveQueue(CancellationToken cancellationToken)
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

        private async UniTaskVoid ProcessUdpReceiveQueue(CancellationToken cancellationToken)
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
        
        private void ProcessRpcMessage(Type callerType, RpcMessage message)
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

        private static RpcMessage DeserializeMessage(byte[] data) =>
            MessagePackSerializer.Deserialize<RpcMessage>(data);

        public void Dispose()
        {
            for (var i = 0; i < _tcpCancellationTokens.Count; i++)
                _tcpCancellationTokens[i].Dispose();

            for (int i = 0; i < _udpCancellationTokens.Count; i++)
                _tcpCancellationTokens[i].Dispose();
        }
    }

    public interface IRpcListener
    {
        void Initialize();
        void AddCaller(Type type, NetworkService service);
        void AddCaller(Type type, NetworkBehaviour service);
        UniTaskVoid ListenForTcpRpcCalls(Socket socket, CancellationToken cancellationToken);
        UniTaskVoid ListenForUdpRpcCalls(Socket socket, IPEndPoint ipEndPoint, CancellationToken cancellationToken);
    }
}