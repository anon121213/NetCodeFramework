namespace Skynet.RpcSystem
{
    /// <summary>
    /// Implemented by every partial class the RPC source generator touches. Consumers call
    /// <see cref="InitializeRpc"/> exactly once per instance to register its <c>[ServerRpc]</c> /
    /// <c>[ClientRpc]</c> handlers with the registry and to hand the instance its send-side
    /// <see cref="IRpcSender"/> reference used by generated wrapper methods.
    /// </summary>
    public interface IRpcHandlerProvider
    {
        void InitializeRpc(IRpcHandlerRegistry registry, IRpcSender sender);
    }
}
