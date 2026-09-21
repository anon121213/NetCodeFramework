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
using Skynet.Tick;
using Skynet.Transport;

namespace Skynet.Runner
{
    /// <summary>
    /// Framework coordinator — owns transport + RPC + tick subsystems, exposes connection lifecycle
    /// to user code. Concrete class (no interface indirection) so framework can share more internally
    /// via <c>internal</c> members while keeping a small, semantic public API for user code.
    /// </summary>
    public sealed class NetworkRunner : IDisposable
    {
        private readonly IMainThreadDispatcher _mainThread;
        private readonly ISkynetLogger _logger;

        // ---------- Framework-internal subsystems (visible to Skynet.Unity via InternalsVisibleTo) ----------
        internal ITransport Transport { get; }
        internal IRpcDispatcher Dispatcher { get; }
        internal IRpcHandlerRegistry Registry { get; }
        internal IRpcSender Sender { get; }
        internal INetworkTickScheduler TickScheduler { get; }
        internal IServerClockSync ClockSync { get; }

        // ---------- Public passthrough — semantic API for user code ----------
        public bool IsServer => Transport.IsServer;
        public bool IsRunning => Transport.IsRunning;
        public int LocalPlayerId => Transport.LocalClientId;
        public uint CurrentTick => TickScheduler.CurrentTick;
        public float CurrentServerTick => ClockSync.CurrentServerTick;
        public float TickInterval => TickScheduler.TickInterval;

        // ---------- Public events ----------
        public event Action OnServerStarted;
        public event Action<int> OnPlayerConnected;      // server-side: new client joined
        public event Action<int> OnPlayerDisconnected;   // server-side: client left
        public event Action OnConnectedToServer;         // client-side
        public event Action OnDisconnectedFromServer;    // client-side

        public NetworkRunner(
            ITransport transport,
            IRpcDispatcher dispatcher,
            IRpcHandlerRegistry registry,
            IRpcSender sender,
            INetworkTickScheduler tickScheduler,
            IMainThreadDispatcher mainThread,
            ISkynetLogger logger)
        {
            Transport = transport;
            Dispatcher = dispatcher;
            Registry = registry;
            Sender = sender;
            TickScheduler = tickScheduler;
            _mainThread = mainThread;
            _logger = logger;

            Transport.OnDataReceived += HandleDataReceived;
            Transport.OnClientConnected += HandleClientConnected;
            Transport.OnClientDisconnected += HandleClientDisconnected;
            Transport.OnConnectedToServer += HandleConnectedToServer;
            Transport.OnDisconnectedFromServer += HandleDisconnectedFromServer;

            // Framework-internal clock sync service — created here to avoid a DI cycle
            // (ClockSyncService is a NetworkService that needs a NetworkRunner reference).
            ClockSync = new Tick.ClockSyncService(this);
        }

        public async Task StartServerAsync(ConnectServerData config, CancellationToken cancellationToken = default)
        {
            await Transport.StartAsServerAsync(config, cancellationToken);
            OnServerStarted?.Invoke();
        }

        public async Task StartClientAsync(ConnectClientData config, CancellationToken cancellationToken = default)
            => await Transport.StartAsClientAsync(config, cancellationToken);

        public Task StopAsync() => Transport.StopAsync();

        public void Dispose()
        {
            Transport.OnDataReceived -= HandleDataReceived;
            Transport.OnClientConnected -= HandleClientConnected;
            Transport.OnClientDisconnected -= HandleClientDisconnected;
            Transport.OnConnectedToServer -= HandleConnectedToServer;
            Transport.OnDisconnectedFromServer -= HandleDisconnectedFromServer;

            // Runner owns ClockSync (created it in ctor) — must dispose it too.
            (ClockSync as IDisposable)?.Dispose();
        }

        // ---------- Transport → Dispatcher bridge ----------

        private void HandleDataReceived(int fromClientId, ReadOnlySpan<byte> payload, NetProtocolType protocol)
        {
            RpcMessage message;
            try
            {
                message = MessagePackSerializer.Deserialize<RpcMessage>(payload.ToArray());
            }
            catch (Exception ex)
            {
                _logger.Exception($"Failed to deserialize RpcMessage from client {fromClientId} ({protocol})", ex);
                return;
            }

            // Server always overrides SenderId with the real transport-level clientId — anti-spoof.
            if (IsServer)
                message.SenderId = fromClientId;

            Dispatcher.Dispatch(message);
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
