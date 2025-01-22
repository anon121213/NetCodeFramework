using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using _Scripts.Netcore.Data.ConnectionData;
using _Scripts.Netcore.Initializer;
using _Scripts.Netcore.RPCSystem;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace _Scripts.Netcore.Runner
{
    public class NetworkRunner : INetworkRunner, IDisposable
    {
        private readonly CancellationTokenSource _cts = new();
        private readonly IRpcListener _rpcListener;

        public Dictionary<int, Socket> ConnectedClients { get; } = new();

        public List<Socket> TcpClientSockets { get; } = new();
        public List<Socket> UdpClientSockets { get; } = new();

        public Socket TcpServerSocket { get; private set; }
        public Socket UdpServerSocket { get; private set; }

        public int TcpPort { get; private set; }
        public int UdpPort { get; private set; }
        public int MaxClients { get; private set; }

        public bool IsServer { get; private set; }

        public event Action<int> OnPlayerConnected;

        public NetworkRunner(IRpcListener rpcListener,
            INetworkInitializer networkInitializer)
        {
            _rpcListener = rpcListener;
            networkInitializer.Initialize(this);
        }
        
        public async UniTask StartServer(ConnectServerData connectServerData)
        {
            SetServerParameters(connectServerData);

            TcpServerSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            TcpServerSocket.Bind(new IPEndPoint(IPAddress.Any, TcpPort));
            TcpServerSocket.Listen(MaxClients);

            UdpServerSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            UdpServerSocket.Bind(new IPEndPoint(IPAddress.Any, 5057));

            IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, connectServerData.UdpPort);

            IsServer = true;

            Console.WriteLine("Сервер ожидает подключения...");

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
        }

        private async UniTaskVoid WaitConnectClients(IPEndPoint remoteEndPoint)
        {
            while (TcpClientSockets.Count < MaxClients
                   && UdpClientSockets.Count < MaxClients)
            {
                var clientSocketTCP = await TcpServerSocket.AcceptAsync();

                int playerIndex = TcpClientSockets.IndexOf(clientSocketTCP);

                TcpClientSockets.Add(clientSocketTCP);
                UdpClientSockets.Add(clientSocketTCP);

                ConnectedClients.Add(playerIndex, clientSocketTCP);

                UniTask.RunOnThreadPool(() => _rpcListener.ListenForTcpRpcCalls(clientSocketTCP, _cts.Token))
                    .AttachExternalCancellation(_cts.Token);
                
                UniTask.RunOnThreadPool(() => _rpcListener.ListenForUdpRpcCalls(UdpServerSocket, remoteEndPoint, _cts.Token))
                    .AttachExternalCancellation(_cts.Token);

                OnPlayerConnected?.Invoke(playerIndex);
                Debug.Log($"Клиент подключен: {clientSocketTCP.RemoteEndPoint}");
            }
        }

        private void SetServerParameters(ConnectServerData data)
        {
            TcpPort = data.TcpPort;
            UdpPort = data.UdpPort;
            MaxClients = data.MaxClients;
        }

        public void Dispose()
        {
            _cts.Dispose();
        }
    }
}