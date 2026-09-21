using System;
using Skynet.RpcSystem;

namespace Skynet.NetworkComponents.RpcComponents
{
    public abstract class NetworkService : IRpcCaller, IRpcHandlerProvider, IDisposable
    {
        public int InstanceId { get; }

        protected IRpcSender RpcSender { get; private set; }
        protected IRpcHandlerRegistry RpcRegistry { get; private set; }

        protected NetworkService()
        {
            InstanceId = RpcId.Fnv1a(GetType().FullName);
        }

        /// <summary>
        /// Wires the service into the RPC pipeline: stores <paramref name="registry"/>+<paramref name="sender"/>
        /// and asks the source-generated <see cref="RegisterHandlers"/> override to install every handler.
        /// Call this exactly once per instance — the framework arranges this for services created via DI.
        /// </summary>
        public void InitializeRpc(IRpcHandlerRegistry registry, IRpcSender sender)
        {
            RpcRegistry = registry;
            RpcSender = sender;
            RegisterHandlers();
            OnNetworkReady();
        }

        /// <summary>
        /// Overridden by the RPC source generator's partial class to register every [ServerRpc]/[ClientRpc]
        /// handler with <see cref="RpcRegistry"/>. Default is a no-op (for services without any RPCs).
        /// </summary>
        protected virtual void RegisterHandlers() { }

        protected virtual void OnDispose() { }

        protected virtual void OnNetworkReady() { }
        
        public void Dispose() => OnDispose();
    }
}
