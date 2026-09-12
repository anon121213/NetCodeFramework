using System.Net;

namespace Skynet.Data.ConnectionData
{
    public struct ConnectClientData
    {
        public IPAddress Ip;
        public int TcpPort;
        public int UdpPort;
    }
}