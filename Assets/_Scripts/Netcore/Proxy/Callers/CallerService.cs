using System;
using System.Collections.Generic;
using _Scripts.Netcore.NetworkComponents.RootComponents;

namespace _Scripts.Netcore.Proxy.Callers
{
    public class CallerService : ICallerService
    {
        public Dictionary<(Type, int), IRPCCaller> Callers { get; } = new();
        
        public void AddCaller(Type type, NetworkService service)
        {
            Callers[(type, service.InstanceId)] = service;
        }

        public void AddCaller(Type type, NetworkBehaviour service)
        {
            Callers[(type, service.InstanceId)] = service;
        }
    }
    
    public interface ICallerService
    {
        Dictionary<(Type, int), IRPCCaller> Callers { get; }
        void AddCaller(Type type, NetworkService service);
        void AddCaller(Type type, NetworkBehaviour service);
    }
}