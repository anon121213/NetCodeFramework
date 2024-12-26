using System.Net;

namespace CodeBase.Network.Data.ConnectionData
{
    public struct ConnectClientData
    {
        public IPAddress Ip;
        public int TcpPort;
        public int UdpPort;
    }
}