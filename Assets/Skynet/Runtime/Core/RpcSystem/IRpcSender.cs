using Skynet.RpcSystem.ProcessorsData;

namespace Skynet.RpcSystem
{
    public interface IRpcSender
    {
        void SendToServer(int callerTypeId, int instanceId, int methodId, byte[] payload, NetProtocolType protocol);

        void SendToClient(int targetSenderId, int callerTypeId, int instanceId, int methodId, byte[] payload,
            NetProtocolType protocol);

        void BroadcastToClients(int callerTypeId, int instanceId, int methodId, byte[] payload,
            NetProtocolType protocol);
    }
}