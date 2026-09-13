using System;
using Skynet.Data.Message;
using Skynet.Diagnostics;
using Skynet.Threading;

namespace Skynet.RpcSystem
{
    public sealed class RpcDispatcher : IRpcDispatcher
    {
        private readonly IRpcHandlerRegistry _registry;
        private readonly IMainThreadDispatcher _mainThread;
        private readonly ISkynetLogger _logger;

        public RpcDispatcher(IRpcHandlerRegistry registry, IMainThreadDispatcher mainThread, ISkynetLogger logger)
        {
            _registry = registry;
            _mainThread = mainThread;
            _logger = logger;
        }
        
        public void Dispatch(RpcMessage message)
        {
            if (!_registry.TryGet(message.CallerTypeId, message.InstanceId, message.MethodId, out var handler, out var execution))
            {
                _logger.Warn($"Can't dispatch message of type {message.CallerTypeId}:{message.InstanceId}:{message.MethodId}");
                return;
            }

            try
            {
                switch (execution)
                {
                    case HandlerExecution.Immediate:
                        handler.Invoke(message.Payload, message.SenderId);
                        break;

                    case HandlerExecution.MainThread when _mainThread.IsOnMainThread:
                        handler.Invoke(message.Payload, message.SenderId);
                        break;

                    case HandlerExecution.MainThread:
                        _mainThread.Post(() => handler.Invoke(message.Payload, message.SenderId));
                        break;
                }
            }
            catch (Exception e)
            {
                _logger.Error($"Handler {message.CallerTypeId}:{message.InstanceId}:{message.MethodId} threw", e);
            }
        }
    }
}