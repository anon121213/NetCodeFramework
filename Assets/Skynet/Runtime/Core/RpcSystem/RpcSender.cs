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
            NetProtocolType protocol) =>
            _transport.Send(targetSenderId, Serialize(callerTypeId, instanceId, methodId, payload), protocol);

        public void BroadcastToClients(int callerTypeId, int instanceId, int methodId, byte[] payload, NetProtocolType protocol) =>
            _transport.Broadcast(Serialize(callerTypeId, instanceId, methodId, payload), protocol);

        /// <summary>
        /// Builds the wire message. SenderId is auto-stamped from the transport:
        /// - On a client, <see cref="ITransport.LocalClientId"/> is the client's own id, so the handler on
        ///   the other side sees "sent by client N". This value is COSMETIC because the server always
        ///   overwrites SenderId with the socket-authoritative id on receive (see NetworkRunner.HandleDataReceived) —
        ///   a malicious client cannot successfully spoof another player's id here.
        /// - On the server, LocalClientId is 0, so client-side handlers see SenderId=0 meaning "from server".
        /// </summary>
        private byte[] Serialize(int callerTypeId, int instanceId, int methodId, byte[] payload)
        {
            return MessagePackSerializer.Serialize(new RpcMessage
            {
                CallerTypeId = callerTypeId,
                InstanceId = instanceId,
                MethodId = methodId,
                Payload = payload,
                SenderId = _transport.LocalClientId,
            });
        }
    }
}
