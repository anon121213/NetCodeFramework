using System.Net;
using System.Net.Sockets;
using System.Reflection;
using _Scripts.Netcore.Data.Attributes;
using _Scripts.Netcore.Data.ConnectionData;
using _Scripts.Netcore.NetworkComponents.NetworkVariableComponent;
using _Scripts.Netcore.NetworkComponents.RPCComponents;
using _Scripts.Netcore.RPCSystem;
using _Scripts.Netcore.RPCSystem.DynamicProcessor;
using _Scripts.Netcore.RPCSystem.ProcessorsData;
using _Scripts.Netcore.Runner;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer.Unity;

namespace _Scripts.Infrastructure
{
    public class Bootstrapper : NetworkService, IInitializable 
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
            RPCInvoker.RegisterRPCInstance<Bootstrapper>(this);
            
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
            RPCInvoker.InvokeServiceRPC<Bootstrapper>(this, methodInfo, NetProtocolType.Tcp, "HelloFromClient");
            RPCInvoker.InvokeServiceRPC<Bootstrapper>(this, methodInfo, NetProtocolType.Udp, "HelloFromClient");
        }

        private async void SendServerEvents(int playerId)
        {
            await UniTask.Delay(2000);
            
            MethodInfo methodInfo = typeof(Bootstrapper).GetMethod(nameof(SendToClient));
            RPCInvoker.InvokeServiceRPC<Bootstrapper>(this, methodInfo, NetProtocolType.Tcp, "HelloFromServer");
            RPCInvoker.InvokeServiceRPC<Bootstrapper>(this, methodInfo, NetProtocolType.Udp, "HelloFromServer");
            
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