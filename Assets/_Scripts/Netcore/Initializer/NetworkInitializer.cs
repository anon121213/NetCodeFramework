using Skynet.RpcSystem.Processors;
using Skynet.FormatterSystem;
using Skynet.NetworkComponents.NetworkVariableComponent.Processor;
using Skynet.RpcSystem;
using Skynet.RpcSystem.Callers;
using Skynet.RpcSystem.DynamicProcessor;
using Skynet.Runner;

namespace Skynet.Initializer
{
    public class NetworkInitializer : INetworkInitializer
    {
        private readonly INetworkFormatter _networkFormatter;
        private readonly IRpcSendProcessor _rpcSendProcessor;
        private readonly IDynamicProcessorService _dynamicProcessorService;
        private readonly ICallerService _callerService;

        public NetworkInitializer(INetworkFormatter networkFormatter,
            IRpcSendProcessor rpcSendProcessor,
            IDynamicProcessorService dynamicProcessorService,
            ICallerService callerService)
        {
            _networkFormatter = networkFormatter;
            _rpcSendProcessor = rpcSendProcessor;
            _dynamicProcessorService = dynamicProcessorService;
            _callerService = callerService;
        }
        
        public void Initialize(INetworkRunner networkRunner)
        {
            _networkFormatter.Initialize();
            _dynamicProcessorService.Initialize();
            _rpcSendProcessor.Initialize(networkRunner);
            RpcInvoker.Initialize(_rpcSendProcessor, _callerService);
            NetworkVariableProcessor.Instance.Initialize(networkRunner);
        }
    }

    public interface INetworkInitializer
    {
        void Initialize(INetworkRunner networkRunner);
    }
}