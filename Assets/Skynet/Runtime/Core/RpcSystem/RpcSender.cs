using MessagePack;
using Skynet.Data.Message;
using Skynet.Diagnostics;
using Skynet.RpcSystem.ProcessorsData;
using Skynet.Transport;

namespace Skynet.RpcSystem
{
    public sealed class RpcSender : IRpcSender
    {
        private readonly ITransport _transport;
        private readonly ISkynetLogger _logger;

        public RpcSender(ITransport transport, ISkynetLogger logger)
        {
            _transport = transport;
            _logger = logger;
        }
        
        public void SendToServer(int callerTypeId, int instanceId, int methodId, byte[] payload, NetProtocolType protocol) => 
            _transport.SendToServer(Serialize(callerTypeId, instanceId, methodId, payload), protocol);

        public void SendToClient(int targetSenderId, int callerTypeId, int instanceId, int methodId, byte[] payload,
            NetProtocolType protocol)
        {
            var serializedPayload = MessagePackSerializer.Serialize(new RpcMessage
            {
                CallerTypeId = callerTypeId,
                InstanceId = instanceId,
                MethodId = methodId,
                Payload = payload,
                SenderId = 0
            });
            
            _transport.Send(targetSenderId, serializedPayload, protocol);
        }

        public void BroadcastToClients(int callerTypeId, int instanceId, int methodId, byte[] payload, NetProtocolType protocol) => 
            _transport.Broadcast(Serialize(callerTypeId, instanceId, methodId, payload), protocol);

        private byte[] Serialize(int callerTypeId, int instanceId, int methodId, byte[] payload)
        {
            return MessagePackSerializer.Serialize(new RpcMessage
            {
                CallerTypeId = callerTypeId,
                InstanceId = instanceId,
                MethodId = methodId,
                Payload = payload,
                SenderId = 0
            });
        }
    }
}