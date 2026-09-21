using System.Net;
using System.Threading;
using Skynet.Data.Attributes;
using Skynet.Data.ConnectionData;
using Skynet.NetworkComponents;
using Skynet.Runner;
using Skynet.Spawner;
using Cysharp.Threading.Tasks;
using Skynet.RpcSystem;
using Skynet.RpcSystem.ProcessorsData;
using UnityEngine;
using VContainer.Unity;

namespace _Scripts.Infrastructure
{
    public partial class Bootstrapper : NetworkService, IAsyncStartable
    {
        private readonly INetworkSpawner _networkSpawner;
        private readonly NetworkObject _gameObject;

        [SyncVar(Authority.Server, NetProtocolType.Udp)] private int Health = 100;

        public Bootstrapper(NetworkRunner runner,
            INetworkSpawner networkSpawner,
            NetworkObject gameObject) : base(runner)
        {
            _networkSpawner = networkSpawner;
            _gameObject = gameObject;
        }

        public async Awaitable StartAsync(CancellationToken cancellation = default)
        {
#if SERVER
            await StartServer();
#else
            await StartClient();
#endif
            OnHealthChanged += OnChanged;
        }

        private async UniTask StartServer()
        {
            var serverData = new ConnectServerData
            {
                MaxClients = 2,
                TcpPort = 5055,
                UdpPort = 5057,
            };

            Runner.OnServerStarted += () => Debug.Log("Server started");
            Runner.OnPlayerConnected += id =>
            {
                SendToClient(id, $"Hello from server (for client {id})");
                _networkSpawner.Spawn(_gameObject, Vector3.zero, Quaternion.identity, Vector3.one, ownerClientId: id);
                Health += 10;
            };
            Runner.OnPlayerDisconnected += id => Debug.Log($"Player disconnected: {id}");

            await Runner.StartServerAsync(serverData);
            
            var go = _networkSpawner.Spawn(_gameObject, Vector3.zero, Quaternion.identity, Vector3.one);
            _networkSpawner.Spawn(_gameObject, Vector3.one * 3, Quaternion.identity, Vector3.one, go.transform);
        }

        private async UniTask StartClient()
        {
            IPAddress.TryParse("127.0.0.1", out var ipAddress);

            var clientData = new ConnectClientData
            {
                Ip = ipAddress,
                TcpPort = 5055,
                UdpPort = 5057,
            };

            Runner.OnConnectedToServer += () => Debug.Log($"Connected to server, my id = {Runner.LocalPlayerId}");
            Runner.OnDisconnectedFromServer += () => Debug.Log("Disconnected from server");

            await Runner.StartClientAsync(clientData);
            
            SendToServer("Hello from client");
        }

        private void OnChanged(int oldValue, int newValue) => 
            Debug.LogError($"oldValue: {oldValue}, newValue: {newValue}");

        [OnChange(nameof(Health))]
        private void OnHealthChange(int oldValue, int newValue) => 
            Debug.LogError($"oldValue: {oldValue}, newValue: {newValue}");

        [ClientRpc(NetProtocolType.Tcp, RpcTarget.Client)]
        private void HandleSendToClient(string text) => Debug.Log(text);

        [ServerRpc]
        private void HandleSendToServer(string text) => Debug.Log(text);

        protected override void OnDispose()
        {
            OnHealthChanged -= OnChanged;
        }
    }
}
