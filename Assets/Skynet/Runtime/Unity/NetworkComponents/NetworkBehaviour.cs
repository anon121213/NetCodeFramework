using System.Collections.Generic;
using MessagePack;
using Skynet.Data.NetworkVariables;
using Skynet.RpcSystem;
using Skynet.RpcSystem.ProcessorsData;
using UnityEngine;

namespace Skynet.NetworkComponents
{
    [RequireComponent(typeof(NetworkObject))]
    public abstract class NetworkBehaviour : MonoBehaviour, IRpcCaller, IRpcHandlerProvider, INetworkComponent
    {
        public int InstanceId { get; private set; } = -1;
        
        [SerializeField] private bool _needToCollectNetworkObject = true;
        [SerializeField] protected NetworkObject _networkObject;

        protected NetworkObject NetworkObject => _networkObject;
        protected IRpcSender RpcSender { get; private set; }
        protected IRpcHandlerRegistry RpcRegistry { get; private set; }
        
        protected bool IsOwner => NetworkObject != null && NetworkObject.IsOwner;
        protected bool IsServer => NetworkObject != null && NetworkObject.NetworkRunner.IsServer;
        
        private readonly List<VariableDelta> _tcpBuffer = new(8);
        private readonly List<VariableDelta> _udpBuffer = new(8);
        
        private void Awake()
        {
            if (_needToCollectNetworkObject)
                _networkObject = GetComponent<NetworkObject>();
        }

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
            RegisterVariableSyncHandler();
        }

        internal void SyncVariables(uint tick)
        {
            _tcpBuffer.Clear();
            _udpBuffer.Clear();
            CollectDirtyVariables(_tcpBuffer, _udpBuffer);

            if (_tcpBuffer.Count > 0)
            {
                byte[] payload = MessagePackSerializer.Serialize(_tcpBuffer);
                RpcSender.BroadcastToClients(NetworkVariableRpcIds.CallerTypeId, InstanceId,
                    NetworkVariableRpcIds.MethodId, payload, NetProtocolType.Tcp);
            }
            if (_udpBuffer.Count > 0)
            {
                byte[] payload = MessagePackSerializer.Serialize(_udpBuffer);
                RpcSender.BroadcastToClients(NetworkVariableRpcIds.CallerTypeId, InstanceId,
                    NetworkVariableRpcIds.MethodId, payload, NetProtocolType.Udp);
            }
        }

        /// <summary>
        /// Overridden by the RPC source generator's partial class. Default is a no-op.
        /// </summary>
        protected virtual void RegisterHandlers() { }
        protected virtual void CollectDirtyVariables(List<VariableDelta> tcpOutput, List<VariableDelta> udpOutput) { }
        protected virtual void ApplyVariableDelta(int index, byte[] payload) { }

        public virtual void OnNetworkReady() { }

        public virtual void OnNetworkUpdate(uint tick) { }

        public virtual void OnNetworkShutdown() { }
        public virtual void OnOwnerChanged(int oldOwner, int newOwner) { }

        private void RegisterVariableSyncHandler()
        {
            RpcRegistry.Register(
                NetworkVariableRpcIds.CallerTypeId,
                InstanceId,
                NetworkVariableRpcIds.MethodId,
                (payload, senderId) =>
                {
                    if (_networkObject.IsOwner) return;
                    var deltas = MessagePackSerializer.Deserialize<List<VariableDelta>>(payload);
                    for (int i = 0; i < deltas.Count; i++)
                        ApplyVariableDelta(deltas[i].Index, deltas[i].Payload);
                },
                HandlerExecution.MainThread);
        }
    }
}
