using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using _Scripts.Netcore.Data.ConnectionData;
using _Scripts.Netcore.NetworkComponents.NetworkVariableComponent.Processor;
using _Scripts.Netcore.Proxy;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace _Scripts.Netcore.Runner
{
    public class NetworkRunner : INetworkRunner
    {
        public event Action<int> OnPlayerConnected;

        public Dictionary<int, Socket> ConnectedClients { get; } = new();

        public List<Socket> TcpClientSockets { get; } = new();
        public List<Socket> UdpClientSockets { get; } = new();

        public Socket TcpServerSocket { get; private set; }
        public Socket UdpServerSocket { get; private set; }

        public int TcpPort { get; private set; }
        public int UdpPort { get; private set; }
        public int MaxClients { get; private set; }

        public bool IsServer { get; private set; }

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
            RpcProxy.Initialize(this);
            NetworkVariableProcessor.Instance.Initialize(this);

            Console.WriteLine("Сервер ожидает подключения...");

            WaitConnectClients(remoteEndPoint);
        }

        public async UniTask StartClient(ConnectClientData connectClientData)
        {
            TcpServerSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            await TcpServerSocket.ConnectAsync(connectClientData.Ip.ToString(), connectClientData.TcpPort);

            UdpServerSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            UdpServerSocket.Bind(new IPEndPoint(IPAddress.Any, UdpPort));

            IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, connectClientData.UdpPort);

            Debug.Log($"Клиент подключен к серверу: {TcpServerSocket.RemoteEndPoint}");

            RpcProxy.Initialize(this);
            NetworkVariableProcessor.Instance.Initialize(this);

            UniTask.RunOnThreadPool(() => RpcProxy.ListenForTcpRpcCalls(TcpServerSocket)).Forget();
            UniTask.RunOnThreadPool(() => RpcProxy.ListenForUdpRpcCalls(UdpServerSocket, remoteEndPoint)).Forget();
        }

        private async void WaitConnectClients(IPEndPoint remoteEndPoint)
        {
            while (TcpClientSockets.Count < MaxClients
                   && UdpClientSockets.Count < MaxClients)
            {
                var clientSocketTCP = await TcpServerSocket.AcceptAsync();

                int playerIndex = TcpClientSockets.IndexOf(clientSocketTCP);

                TcpClientSockets.Add(clientSocketTCP);
                UdpClientSockets.Add(clientSocketTCP);

                ConnectedClients.Add(playerIndex, clientSocketTCP);

                UniTask.RunOnThreadPool(() => RpcProxy.ListenForTcpRpcCalls(clientSocketTCP)).Forget();
                UniTask.RunOnThreadPool(() => RpcProxy.ListenForUdpRpcCalls(UdpServerSocket, remoteEndPoint)).Forget();

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
    }
}