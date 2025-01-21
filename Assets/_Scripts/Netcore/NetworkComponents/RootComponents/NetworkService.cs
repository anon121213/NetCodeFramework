using System.Threading;

namespace _Scripts.Netcore.NetworkComponents.RootComponents
{
    public abstract class NetworkService : IRPCCaller
    {
        private int _instanceCounter;

        public int InstanceId { get; private set; }

        public void InitializeNetworkService() => 
            InstanceId = Interlocked.Increment(ref _instanceCounter);
    }
}