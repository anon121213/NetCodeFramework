using System.Net;
using System.Reflection;
using System.Threading;
using Skynet.Data.Attributes;
using Skynet.Data.ConnectionData;
using Skynet.NetworkComponents.NetworkVariableComponent;
using Skynet.NetworkComponents.RpcComponents;
using Skynet.RpcSystem;
using Skynet.RpcSystem.ProcessorsData;
using Skynet.Runner;
using Skynet.Spawner;
using Skynet.Spawner.ObjectsSyncer;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer.Unity;

namespace _Scripts.Infrastructure
{
    public class Bootstrapper : NetworkService, IAsyncStartable
    {
        private readonly INetworkRunner _networkRunner;
        private readonly INetworkSpawner _networkSpawner;
        private readonly NetworkObject _gameObject;
        private readonly INetworkObjectSyncer _networkObjectSyncer;

        private readonly INetworkVariable<int> _networkStringVariable = 
            new NetworkVariable<int>("TestVar", 0);

        public Bootstrapper(INetworkRunner networkRunner,
            INetworkSpawner networkSpawner,
            NetworkObject gameObject,
            INetworkObjectSyncer networkObjectSyncer)
        {
            _networkRunner = networkRunner;
            _networkSpawner = networkSpawner;
            _gameObject = gameObject;
            _networkObjectSyncer = networkObjectSyncer;
        }
        
        public async Awaitable StartAsync(CancellationToken cancellation = default)
        {
            RpcInvoker.RegisterRpcInstance<Bootstrapper>(this);
            
            _networkStringVariable.OnValueChanged += i => Debug.Log($"Value has been changed on: {i}");
#if SERVER
            await StartServer();
#else
            await StartClient();
#endif
        }

        private async UniTask StartServer()
        {
            ConnectServerData serverData = new ConnectServerData
            {
                MaxClients = 2,
                TcpPort = 5055,
                UdpPort = 5057
            };

            await _networkRunner.StartServer(serverData);
            
            var go = _networkSpawner.Spawn(_gameObject, Vector3.zero, Quaternion.identity, Vector3.one);
            _networkSpawner.Spawn(_gameObject, Vector3.one * 3, Quaternion.identity, Vector3.one, go.transform);
            
            _networkRunner.OnPlayerConnected += async id => await SendServerEvents(id);
        }

        private async UniTask StartClient()
        {
            IPAddress.TryParse("127.0.0.1", out IPAddress ipAddress);
            
            ConnectClientData clientData = new ConnectClientData
            {
                Ip = ipAddress,
                TcpPort = 5055,
                UdpPort = 5056
            };
            await _networkRunner.StartClient(clientData);

            MethodInfo methodInfo = typeof(Bootstrapper).GetMethod(nameof(SendToServer));
            RpcInvoker.InvokeServiceRpc<Bootstrapper>(this, methodInfo, NetProtocolType.Tcp, "HelloFromClient");
            RpcInvoker.InvokeServiceRpc<Bootstrapper>(this, methodInfo, NetProtocolType.Udp, "HelloFromClient");
        }

        private async UniTask SendServerEvents(int playerId)
        {
            await UniTask.Delay(1000);
            
            MethodInfo methodInfo = typeof(Bootstrapper).GetMethod(nameof(SendToClient));
            RpcInvoker.InvokeServiceRpc<Bootstrapper>(this, methodInfo, NetProtocolType.Tcp, "HelloFromServerTcp");
            RpcInvoker.InvokeServiceRpc<Bootstrapper>(this, methodInfo, NetProtocolType.Udp, "HelloFromServerUdp");
            
            _networkSpawner.Sync();
            
            _networkStringVariable.Value = 100;
        }

        [ClientRpc]
        public void SendToClient(string text)
        {
            Debug.Log(text);
        }

        [ServerRpc]
        public void SendToServer(string text)
        {
            Debug.Log(text);
        }
    }
}