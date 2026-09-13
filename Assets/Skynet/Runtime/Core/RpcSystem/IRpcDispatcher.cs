using Skynet.Data.Message;

namespace Skynet.RpcSystem
{
    public interface IRpcDispatcher
    {
        void Dispatch(RpcMessage message);
    }
}