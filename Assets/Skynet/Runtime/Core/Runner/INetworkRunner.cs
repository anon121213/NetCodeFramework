using System;
using System.Threading;
using System.Threading.Tasks;
using Skynet.Data.ConnectionData;

namespace Skynet.Runner
{
    /// <summary>
    /// Application-facing facade over the transport + RPC layer. Handles bridging inbound
    /// transport bytes to the dispatcher, exposes connection lifecycle events in
    /// application-level terms, and holds shared state (own PlayerId, isServer, etc).
    /// </summary>
    public interface INetworkRunner
    {
        Task StartServerAsync(ConnectServerData config, CancellationToken cancellationToken = default);
        Task StartClientAsync(ConnectClientData config, CancellationToken cancellationToken = default);
        Task StopAsync();

        bool IsServer { get; }
        bool IsRunning { get; }

        /// <summary>
        /// Client-side: this client's id assigned by the server during handshake.
        /// Server-side: always 0.
        /// </summary>
        int LocalPlayerId { get; }

        event Action OnServerStarted;
        event Action<int> OnPlayerConnected;      // server-side: new client joined
        event Action<int> OnPlayerDisconnected;   // server-side: client left
        event Action OnConnectedToServer;         // client-side
        event Action OnDisconnectedFromServer;    // client-side
    }
}
