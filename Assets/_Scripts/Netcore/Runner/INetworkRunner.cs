using System;
using System.Collections.Generic;
using System.Net.Sockets;
using _Scripts.Netcore.Data.ConnectionData;
using Cysharp.Threading.Tasks;

namespace _Scripts.Netcore.Runner
{
    public interface INetworkRunner
    {
        UniTask StartServer(ConnectServerData connectServerData);
        UniTask StartClient(ConnectClientData connectClientData);

        event Action<int> OnPlayerConnected;

        Dictionary<int, Socket> ConnectedClients { get; }

        List<Socket> TcpClientSockets { get; }
        List<Socket> UdpClientSockets { get; }

        Socket TcpServerSocket { get; }
        Socket UdpServerSocket { get; }

        int TcpPort { get; }
        int UdpPort { get; }
        int MaxClients { get; }

        bool IsServer { get; }
    }
}