using _Scripts.Netcore.Runner;
using VContainer;
using VContainer.Unity;

namespace _Scripts.Infrastructure.Installers
{
    public class MainInstaller : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterEntryPoint<Bootstrapper>();
            
            builder.Register<INetworkRunner, NetworkRunner>(Lifetime.Singleton);
        }
    }
}