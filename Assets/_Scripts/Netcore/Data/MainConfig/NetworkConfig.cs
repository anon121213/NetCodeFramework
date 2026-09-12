using System.Net;
using Skynet.Data.ConnectionData;
using UnityEngine;

namespace Skynet.Data.MainConfig
{
    [CreateAssetMenu(fileName = "NetworkConfig", menuName = "Skynet/NetworkConfig")]
    public class NetworkConfig : ScriptableObject
    {
        [SerializeField] private string _serverIp = "127.0.0.1";
        [SerializeField] private int _tcpPort = 5056;
        [SerializeField] private int _udpPort = 5057;
        [SerializeField] private int _maxClients = 10;

        public ConnectServerData ServerData => new()
        {
            TcpPort = _tcpPort,
            UdpPort = _udpPort,
            MaxClients = _maxClients
        };

        public ConnectClientData ClientData => new()
        {
            Ip = IPAddress.Parse(_serverIp),
            TcpPort = _tcpPort,
            UdpPort = _udpPort
        };
    }
}
