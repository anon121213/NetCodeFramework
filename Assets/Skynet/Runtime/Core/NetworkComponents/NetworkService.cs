using System;
using System.Collections.Generic;
using MessagePack;
using Skynet.Data.NetworkVariables;
using Skynet.RpcSystem;
using Skynet.RpcSystem.ProcessorsData;
using Skynet.Runner;

namespace Skynet.NetworkComponents
{
    /// <summary>
    /// Framework base for non-MonoBehaviour networked services. A derived class becomes wire-connected
    /// simply by inheriting: base ctor pulls RPC + tick infrastructure off the shared <see cref="NetworkRunner"/>,
    /// registers source-generated handlers + variable-sync handler, and subscribes to the tick for periodic
    /// variable delta broadcasting. Service authority is server-side (network variables server-writes, clients read).
    /// </summary>
    public abstract class NetworkService : IDisposable
    {
        protected readonly NetworkRunner Runner;

        public int InstanceId { get; }

        protected IRpcSender RpcSender => Runner.Sender;
        protected IRpcHandlerRegistry RpcRegistry => Runner.Registry;
        
        protected bool IsOwner => false;             // singleton service — no per-instance owner TODO Make it per instance
        protected bool IsServer => Runner.IsServer;

        private readonly List<VariableDelta> _tcpBuffer = new(8);
        private readonly List<VariableDelta> _udpBuffer = new(8);

        protected NetworkService(NetworkRunner runner)
        {
            Runner = runner;
            InstanceId = RpcId.Fnv1a(GetType().FullName);

            RegisterHandlers();
            RegisterVariableSyncHandler();
            Runner.TickScheduler.OnTick += SyncVariables;
            OnNetworkReady();
        }

        /// <summary>Source generator overrides this to register every [ServerRpc]/[ClientRpc] handler.</summary>
        protected virtual void RegisterHandlers() { }

        /// <summary>User hook — called once after framework wiring is complete.</summary>
        protected virtual void OnNetworkReady() { }

        /// <summary>User hook — called from <see cref="Dispose"/>.</summary>
        protected virtual void OnDispose() { }

        /// <summary>Source-generated override collects fields marked <c>[SyncVar]</c> whose value changed.</summary>
        protected virtual void CollectDirtyVariables(List<VariableDelta> tcpOutput, List<VariableDelta> udpOutput) { }

        /// <summary>Source-generated override applies a single delta received from the server.</summary>
        protected virtual void ApplyVariableDelta(int index, byte[] payload) { }

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

        private void RegisterVariableSyncHandler()
        {
            Runner.Registry.Register(
                NetworkVariableRpcIds.CallerTypeId,
                InstanceId,
                NetworkVariableRpcIds.MethodId,
                (payload, senderId) =>
                {
                    if (Runner.IsServer) return; // server-authoritative — server is the source of truth
                    var deltas = MessagePackSerializer.Deserialize<List<VariableDelta>>(payload);
                    for (int i = 0; i < deltas.Count; i++)
                        ApplyVariableDelta(deltas[i].Index, deltas[i].Payload);
                },
                HandlerExecution.MainThread);
        }

        public void Dispose()
        {
            Runner.TickScheduler.OnTick -= SyncVariables;
            OnDispose();
        }
    }
}
