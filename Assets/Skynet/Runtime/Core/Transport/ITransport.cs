using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Skynet.Data.ConnectionData;
using Skynet.RpcSystem.ProcessorsData;

namespace Skynet.Transport
{
    public delegate void DataReceivedHandler(int clientId, ReadOnlySpan<byte> payload, NetProtocolType protocol);
    
    /// <summary>
    /// Abstraction over the wire. Skynet knows nothing about TCP, UDP, WebSockets or RUDP —
    /// it only knows there is an <see cref="ITransport"/> that can ship bytes to peers
    /// and raise events when bytes come back.
    /// <para/>
    /// Events fire on transport-internal threads. Consumers that need Unity main-thread
    /// safety must hop through <see cref="Skynet.Threading.IMainThreadDispatcher"/>.
    /// </summary>
    public interface ITransport : IDisposable
    {
        Task StartAsServerAsync(ConnectServerData config, CancellationToken cancellationToken);
        Task StartAsClientAsync(ConnectClientData config, CancellationToken cancellationToken);
        Task StopAsync();

        // Server-side send API.
        // clientId is assigned by the transport when a client connects and is surfaced through OnClientConnected.
        void Send(int clientId, ReadOnlySpan<byte> payload, NetProtocolType protocol);
        void Broadcast(ReadOnlySpan<byte> payload, NetProtocolType protocol);

        // Client-side send API.
        void SendToServer(ReadOnlySpan<byte> payload, NetProtocolType protocol);

        // (fromClientId, payload, protocol). fromClientId is 0 when the local side is a client (data is always from server).
        event DataReceivedHandler OnDataReceived;

        event Action<int> OnClientConnected;
        event Action<int> OnClientDisconnected;
        event Action OnConnectedToServer;
        event Action OnDisconnectedFromServer;
        
        IReadOnlyCollection<int> ClientIds { get; }

        bool IsServer { get; }
        bool IsRunning { get; }

        /// <summary>
        /// Client-side: own clientId assigned by server during handshake, valid after
        /// <see cref="OnConnectedToServer"/> fires. Server-side: always 0.
        /// </summary>
        int LocalClientId { get; }
    }
}
