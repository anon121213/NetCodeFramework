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
using _Scripts.Netcore.NetworkComponents.RootComponents;
using _Scripts.Netcore.Runner;
using Cysharp.Threading.Tasks;
using MessagePack;
using UnityEngine;

namespace _Scripts.Netcore.Proxy
{
    public class RPCInvoker
    {
        private static readonly Dictionary<(Type, int), IRPCCaller> _callers = new();
        
        private static readonly ConcurrentQueue<byte[]> TcpSendQueue = new();
        private static readonly ConcurrentQueue<byte[]> UdpSendQueue = new();

        private static readonly List<CancellationTokenSource> _tcpSendCancellationTokens = new();
        private static readonly List<CancellationTokenSource> _udpSendCancellationTokens = new();
        
        private static INetworkRunner _runner;
        private static IRpcListener _rpcListener; 
        
        public static void Initialize(INetworkRunner networkRunner,
            IRpcListener rpcListener)
        {
            _runner = networkRunner;
            _rpcListener = rpcListener;
            
            for (int i = 0; i < 5; i++)
            {
                var tcpSendTokenSource = new CancellationTokenSource();
                _tcpSendCancellationTokens.Add(tcpSendTokenSource);
                ProcessTcpSendQueue(tcpSendTokenSource.Token).Forget();
            }

            for (int i = 0; i < 5; i++)
            {
                var udpSendTokenSource = new CancellationTokenSource();
                _udpSendCancellationTokens.Add(udpSendTokenSource);
                ProcessUdpSendQueue(udpSendTokenSource.Token).Forget();
            }
        }
        
        public static void RegisterRPCInstance<T>(NetworkService caller) where T : IRPCCaller
        {
            caller.InitializeNetworkService();
            _callers[(typeof(T), caller.InstanceId)] = caller;
            _rpcListener.AddCaller(typeof(T), caller);
        }

        public static void RegisterRPCInstance<T>(NetworkBehaviour caller) where T : IRPCCaller
        {
            caller.InitializeNetworkBehaviour();
            _callers[(typeof(T), caller.InstanceId)] = caller;
            _rpcListener.AddCaller(typeof(T), caller);
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

        private static byte[] SerializeMessage(RpcMessage message) =>
            MessagePackSerializer.Serialize(message);

        public static void Dispose()
        {
            for (var i = 0; i < _tcpSendCancellationTokens.Count; i++)
                _tcpSendCancellationTokens[i].Dispose();

            for (int i = 0; i < _udpSendCancellationTokens.Count; i++)
                _tcpSendCancellationTokens[i].Dispose();
        }
    }
}