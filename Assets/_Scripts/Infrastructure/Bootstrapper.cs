using System.Net;
using System.Threading;
using Skynet.Data.Attributes;
using Skynet.Data.ConnectionData;
using Skynet.NetworkComponents.RpcComponents;
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
        private readonly INetworkRunner _networkRunner;
        private readonly INetworkSpawner _networkSpawner;
        private readonly NetworkObject _gameObject;

        public Bootstrapper(INetworkRunner networkRunner,
            INetworkSpawner networkSpawner,
            NetworkObject gameObject,
            IRpcHandlerRegistry registry,
            IRpcSender sender)
        {
            _networkRunner = networkRunner;
            _networkSpawner = networkSpawner;
            _gameObject = gameObject;

            InitializeRpc(registry, sender);
        }

        public async Awaitable StartAsync(CancellationToken cancellation = default)
        {
            
#if SERVER
            await StartServer();
#else
            await StartClient();
#endif
        }

        private async UniTask StartServer()
        {
            var serverData = new ConnectServerData
            {
                MaxClients = 2,
                TcpPort = 5055,
                UdpPort = 5057,
            };

            _networkRunner.OnServerStarted += () => Debug.Log("Server started");
            _networkRunner.OnPlayerConnected += id => SendToClient(id, $"Hello from server (for client {id})");
            _networkRunner.OnPlayerDisconnected += id => Debug.Log($"Player disconnected: {id}");

            await _networkRunner.StartServerAsync(serverData);
            
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

            _networkRunner.OnConnectedToServer += () => Debug.Log($"Connected to server, my id = {_networkRunner.LocalPlayerId}");
            _networkRunner.OnDisconnectedFromServer += () => Debug.Log("Disconnected from server");

            await _networkRunner.StartClientAsync(clientData);
            
            SendToServer("Hello from client");
        }

        [ClientRpc(NetProtocolType.Tcp, RpcTarget.Client)]
        private void HandleSendToClient(string text) => Debug.Log(text);

        [ServerRpc]
        private void HandleSendToServer(string text) => Debug.Log(text);
    }
}
