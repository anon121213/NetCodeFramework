using System.Threading;

namespace _Scripts.Netcore.NetworkComponents.RPCComponents
{
    public abstract class NetworkService : IRPCCaller
    {
        private static int _instanceCounter;

        public int InstanceId { get; private set; }

        public void InitializeNetworkService() => 
            InstanceId = Interlocked.Increment(ref _instanceCounter);
    }
}