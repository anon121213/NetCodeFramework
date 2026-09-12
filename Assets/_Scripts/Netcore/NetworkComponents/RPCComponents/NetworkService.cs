namespace Skynet.NetworkComponents.RPCComponents
{
    public abstract class NetworkService : IRPCCaller
    {
        public int InstanceId { get; private set; }

        protected NetworkService() => 
            InstanceId = GetStableHash(GetType().FullName);

        private static int GetStableHash(string str)
        {
            unchecked
            {
                int hash = (int)2166136261;
                foreach (char c in str) { hash ^= c; hash *= 16777619; }
                return hash;
            }
        }
    }
}