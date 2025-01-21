using System;
using System.Collections.Generic;
using System.Threading;
using _Scripts.Netcore.Proxy.Processors;
using Cysharp.Threading.Tasks;

namespace _Scripts.Netcore.Proxy.DynamicProcessor
{
    public class DynamicProcessorService : IDynamicProcessorService, IDisposable
    {
        private readonly List<CancellationTokenSource> _tcpCancellationTokens = new();
        private readonly List<CancellationTokenSource> _udpCancellationTokens = new();
        
        private static readonly List<CancellationTokenSource> _tcpSendCancellationTokens = new();
        private static readonly List<CancellationTokenSource> _udpSendCancellationTokens = new();
        
        private readonly IRpcReceiveProcessor _rpcReceiveProcessor;
        private readonly IRPCSendProcessor _rpcSendProcessor;

        public DynamicProcessorService(IRpcReceiveProcessor rpcReceiveProcessor,
            IRPCSendProcessor rpcSendProcessor)
        {
            _rpcReceiveProcessor = rpcReceiveProcessor;
            _rpcSendProcessor = rpcSendProcessor;
        }
        
        public void Initialize()
        {
            for (int i = 0; i < 5; i++)
            {
                var tcpTokenSource = new CancellationTokenSource();
                _tcpCancellationTokens.Add(tcpTokenSource);
                
                _rpcReceiveProcessor.ProcessTcpReceiveQueue(tcpTokenSource.Token)
                    .AttachExternalCancellation(tcpTokenSource.Token);
            }

            for (int i = 0; i < 5; i++)
            {
                var udpTokenSource = new CancellationTokenSource();
                _udpCancellationTokens.Add(udpTokenSource);
                
                _rpcReceiveProcessor.ProcessUdpReceiveQueue(udpTokenSource.Token)
                    .AttachExternalCancellation(udpTokenSource.Token);
            }
            
            for (int i = 0; i < 5; i++)
            {
                var tcpTokenSource = new CancellationTokenSource();
                _tcpSendCancellationTokens.Add(tcpTokenSource);
                
                _rpcSendProcessor.ProcessTcpSendQueue(tcpTokenSource.Token)
                    .AttachExternalCancellation(tcpTokenSource.Token);
            }
            
            for (int i = 0; i < 5; i++)
            {
                var udpTokenSource = new CancellationTokenSource();
                _udpSendCancellationTokens.Add(udpTokenSource);
                
                _rpcSendProcessor.ProcessUdpSendQueue(udpTokenSource.Token)
                    .AttachExternalCancellation(udpTokenSource.Token);
            }
        }
        
        public void Dispose()
        {
            for (var i = 0; i < _tcpCancellationTokens.Count; i++)
                _tcpCancellationTokens[i].Dispose();

            for (int i = 0; i < _udpCancellationTokens.Count; i++)
                _tcpCancellationTokens[i].Dispose();
            
            for (var i = 0; i < _tcpSendCancellationTokens.Count; i++)
                _tcpSendCancellationTokens[i].Dispose();

            for (int i = 0; i < _udpSendCancellationTokens.Count; i++)
                _tcpSendCancellationTokens[i].Dispose();
        }
    }

    public interface IDynamicProcessorService
    {
        void Initialize();
    }
}