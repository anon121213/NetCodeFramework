using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using CodeBase.Network.Data.ConnectionData;
using CodeBase.Network.NetworkComponents.NetworkVariableComponent.Processor;
using CodeBase.Network.Proxy;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CodeBase.Network.Runner
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

            UniTask.Run(() => RpcProxy.ListenForTcpRpcCalls(TcpServerSocket));
            UniTask.Run(() => RpcProxy.ListenForUdpRpcCalls(UdpServerSocket, remoteEndPoint));
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

                OnPlayerConnected?.Invoke(playerIndex);
                
                UniTask.Run(() => RpcProxy.ListenForTcpRpcCalls(clientSocketTCP));
                UniTask.Run(() => RpcProxy.ListenForUdpRpcCalls(UdpServerSocket, remoteEndPoint));

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