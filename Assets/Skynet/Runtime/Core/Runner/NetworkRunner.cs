using System;
using System.Threading;
using System.Threading.Tasks;
using MessagePack;
using Skynet.Data.ConnectionData;
using Skynet.Data.Message;
using Skynet.Diagnostics;
using Skynet.RpcSystem;
using Skynet.RpcSystem.ProcessorsData;
using Skynet.Threading;
using Skynet.Transport;

namespace Skynet.Runner
{
    public sealed class NetworkRunner : INetworkRunner, IDisposable
    {
        private readonly ITransport _transport;
        private readonly IRpcDispatcher _dispatcher;
        private readonly IMainThreadDispatcher _mainThread;
        private readonly ISkynetLogger _logger;

        public NetworkRunner(ITransport transport, IRpcDispatcher dispatcher, IMainThreadDispatcher mainThread, ISkynetLogger logger)
        {
            _transport = transport;
            _dispatcher = dispatcher;
            _mainThread = mainThread;
            _logger = logger;

            _transport.OnDataReceived += HandleDataReceived;
            _transport.OnClientConnected += HandleClientConnected;
            _transport.OnClientDisconnected += HandleClientDisconnected;
            _transport.OnConnectedToServer += HandleConnectedToServer;
            _transport.OnDisconnectedFromServer += HandleDisconnectedFromServer;
        }

        public bool IsServer => _transport.IsServer;
        public bool IsRunning => _transport.IsRunning;
        public int LocalPlayerId => _transport.LocalClientId;

        public event Action OnServerStarted;
        public event Action<int> OnPlayerConnected;
        public event Action<int> OnPlayerDisconnected;
        public event Action OnConnectedToServer;
        public event Action OnDisconnectedFromServer;

        public async Task StartServerAsync(ConnectServerData config, CancellationToken cancellationToken = default)
        {
            await _transport.StartAsServerAsync(config, cancellationToken);
            OnServerStarted?.Invoke();
        }

        public async Task StartClientAsync(ConnectClientData config, CancellationToken cancellationToken = default)
            => await _transport.StartAsClientAsync(config, cancellationToken);

        public Task StopAsync() => _transport.StopAsync();

        public void Dispose()
        {
            _transport.OnDataReceived -= HandleDataReceived;
            _transport.OnClientConnected -= HandleClientConnected;
            _transport.OnClientDisconnected -= HandleClientDisconnected;
            _transport.OnConnectedToServer -= HandleConnectedToServer;
            _transport.OnDisconnectedFromServer -= HandleDisconnectedFromServer;
        }

        // ---------- Transport → Dispatcher bridge ----------

        private void HandleDataReceived(int fromClientId, ReadOnlySpan<byte> payload, NetProtocolType protocol)
        {
            RpcMessage message;
            try
            {
                // Copy Span out to array because MessagePackSerializer.Deserialize<T>(ReadOnlyMemory<byte>) needs
                // ownership and the underlying buffer is pool-owned by the transport receive loop.
                // TODO(perf): teach MessagePack + our wire format to work on Span/ReadOnlyMemory directly.
                message = MessagePackSerializer.Deserialize<RpcMessage>(payload.ToArray());
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to deserialize RpcMessage from client {fromClientId} ({protocol})", ex);
                return;
            }

            // Server always overrides SenderId with the real transport-level clientId — this defeats spoofing
            // (a malicious client can't claim to be someone else, because the server bases sender on the socket
            // the packet arrived on, not on trust of the payload).
            if (IsServer)
                message.SenderId = fromClientId;

            _dispatcher.Dispatch(message);
        }

        private void HandleClientConnected(int clientId) => 
            _mainThread.Post(() => OnPlayerConnected?.Invoke(clientId));

        private void HandleClientDisconnected(int clientId) => 
            _mainThread.Post(() => OnPlayerDisconnected?.Invoke(clientId));

        private void HandleConnectedToServer() => 
            _mainThread.Post(() => OnConnectedToServer?.Invoke());

        private void HandleDisconnectedFromServer() => 
            _mainThread.Post(() => OnDisconnectedFromServer?.Invoke());
    }
}
