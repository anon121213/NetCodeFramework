using System.Collections.Concurrent;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Skynet.RpcSystem.Processors
{
    public interface IRpcReceiveProcessor
    {
        ConcurrentQueue<byte[]> TcpReceiveQueue { get; }
        ConcurrentQueue<byte[]> UdpReceiveQueue { get; }
        UniTask ProcessTcpReceiveQueue(CancellationToken cancellationToken);
        UniTask ProcessUdpReceiveQueue(CancellationToken cancellationToken);
    }
}