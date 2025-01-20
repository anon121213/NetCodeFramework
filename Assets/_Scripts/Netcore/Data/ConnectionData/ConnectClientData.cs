using System.Net;

namespace _Scripts.Netcore.Data.ConnectionData
{
    public struct ConnectClientData
    {
        public IPAddress Ip;
        public int TcpPort;
        public int UdpPort;
    }
}