using Skynet.RpcSystem;
using Skynet.RpcSystem.Processors;
using Skynet.Data.NetworkObjects;
using Skynet.FormatterSystem;
using Skynet.Initializer;
using Skynet.NetworkComponents.RpcComponents;
using Skynet.RpcSystem.Callers;
using Skynet.RpcSystem.DynamicProcessor;
using Skynet.RpcSystem.Processors;
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
            builder.Register<IRpcListener, RpcListener>(Lifetime.Singleton);
            builder.Register<ICallerService, CallerService>(Lifetime.Singleton);
            builder.Register<IRpcReceiveProcessor, RpcReceiveProcessor>(Lifetime.Singleton);
            builder.Register<IRpcSendProcessor, RpcSendProcessor>(Lifetime.Singleton);
            builder.Register<IDynamicProcessorService, DynamicProcessorService>(Lifetime.Singleton);
            builder.Register<INetworkInitializer, NetworkInitializer>(Lifetime.Singleton);
            builder.Register<INetworkObjectSyncer, NetworkObjectsSyncer>(Lifetime.Singleton);
            builder.Register<INetworkSpawner, NetworkSpawner>(Lifetime.Singleton).WithParameter(_networkObjectsConfig);
            
            builder.RegisterInstance(GameObject);
        }
    }
}