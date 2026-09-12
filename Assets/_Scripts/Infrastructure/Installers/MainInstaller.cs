using _Scripts.Netcore.RPCSystem;
using _Scripts.Netcore.RPCSystem.Processors;
using Skynet.Data.NetworkObjects;
using Skynet.FormatterSystem;
using Skynet.Initializer;
using Skynet.NetworkComponents.RPCComponents;
using Skynet.RPCSystem.Callers;
using Skynet.RPCSystem.DynamicProcessor;
using Skynet.RPCSystem.Processors;
using Skynet.Runner;
using Skynet.Spawner;
using Skynet.Spawner.ObjectsSyncer;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace _Scripts.Infrastructure.Installers
{
    public class MainInstaller : LifetimeScope
    {
        [SerializeField] private NetworkObjectsConfig _networkObjectsConfig;
        [SerializeField] private NetworkObject GameObject;
        
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
            builder.Register<INetworkObjectSyncer, NetworkObjectsSyncer>(Lifetime.Singleton);
            builder.Register<INetworkSpawner, NetworkSpawner>(Lifetime.Singleton).WithParameter(_networkObjectsConfig);
            
            builder.RegisterInstance(GameObject);
        }
    }
}