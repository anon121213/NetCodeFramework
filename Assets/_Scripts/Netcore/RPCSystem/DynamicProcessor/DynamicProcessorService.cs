using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using _Scripts.Netcore.RPCSystem.Processors;
using _Scripts.Netcore.RPCSystem.ProcessorsData;
using Cysharp.Threading.Tasks;

namespace _Scripts.Netcore.RPCSystem.DynamicProcessor
{
    public class DynamicProcessorService : IDynamicProcessorService, IDisposable
    {
        private readonly Dictionary<(NetProtocolType, ProcessorType), (List<CancellationTokenSource> tokens, int count)> _processors = new();
        private readonly CancellationTokenSource _checkerCancellationToken = new();
        
        private readonly IRpcReceiveProcessor _rpcReceiveProcessor;
        private readonly IRPCSendProcessor _rpcSendProcessor;

        public DynamicProcessorService(IRpcReceiveProcessor rpcReceiveProcessor, IRPCSendProcessor rpcSendProcessor)
        {
            _rpcReceiveProcessor = rpcReceiveProcessor;
            _rpcSendProcessor = rpcSendProcessor;
            
            InitializeProcessorDictionary();
        }

        private void InitializeProcessorDictionary()
        {
            foreach (NetProtocolType protocolType in Enum.GetValues(typeof(NetProtocolType)))
                foreach (ProcessorType processorType in Enum.GetValues(typeof(ProcessorType)))
                    _processors[(protocolType, processorType)] = (new List<CancellationTokenSource>(), 0);
        }

        public void Initialize()
        {
            InitializeProcessors(3);

            StartQueueLoadCheck(_rpcReceiveProcessor.TcpReceiveQueue, NetProtocolType.Tcp, ProcessorType.Receive)
                .AttachExternalCancellation(_checkerCancellationToken.Token);
            
            StartQueueLoadCheck(_rpcReceiveProcessor.UdpReceiveQueue, NetProtocolType.Udp, ProcessorType.Receive)
                .AttachExternalCancellation(_checkerCancellationToken.Token);
            
            StartQueueLoadCheck(_rpcSendProcessor.TcpSendQueue, NetProtocolType.Tcp, ProcessorType.Send)
                .AttachExternalCancellation(_checkerCancellationToken.Token);
            
            StartQueueLoadCheck(_rpcSendProcessor.UdpSendQueue, NetProtocolType.Udp, ProcessorType.Send)
                .AttachExternalCancellation(_checkerCancellationToken.Token);
        }

        private void InitializeProcessors(int count)
        {
            for (int i = 0; i < count; i++)
            {
                StartNewProcessor(NetProtocolType.Tcp, ProcessorType.Receive);
                StartNewProcessor(NetProtocolType.Tcp, ProcessorType.Send);
                StartNewProcessor(NetProtocolType.Udp, ProcessorType.Receive);
                StartNewProcessor(NetProtocolType.Udp, ProcessorType.Send);
            }
        }

        private void StartNewProcessor(NetProtocolType netProtocolType, ProcessorType processorType)
        {
            var (tokens, count) = _processors[(netProtocolType, processorType)];
            var cancellationTokenSource = new CancellationTokenSource();
            CancellationToken token = cancellationTokenSource.Token;

            tokens.Add(cancellationTokenSource);
            _processors[(netProtocolType, processorType)] = (tokens, count + 1);

            if (processorType == ProcessorType.Send)
                if (netProtocolType == NetProtocolType.Tcp)
                    _rpcSendProcessor.ProcessTcpSendQueue(token).AttachExternalCancellation(token);
                else
                    _rpcSendProcessor.ProcessUdpSendQueue(token).AttachExternalCancellation(token);
            else
                if (netProtocolType == NetProtocolType.Tcp)
                    _rpcReceiveProcessor.ProcessTcpReceiveQueue(token).AttachExternalCancellation(token);
                else
                    _rpcReceiveProcessor.ProcessUdpReceiveQueue(token).AttachExternalCancellation(token);
        }

        private async UniTask StartQueueLoadCheck(ConcurrentQueue<byte[]> queue,
            NetProtocolType netProtocolType, ProcessorType processorType)
        {
            while (true)
            {
                var (_, count) = _processors[(netProtocolType, processorType)];

                if (queue.Count > 2)
                    StartNewProcessor(netProtocolType, processorType);
                else if (queue.Count == 0 && count > 1) 
                    HandleProcessorStop(netProtocolType, processorType);

                await UniTask.Delay(500);
            }
        }

        private void HandleProcessorStop(NetProtocolType netProtocolType, ProcessorType processorType)
        {
            var (tokens, count) = _processors[(netProtocolType, processorType)];

            if (count > 1)
            {
                var cancellationTokenSource = tokens[0];
                cancellationTokenSource.Dispose();
                tokens.RemoveAt(0);
                _processors[(netProtocolType, processorType)] = (tokens, count - 1);
            }
        }

        public void Dispose()
        {
            foreach (var processor in _processors.Values)
            {
                foreach (var tokenSource in processor.tokens)
                {
                    tokenSource?.Dispose();
                }
            }

            _processors.Clear();
            _checkerCancellationToken?.Dispose();
        }
    }
}
