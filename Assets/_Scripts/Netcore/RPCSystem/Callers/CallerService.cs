using System;
using System.Collections.Generic;
using _Scripts.Netcore.NetworkComponents.RPCComponents;

namespace _Scripts.Netcore.RPCSystem.Callers
{
    public class CallerService : ICallerService
    {
        public Dictionary<(Type, int), IRPCCaller> CallerServices { get; } = new();
        public Dictionary<(Type, int), IRPCCaller> CallerBehaviours { get; } = new();
        
        public void AddCaller(Type type, NetworkService service)
        {
            CallerServices[(type, service.InstanceId)] = service;
        }

        public void AddCaller(Type type, NetworkBehaviour service)
        {
            CallerBehaviours[(type, service.InstanceId)] = service;
        }
    }
    
    public interface ICallerService
    {
        Dictionary<(Type, int), IRPCCaller> CallerServices { get; }
        Dictionary<(Type, int), IRPCCaller> CallerBehaviours { get; }
        
        void AddCaller(Type type, NetworkService service);
        void AddCaller(Type type, NetworkBehaviour service);
    }
}