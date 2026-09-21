using Skynet.RpcSystem;

namespace Skynet.Data.NetworkVariables
{
    internal static class NetworkVariableRpcIds
    {
        public static readonly int CallerTypeId = RpcId.Fnv1a("Skynet.NetworkVariables");
        public static readonly int MethodId = RpcId.Fnv1a("ApplyVariables");
    }
}