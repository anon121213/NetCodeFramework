using _Scripts.Netcore.FormatterSystem;
using _Scripts.Netcore.NetworkComponents.NetworkVariableComponent.Processor;
using _Scripts.Netcore.RPCSystem;
using _Scripts.Netcore.RPCSystem.Callers;
using _Scripts.Netcore.RPCSystem.DynamicProcessor;
using _Scripts.Netcore.RPCSystem.Processors;
using _Scripts.Netcore.Runner;

namespace _Scripts.Netcore.Initializer
{
    public class NetworkInitializer : INetworkInitializer
    {
        private readonly INetworkFormatter _networkFormatter;
        private readonly IRPCSendProcessor _rpcSendProcessor;
        private readonly IDynamicProcessorService _dynamicProcessorService;
        private readonly ICallerService _callerService;

        public NetworkInitializer(INetworkFormatter networkFormatter,
            IRPCSendProcessor rpcSendProcessor,
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
            RPCInvoker.Initialize(_rpcSendProcessor, _callerService);
            NetworkVariableProcessor.Instance.Initialize(networkRunner);
        }
    }

    public interface INetworkInitializer
    {
        void Initialize(INetworkRunner networkRunner);
    }
}