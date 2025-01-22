using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using _Scripts.Netcore.Runner;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace _Scripts.Netcore.RPCSystem.Processors
{
    public class RPCSendProcessor : IRPCSendProcessor
    {
        private INetworkRunner _networkRunner;

        public ConcurrentQueue<byte[]> TcpSendQueue { get; } = new();
        public ConcurrentQueue<byte[]> UdpSendQueue { get; } = new();

        public void Initialize(INetworkRunner networkRunner)
        {
            _networkRunner = networkRunner;
        }
        
        public async UniTask ProcessTcpSendQueue(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (TcpSendQueue.TryDequeue(out var data))
                {
                    if (_networkRunner.IsServer)
                    {
                        foreach (var socket in _networkRunner.TcpClientSockets.Where(socket => socket.Connected))
                        {
                            try
                            {
                                await socket.SendAsync(new ArraySegment<byte>(data), SocketFlags.None);
                            }
                            catch (Exception ex)
                            {
                                Debug.LogError($"TCP Send Error: {ex.Message}");
                            }
                        }
                    }
                    else
                    {
                        if (_networkRunner.TcpServerSocket.Connected)
                        {
                            try
                            {
                                await _networkRunner.TcpServerSocket.SendAsync(new ArraySegment<byte>(data), SocketFlags.None);
                            }
                            catch (Exception ex)
                            {
                                Debug.LogError($"TCP Send Error: {ex.Message}");
                            }
                        }
                    }
                }

                await UniTask.Yield();
            }
        }

        public async UniTask ProcessUdpSendQueue(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (UdpSendQueue.TryDequeue(out var data))
                {
                    if (_networkRunner.IsServer)
                    {
                        foreach (var socket in _networkRunner.UdpClientSockets)
                        {
                            try
                            {
                                if (socket.RemoteEndPoint is IPEndPoint remoteEndPoint)
                                    await socket.SendToAsync(new ArraySegment<byte>(data), SocketFlags.None,
                                        remoteEndPoint);
                            }
                            catch (Exception ex)
                            {
                                Debug.LogError($"UDP Send Error: {ex.Message}");
                            }
                        }
                    }
                    else
                    {
                        try
                        {
                            IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 5057);
                            // if (_runner.UdpServerSocket.RemoteEndPoint is IPEndPoint remoteEndPoint)
                            await _networkRunner.UdpServerSocket.SendToAsync(new ArraySegment<byte>(data), SocketFlags.None,
                                remoteEndPoint);
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"UDP Send Error: {ex.Message}");
                        }
                    }
                }

                await UniTask.Yield();
            }
        }
    }

    public interface IRPCSendProcessor
    {
        void Initialize(INetworkRunner networkRunner);
        
        ConcurrentQueue<byte[]> TcpSendQueue { get; }
        ConcurrentQueue<byte[]> UdpSendQueue { get; }

        UniTask ProcessTcpSendQueue(CancellationToken cancellationToken);
        UniTask ProcessUdpSendQueue(CancellationToken cancellationToken);
    }
}