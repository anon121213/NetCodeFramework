using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Skynet.NetworkComponents.RPCComponents;

namespace Skynet.RPCSystem.Callers
{
    public class CallerService : ICallerService
    {
        private ConcurrentDictionary<CallerKey, IRPCCaller> _callerServices { get; } = new();
        private ConcurrentDictionary<CallerKey, IRPCCaller> _callerBehaviours { get; } = new();

        public IReadOnlyDictionary<CallerKey, IRPCCaller> CallerServices => _callerServices;
        public IReadOnlyDictionary<CallerKey, IRPCCaller> CallerBehaviours => _callerBehaviours;
        
        public void AddCaller(Type type, NetworkService service) => 
            _callerServices[new CallerKey(type, service.InstanceId)] = service;

        public void AddCaller(Type type, NetworkBehaviour service) => 
            _callerBehaviours[new CallerKey(type, service.InstanceId)] = service;
    }

    public readonly struct CallerKey : IEquatable<CallerKey>
    {
        public Type Type { get; }
        public int InstanceId { get; }

        public CallerKey(Type type, int service)
        {
            Type = type;
            InstanceId = service;
        }

        public bool Equals(CallerKey other) => 
            Type == other.Type && InstanceId == other.InstanceId;

        public override bool Equals(object obj) => 
            obj is CallerKey other && Equals(other);

        public override int GetHashCode() => 
            HashCode.Combine(Type, InstanceId);
    }
    
    public interface ICallerService
    {
        IReadOnlyDictionary<CallerKey, IRPCCaller> CallerServices { get; }
        IReadOnlyDictionary<CallerKey, IRPCCaller> CallerBehaviours { get; }
        
        void AddCaller(Type type, NetworkService service);
        void AddCaller(Type type, NetworkBehaviour service);
    }
}