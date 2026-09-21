using Skynet.RpcSystem;
using UnityEngine;

namespace Skynet.NetworkComponents.RpcComponents
{
    [RequireComponent(typeof(NetworkObject))]
    public abstract class NetworkBehaviour : MonoBehaviour, IRpcCaller, IRpcHandlerProvider, INetworkComponent
    {
        public int InstanceId { get; private set; } = -1;

        protected IRpcSender RpcSender { get; private set; }
        protected IRpcHandlerRegistry RpcRegistry { get; private set; }

        public bool TryInitNetworkId(int hash)
        {
            if (InstanceId >= 0)
                return false;

            InstanceId = hash;
            return true;
        }

        /// <summary>
        /// Wires this behaviour into the RPC pipeline. Must be called AFTER
        /// <see cref="TryInitNetworkId"/> so <see cref="InstanceId"/> is valid.
        /// The framework arranges this via <c>NetworkObject.InitializeBehaviours</c>.
        /// </summary>
        public void InitializeRpc(IRpcHandlerRegistry registry, IRpcSender sender)
        {
            RpcRegistry = registry;
            RpcSender = sender;
            RegisterHandlers();
        }

        /// <summary>
        /// Overridden by the RPC source generator's partial class. Default is a no-op.
        /// </summary>
        protected virtual void RegisterHandlers() { }

        public virtual void OnNetworkReady() { }

        public virtual void OnNetworkUpdate(uint tick) { }

        public virtual void OnNetworkShutdown() { }
    }

    public interface INetworkComponent
    {
    }
}
