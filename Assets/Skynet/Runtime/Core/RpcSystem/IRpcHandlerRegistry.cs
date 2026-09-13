namespace Skynet.RpcSystem
{
    public delegate void RpcHandler(byte[] payload, int senderId);

    public enum HandlerExecution
    {
        Immediate = 0,
        MainThread = 1,
    }
    
    public interface IRpcHandlerRegistry
    {
        public void Register(int callerTypeId, int instanceId, int methodId, RpcHandler handler, HandlerExecution execution);
        public void Unregister(int callerTypeId, int instanceId);
        public bool TryGet(int callerTypeId, int instanceId, int methodId, out RpcHandler handler, out HandlerExecution execution);
    }
}