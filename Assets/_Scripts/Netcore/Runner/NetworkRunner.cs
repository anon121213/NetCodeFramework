using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using _Scripts.Netcore.Data.Attributes;
using _Scripts.Netcore.Data.ConnectionData;
using _Scripts.Netcore.Initializer;
using _Scripts.Netcore.NetworkComponents.RPCComponents;
using _Scripts.Netcore.RPCSystem;
using _Scripts.Netcore.RPCSystem.ProcessorsData;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace _Scripts.Netcore.Runner
{
    public class NetworkRunner : NetworkService, INetworkRunner, IDisposable
    {
        private readonly CancellationTokenSource _cts = new();
        private readonly IRpcListener _rpcListener;
        private readonly MethodInfo _methodInfo = typeof(NetworkRunner).GetMethod(nameof(SetPlayerId));

        private bool _isSettedPlayerId;
        
        public Dictionary<int, Socket> ConnectedClients { get; } = new();

        public List<Socket> TcpClientSockets { get; } = new();
        public List<Socket> UdpClientSockets { get; } = new();

        public Socket TcpServerSocket { get; private set; }
        public Socket UdpServerSocket { get; private set; }

        public int TcpPort { get; private set; }
        public int UdpPort { get; private set; }
        public int MaxClients { get; private set; }

        public bool IsServer { get; private set; }
        
        public IPAddress ServerIp { get; private set; }

        public int PlayerId { get; private set; }

        public event Action<int> OnPlayerConnected;
        public event Action OnServerStarted;
        public event Action OnClientStarted;

        public NetworkRunner(IRpcListener rpcListener,
            INetworkInitializer networkInitializer)
        {
            _rpcListener = rpcListener;
            networkInitializer.Initialize(this);
            RPCInvoker.RegisterRPCInstance<NetworkRunner>(this);
        }
        
        public async UniTask StartServer(ConnectServerData connectServerData)
        {
            SetServerParameters(connectServerData);
            
            ServerIp = IPAddress.Parse("127.0.0.1");
            
            TcpServerSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            TcpServerSocket.Bind(new IPEndPoint(IPAddress.Any, TcpPort));
            TcpServerSocket.Listen(MaxClients);

            UdpServerSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            UdpServerSocket.Bind(new IPEndPoint(IPAddress.Any, 5057));

            IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, connectServerData.UdpPort);

            IsServer = true;

            Console.WriteLine("Сервер ожидает подключения...");

            OnServerStarted?.Invoke();
            
            WaitConnectClients(remoteEndPoint).Forget();
        }

        public async UniTask StartClient(ConnectClientData connectClientData)
        {
            TcpServerSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            await TcpServerSocket.ConnectAsync(connectClientData.Ip.ToString(), connectClientData.TcpPort);

            UdpServerSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            UdpServerSocket.Bind(new IPEndPoint(IPAddress.Any, UdpPort));

            IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, connectClientData.UdpPort);

            Debug.Log($"Клиент подключен к серверу: {TcpServerSocket.RemoteEndPoint}");

            UniTask.RunOnThreadPool(() => _rpcListener.ListenForTcpRpcCalls(TcpServerSocket, _cts.Token))
                .AttachExternalCancellation(_cts.Token);
            
            UniTask.RunOnThreadPool(() => _rpcListener.ListenForUdpRpcCalls(UdpServerSocket, remoteEndPoint, _cts.Token))
                .AttachExternalCancellation(_cts.Token);
            
            OnClientStarted?.Invoke();
        }

        private async UniTaskVoid WaitConnectClients(IPEndPoint remoteEndPoint)
        {
            while (TcpClientSockets.Count < MaxClients
                   && UdpClientSockets.Count < MaxClients)
            {
                var clientSocket = await TcpServerSocket.AcceptAsync();

                int playerIndex = TcpClientSockets.Count + 1;

                TcpClientSockets.Add(clientSocket);
                UdpClientSockets.Add(clientSocket);

                ConnectedClients.Add(playerIndex, clientSocket);

                UniTask.RunOnThreadPool(() => _rpcListener.ListenForTcpRpcCalls(clientSocket, _cts.Token))
                    .AttachExternalCancellation(_cts.Token);
                
                UniTask.RunOnThreadPool(() => _rpcListener.ListenForUdpRpcCalls(UdpServerSocket, remoteEndPoint, _cts.Token))
                    .AttachExternalCancellation(_cts.Token);

                OnPlayerConnected?.Invoke(playerIndex);
                await UniTask.Delay(1000);
                RPCInvoker.InvokeServiceRPC<NetworkRunner>(this, _methodInfo, NetProtocolType.Tcp, playerIndex);
                Debug.Log($"Клиент подключен: {clientSocket.RemoteEndPoint}");
            }
        }

        [ClientRPC]
        public void SetPlayerId(int id)
        {
            if (_isSettedPlayerId)
                return;
            
            _isSettedPlayerId = true;
            PlayerId = id;
        }

        private void SetServerParameters(ConnectServerData data)
        {
            TcpPort = data.TcpPort;
            UdpPort = data.UdpPort;
            MaxClients = data.MaxClients;
        }

        public void Dispose()
        {
            _cts.Cancel();
            _cts.Dispose();
        }
    }
}