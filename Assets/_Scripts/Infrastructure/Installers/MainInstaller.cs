using Skynet.Configuration;
using Skynet.Diagnostics;
using Skynet.NetworkComponents;
using Skynet.Runner;
using Skynet.RpcSystem;
using Skynet.Spawner;
using Skynet.Threading;
using Skynet.Tick;
using Skynet.Transport;
using Skynet.Unity.Diagnostics;
using Skynet.Unity.Formatters;
using Skynet.Unity.Threading;
using Skynet.Unity.Tick;
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

            // Diagnostics + threading (Unity slot)
            builder.Register<ISkynetLogger, UnityLogger>(Lifetime.Singleton);
            builder.Register<IMainThreadDispatcher, UnityMainThreadDispatcher>(Lifetime.Singleton);

            // Network tick — PlayerLoop-driven, no scene component. Tick rate lives in NetworkTickSettings.
            // Force eager resolution so the scheduler installs its PlayerLoop hook at scope startup
            // (and its Dispose runs at scope shutdown to remove the hook).
            builder.RegisterInstance(new NetworkTickSettings(tickRate: 30));
            builder.Register<PlayerLoopNetworkTickScheduler>(Lifetime.Singleton).As<INetworkTickScheduler>();
            builder.RegisterBuildCallback(container => container.Resolve<INetworkTickScheduler>());

            // Formatters (kept in old location for now — will move to Skynet.Unity slot in Stage 6).
            // Force eager init so MessagePack's default options carry Vector3/Quaternion formatters
            // before any snapshot serialization runs.
            builder.Register<INetworkFormatter, NetworkFormatter>(Lifetime.Singleton);
            builder.RegisterBuildCallback(container => container.Resolve<INetworkFormatter>().Initialize());

            // Skynet.Core RPC + Transport pipeline. Note: NetworkRunner creates ClockSyncService internally,
            // no separate registration needed.
            builder.Register<ITransport, DualSocketTransport>(Lifetime.Singleton);
            builder.Register<IRpcHandlerRegistry, RpcHandlerRegistry>(Lifetime.Singleton);
            builder.Register<IRpcSender, RpcSender>(Lifetime.Singleton);
            builder.Register<IRpcDispatcher, RpcDispatcher>(Lifetime.Singleton);
            builder.Register<NetworkRunner>(Lifetime.Singleton);

            // Spawner + object container
            builder.Register<INetworkObjectContainer, NetworkObjectsContainer>(Lifetime.Singleton);
            builder.Register<INetworkSpawner, NetworkSpawner>(Lifetime.Singleton).WithParameter(_networkObjectsConfig);

            builder.RegisterInstance(GameObject);
        }
    }
}
