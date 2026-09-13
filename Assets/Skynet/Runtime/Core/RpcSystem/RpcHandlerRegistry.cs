using System;
using System.Collections.Concurrent;
using Skynet.Diagnostics;

namespace Skynet.RpcSystem
{
    public sealed class RpcHandlerRegistry : IRpcHandlerRegistry
    {
        private readonly ISkynetLogger _logger;
        private readonly ConcurrentDictionary<HandlerKey, HandlerEntry> _handlers = new();

        public RpcHandlerRegistry(ISkynetLogger logger)
        {
            _logger = logger;
        }
        
        public void Register(int callerTypeId, int instanceId, int methodId, RpcHandler handler, HandlerExecution execution)
        {
            if (!_handlers.TryAdd(new HandlerKey(callerTypeId, instanceId, methodId), new HandlerEntry(handler, execution)))
                _logger.Warn($"Handler: {{callerTypeId: {callerTypeId}, instanceId: {instanceId}, methodId: {methodId}}} already exists");
        }

        public void Unregister(int callerTypeId, int instanceId)
        {
            foreach (var key in _handlers.Keys)
                if (key.CallerTypeId == callerTypeId && key.InstanceId == instanceId)
                    _handlers.TryRemove(key, out _);
        }

        public bool TryGet(int callerTypeId, int instanceId, int methodId, out RpcHandler handler, out HandlerExecution execution)
        {
            if (!_handlers.TryGetValue(new HandlerKey(callerTypeId, instanceId, methodId), out HandlerEntry entry))
            {
                handler = null;
                execution = default;
                return false;
            }
            
            handler = entry.Handler;
            execution = entry.Execution;
            return true;
        }
        
        private readonly struct HandlerKey : IEquatable<HandlerKey>
        {
            public int CallerTypeId { get; }
            public int InstanceId { get; }
            public int MethodId { get; }

            public HandlerKey(int callerTypeId, int instanceId, int methodId)
            {
                CallerTypeId = callerTypeId;
                InstanceId = instanceId;
                MethodId = methodId;
            }

            public bool Equals(HandlerKey other) => 
                CallerTypeId == other.CallerTypeId && InstanceId == other.InstanceId && MethodId == other.MethodId;

            public override bool Equals(object obj)
            {
                return obj is HandlerKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(CallerTypeId, InstanceId, MethodId);
            }
        }

        private struct HandlerEntry
        {
            public RpcHandler Handler { get; }
            public HandlerExecution Execution { get; }

            public HandlerEntry(RpcHandler handler, HandlerExecution execution)
            {
                Handler = handler;
                Execution = execution;
            }
        }
    }
}