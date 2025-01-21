namespace _Scripts.Netcore.Data.MainConfig
{
    public class NetworkConfig
    {
        public int TcpPort { get; private set; }
        public int UdpPort { get; private set; }
        public int MaxClients { get; private set; }

        public bool IsServer { get; private set; }
    }
}