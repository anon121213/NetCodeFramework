using System;
using System.Buffers;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using _Scripts.Netcore.RPCSystem.Processors;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace _Scripts.Netcore.RPCSystem
{
    public class RPCListener : IRpcListener
    {
        private readonly IRpcReceiveProcessor _receiveProcessor;

        public RPCListener(IRpcReceiveProcessor receiveProcessor)
        {
            _receiveProcessor = receiveProcessor;
        }
        
        public async UniTask ListenForTcpRpcCalls(Socket socket, CancellationToken cancellationToken)
        {
            byte[] buffer = ArrayPool<byte>.Shared.Rent(1024 * 64);
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), SocketFlags.None);

                        if (result <= 0)
                            continue;

                        var receivedData = new byte[result];
                        Array.Copy(buffer, receivedData, result);
                        _receiveProcessor.TcpReceiveQueue.Enqueue(receivedData);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"TCP Receive Exception: {ex.Message}");
                        await UniTask.Delay(60, cancellationToken: cancellationToken);
                    }
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }


        public async UniTask ListenForUdpRpcCalls(Socket socket, IPEndPoint ipEndPoint,
            CancellationToken cancellationToken)
        {
            byte[] buffer = ArrayPool<byte>.Shared.Rent(1024 * 128);
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        var result = await socket.ReceiveFromAsync(new ArraySegment<byte>(buffer), SocketFlags.None,
                            ipEndPoint);

                        if (result.ReceivedBytes <= 0)
                            continue;

                        var receivedData = new byte[result.ReceivedBytes];
                        Array.Copy(buffer, receivedData, result.ReceivedBytes);
                        _receiveProcessor.UdpReceiveQueue.Enqueue(receivedData);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"UDP Receive Exception: {ex.Message}");
                        await UniTask.Delay(60, cancellationToken: cancellationToken);
                    }
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }

    public interface IRpcListener
    {
        UniTask ListenForTcpRpcCalls(Socket socket, CancellationToken cancellationToken);
        UniTask ListenForUdpRpcCalls(Socket socket, IPEndPoint ipEndPoint, CancellationToken cancellationToken);
    }
}