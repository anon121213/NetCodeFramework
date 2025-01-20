using System.Net;
using System.Net.Sockets;
using System.Reflection;
using _Scripts.Netcore.Data.Attributes;
using _Scripts.Netcore.Data.ConnectionData;
using _Scripts.Netcore.NetworkComponents.NetworkVariableComponent;
using _Scripts.Netcore.Proxy;
using _Scripts.Netcore.Runner;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer.Unity;

namespace _Scripts.Infrastructure
{
    public class Bootstrapper : IInitializable, IRPCCaller
    {
        private readonly INetworkRunner _networkRunner;

        private readonly INetworkVariable<int> _networkStringVariable = 
            new NetworkVariable<int>("TestVar", 0);

        public Bootstrapper(INetworkRunner networkRunner)
        {
            _networkRunner = networkRunner;
        }
        
        public async void Initialize()
        {
            RpcProxy.RegisterRPCInstance<Bootstrapper>(this);
            
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
            _networkRunner.OnPlayerConnected += SendServerEvents;
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
            RpcProxy.TryInvokeRPC<Bootstrapper>(methodInfo, ProtocolType.Tcp, "HelloFromClient");
            RpcProxy.TryInvokeRPC<Bootstrapper>(methodInfo, ProtocolType.Udp, "HelloFromClient");
            
           
        }

        private async void SendServerEvents(int playerId)
        {
            await UniTask.Delay(1000);
            
            MethodInfo methodInfo = typeof(Bootstrapper).GetMethod(nameof(SendToClient));
            RpcProxy.TryInvokeRPC<Bootstrapper>(methodInfo, ProtocolType.Tcp, "HelloFromServer");
            RpcProxy.TryInvokeRPC<Bootstrapper>(methodInfo, ProtocolType.Udp, "HelloFromServer");
            
            _networkStringVariable.Value = 100;
        }

        [ClientRPC]
        public void SendToClient(string text)
        {
            Debug.Log(text);
        }

        [ServerRPC]
        public void SendToServer(string text)
        {
            Debug.Log(text);
        }
    }
}