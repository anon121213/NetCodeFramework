using _Scripts.Netcore.Data.NetworkObjects;
using _Scripts.Netcore.FormatterSystem;
using _Scripts.Netcore.Initializer;
using _Scripts.Netcore.RPCSystem;
using _Scripts.Netcore.RPCSystem.Callers;
using _Scripts.Netcore.RPCSystem.DynamicProcessor;
using _Scripts.Netcore.RPCSystem.Processors;
using _Scripts.Netcore.Runner;
using _Scripts.Netcore.Spawner;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace _Scripts.Infrastructure.Installers
{
    public class MainInstaller : LifetimeScope
    {
        [SerializeField] private NetworkObjectsConfig _networkObjectsConfig;
        public GameObject GameObject;
        
        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterEntryPoint<Bootstrapper>();
            
            builder.Register<INetworkRunner, NetworkRunner>(Lifetime.Singleton);
            builder.Register<INetworkFormatter, NetworkFormatter>(Lifetime.Singleton);
            builder.Register<IRpcListener, RPCListener>(Lifetime.Singleton);
            builder.Register<ICallerService, CallerService>(Lifetime.Singleton);
            builder.Register<IRpcReceiveProcessor, RpcReceiveReceiveProcessor>(Lifetime.Singleton);
            builder.Register<IRPCSendProcessor, RPCSendProcessor>(Lifetime.Singleton);
            builder.Register<IDynamicProcessorService, DynamicProcessorService>(Lifetime.Singleton);
            builder.Register<INetworkInitializer, NetworkInitializer>(Lifetime.Singleton);
            builder.Register<INetworkSpawner, NetworkSpawner>(Lifetime.Singleton).WithParameter(_networkObjectsConfig);
            
            builder.RegisterInstance(GameObject);
        }
    }
}