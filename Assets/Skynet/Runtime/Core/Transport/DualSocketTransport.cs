using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Skynet.Data.ConnectionData;
using Skynet.Diagnostics;
using Skynet.RpcSystem.ProcessorsData;

namespace Skynet.Transport
{
    public sealed class DualSocketTransport : ITransport
    {
        public const int HeaderSize = sizeof(int);
        public const int MaxPayloadSize = 65536;

        private readonly ISkynetLogger _logger;
        private CancellationTokenSource _cts = new();

        private bool _isRunning;

        #region Server

        private Socket _tcpListener;
        private Socket _udpListener;

        private readonly ConcurrentDictionary<int, ClientConnection> _clients = new();
        private readonly ConcurrentDictionary<IPEndPoint, int> _endPointToClients = new();

        private int _nextClientId;

        private Task _acceptTask;
        private Task _serverUdpReceiveTask;

        #endregion

        #region Client

        private Socket _tcpToServer;
        private Socket _udp;

        private IPEndPoint _serverEndPoint;

        private int _myClientId;

        private Channel<OutgoingPacket> _tcpOut;
        private Channel<OutgoingPacket> _udpOut;

        private Task _tcpReceiveTask, _tcpSendTask, _clientUdpReceiveTask, _udpSendTask;

        #endregion

        public bool IsServer { get; private set; }

        public bool IsRunning => _isRunning;

        public event DataReceivedHandler OnDataReceived;
        public event Action<int> OnClientConnected;
        public event Action<int> OnClientDisconnected;
        public event Action OnConnectedToServer;
        public event Action OnDisconnectedFromServer;

        public DualSocketTransport(ISkynetLogger logger)
        {
            _logger = logger;
        }

        // ---------- Lifecycle ----------

        public Task StartAsServerAsync(ConnectServerData config, CancellationToken cancellationToken)
        {
            if (_isRunning) throw new InvalidOperationException("Transport already running");
            IsServer = true;
            _cts = new CancellationTokenSource();

            _tcpListener = new Socket(AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp)
            {
                DualMode = true,
            };
            _tcpListener.Bind(new IPEndPoint(IPAddress.IPv6Any, config.TcpPort));
            _tcpListener.Listen(config.MaxClients);

            _udpListener = new Socket(AddressFamily.InterNetworkV6, SocketType.Dgram, ProtocolType.Udp)
            {
                DualMode = true,
            };
            _udpListener.Bind(new IPEndPoint(IPAddress.IPv6Any, config.UdpPort));

            _isRunning = true;

            _acceptTask = Task.Run(() => AcceptLoop(_cts.Token));
            _serverUdpReceiveTask = Task.Run(() => ServerUdpReceiveLoop(_cts.Token));

            return Task.CompletedTask;
        }

        public async Task StartAsClientAsync(ConnectClientData config, CancellationToken cancellationToken)
        {
            if (_isRunning) throw new InvalidOperationException("Transport already running");
            IsServer = false;
            _cts = new CancellationTokenSource();

            _tcpToServer = new Socket(AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp)
            {
                DualMode = true,
            };
            await _tcpToServer.ConnectAsync(config.Ip, config.TcpPort);

            var lengthBuf = new byte[HeaderSize];
            if (!await ReceiveExactAsync(_tcpToServer, lengthBuf, _cts.Token))
                throw new IOException("Server closed connection during handshake");

            int payloadLen = BinaryPrimitives.ReadInt32LittleEndian(lengthBuf);
            if (payloadLen != HeaderSize)
                throw new IOException($"Bad handshake: expected {HeaderSize} bytes, got {payloadLen}");

            var idBuf = new byte[HeaderSize];
            if (!await ReceiveExactAsync(_tcpToServer, idBuf, _cts.Token))
                throw new IOException("Server closed during handshake");
            _myClientId = BinaryPrimitives.ReadInt32LittleEndian(idBuf);

            _udp = new Socket(AddressFamily.InterNetworkV6, SocketType.Dgram, ProtocolType.Udp)
            {
                DualMode = true,
            };
            _udp.Bind(new IPEndPoint(IPAddress.IPv6Any, 0));

            // Map IPv4 target to IPv4-mapped IPv6 so SendToAsync from a DualMode socket
            // hits the right address family.
            var serverIp = config.Ip.AddressFamily == AddressFamily.InterNetwork
                ? config.Ip.MapToIPv6()
                : config.Ip;
            _serverEndPoint = new IPEndPoint(serverIp, config.UdpPort);

            _tcpOut = CreateTcpChannel();
            _udpOut = CreateUdpChannel();

            _isRunning = true;

            _tcpReceiveTask = Task.Run(() => ClientTcpReceiveLoop(_cts.Token));
            _tcpSendTask = Task.Run(() => ClientTcpSendLoop(_cts.Token));
            _clientUdpReceiveTask = Task.Run(() => ClientUdpReceiveLoop(_cts.Token));
            _udpSendTask = Task.Run(() => ClientUdpSendLoop(_cts.Token));

            OnConnectedToServer?.Invoke();
        }

        public async Task StopAsync()
        {
            if (!_isRunning) return;
            _isRunning = false;

            _cts.Cancel();

            _tcpListener?.Close();
            _udpListener?.Close();
            _tcpToServer?.Close();
            _udp?.Close();

            var tasks = new List<Task>();
            if (_acceptTask != null) tasks.Add(_acceptTask);
            if (_serverUdpReceiveTask != null) tasks.Add(_serverUdpReceiveTask);
            if (_tcpReceiveTask != null) tasks.Add(_tcpReceiveTask);
            if (_tcpSendTask != null) tasks.Add(_tcpSendTask);
            if (_clientUdpReceiveTask != null) tasks.Add(_clientUdpReceiveTask);
            if (_udpSendTask != null) tasks.Add(_udpSendTask);

            foreach (var conn in _clients.Values)
            {
                conn.Cts?.Cancel();
                try { conn.Tcp?.Close(); } catch { }
                if (conn.TcpReceiveTask != null) tasks.Add(conn.TcpReceiveTask);
                if (conn.TcpSendTask != null) tasks.Add(conn.TcpSendTask);
                if (conn.UdpSendTask != null) tasks.Add(conn.UdpSendTask);
            }

            try
            {
                await Task.WhenAny(Task.WhenAll(tasks), Task.Delay(TimeSpan.FromSeconds(2)));
            }
            catch (Exception ex)
            {
                _logger.Warn($"Some transport tasks didn't stop cleanly: {ex.Message}");
            }

            _clients.Clear();
            _endPointToClients.Clear();
            _cts.Dispose();
        }

        public void Dispose() => StopAsync().GetAwaiter().GetResult();

        // ---------- Send API ----------

        public void Send(int clientId, ReadOnlySpan<byte> payload, NetProtocolType protocol)
        {
            if (!IsServer || !_isRunning) return;
            if (!_clients.TryGetValue(clientId, out var conn)) return;

            var packet = BuildPacket(clientId, payload, protocol);
            var channel = protocol == NetProtocolType.Tcp ? conn.TcpOut : conn.UdpOut;

            if (!channel.Writer.TryWrite(packet))
            {
                ArrayPool<byte>.Shared.Return(packet.Buffer);
                _logger.Warn($"Send channel full for client {clientId} ({protocol})");
            }
        }

        public void Broadcast(ReadOnlySpan<byte> payload, NetProtocolType protocol)
        {
            if (!IsServer || !_isRunning) return;

            foreach (var conn in _clients.Values)
            {
                var packet = BuildPacket(conn.ClientId, payload, protocol);
                var channel = protocol == NetProtocolType.Tcp ? conn.TcpOut : conn.UdpOut;

                if (!channel.Writer.TryWrite(packet))
                {
                    ArrayPool<byte>.Shared.Return(packet.Buffer);
                    _logger.Warn($"Send channel full for client {conn.ClientId} ({protocol})");
                }
            }
        }

        public void SendToServer(ReadOnlySpan<byte> payload, NetProtocolType protocol)
        {
            if (IsServer || !_isRunning) return;

            var packet = BuildPacket(_myClientId, payload, protocol);
            var channel = protocol == NetProtocolType.Tcp ? _tcpOut : _udpOut;

            if (!channel.Writer.TryWrite(packet))
            {
                ArrayPool<byte>.Shared.Return(packet.Buffer);
                _logger.Warn($"Send channel full to server ({protocol})");
            }
        }

        // ---------- Server loops ----------

        private async Task AcceptLoop(CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    Socket clientSocket;
                    try
                    {
                        clientSocket = await _tcpListener.AcceptAsync();
                    }
                    catch (ObjectDisposedException) { break; }
                    catch (SocketException) when (ct.IsCancellationRequested) { break; }

                    int clientId = Interlocked.Increment(ref _nextClientId);
                    var conn = new ClientConnection
                    {
                        ClientId = clientId,
                        Tcp = clientSocket,
                        TcpOut = CreateTcpChannel(),
                        UdpOut = CreateUdpChannel(),
                        Cts = CancellationTokenSource.CreateLinkedTokenSource(ct),
                    };
                    _clients[clientId] = conn;

                    var handshake = new byte[HeaderSize + HeaderSize];
                    BinaryPrimitives.WriteInt32LittleEndian(handshake.AsSpan(0, HeaderSize), HeaderSize);
                    BinaryPrimitives.WriteInt32LittleEndian(handshake.AsSpan(HeaderSize), clientId);
                    try
                    {
                        await clientSocket.SendAsync(handshake.AsMemory(), SocketFlags.None, ct);
                    }
                    catch (Exception ex)
                    {
                        _logger.Warn($"Handshake failed for client {clientId}: {ex.Message}");
                        DisconnectClient(conn);
                        continue;
                    }

                    conn.TcpReceiveTask = Task.Run(() => ServerTcpReceiveLoop(conn));
                    conn.TcpSendTask = Task.Run(() => ServerTcpSendLoop(conn));
                    conn.UdpSendTask = Task.Run(() => ServerUdpSendLoop(conn));

                    OnClientConnected?.Invoke(clientId);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { _logger.Error("Accept loop crashed", ex); }
        }

        private async Task ServerTcpReceiveLoop(ClientConnection conn)
        {
            var ct = conn.Cts.Token;
            var lengthBuf = new byte[HeaderSize];

            try
            {
                while (!ct.IsCancellationRequested)
                {
                    if (!await ReceiveExactAsync(conn.Tcp, lengthBuf, ct)) break;

                    int payloadLen = BinaryPrimitives.ReadInt32LittleEndian(lengthBuf);
                    if (payloadLen <= 0 || payloadLen > MaxPayloadSize)
                    {
                        _logger.Error($"Invalid TCP payload length from client {conn.ClientId}: {payloadLen}");
                        break;
                    }

                    var payloadBuffer = ArrayPool<byte>.Shared.Rent(payloadLen);
                    try
                    {
                        if (!await ReceiveExactAsync(conn.Tcp, payloadBuffer.AsMemory(0, payloadLen), ct)) break;
                        OnDataReceived?.Invoke(conn.ClientId, payloadBuffer.AsSpan(0, payloadLen), NetProtocolType.Tcp);
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(payloadBuffer);
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (SocketException ex) when (ex.SocketErrorCode is SocketError.ConnectionReset or SocketError.ConnectionAborted) { }
            catch (Exception ex)
            {
                _logger.Error($"Server TCP receive loop for client {conn.ClientId} crashed", ex);
            }

            DisconnectClient(conn);
        }

        private async Task ServerTcpSendLoop(ClientConnection conn)
        {
            var ct = conn.Cts.Token;
            try
            {
                await foreach (var packet in conn.TcpOut.Reader.ReadAllAsync(ct))
                {
                    try
                    {
                        var mem = packet.Buffer.AsMemory(0, packet.Length);
                        int sent = 0;
                        while (sent < mem.Length)
                        {
                            int n = await conn.Tcp.SendAsync(mem.Slice(sent), SocketFlags.None, ct);
                            if (n == 0) return;
                            sent += n;
                        }
                    }
                    catch (SocketException ex)
                    {
                        _logger.Warn($"TCP send to client {conn.ClientId} failed: {ex.Message}");
                        return;
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(packet.Buffer);
                    }
                }
            }
            catch (OperationCanceledException) { }
        }

        private async Task ServerUdpSendLoop(ClientConnection conn)
        {
            var ct = conn.Cts.Token;
            try
            {
                // Block until we know where to send — client's first inbound UDP populates UdpEndPoint
                // and signals UdpReady. Outgoing UDP piles up in the DropOldest channel meanwhile,
                // so stale messages naturally roll off; anything queued when the endpoint arrives is
                // flushed as normal.
                using (ct.Register(() => conn.UdpReady.TrySetCanceled()))
                    await conn.UdpReady.Task;

                await foreach (var packet in conn.UdpOut.Reader.ReadAllAsync(ct))
                {
                    try
                    {
                        // SendToAsync on netstandard2.1 requires ArraySegment — no Memory overload yet.
                        await _udpListener.SendToAsync(
                            new ArraySegment<byte>(packet.Buffer, 0, packet.Length),
                            SocketFlags.None,
                            conn.UdpEndPoint);
                    }
                    catch (SocketException ex)
                    {
                        _logger.Warn($"UDP send to client {conn.ClientId} failed: {ex.Message}");
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(packet.Buffer);
                    }
                }
            }
            catch (OperationCanceledException) { }
        }

        private async Task ServerUdpReceiveLoop(CancellationToken ct)
        {
            var buffer = ArrayPool<byte>.Shared.Rent(MaxPayloadSize);
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    SocketReceiveFromResult result;
                    EndPoint remoteEp = new IPEndPoint(IPAddress.IPv6Any, 0);
                    try
                    {
                        result = await _udpListener.ReceiveFromAsync(
                            new ArraySegment<byte>(buffer),
                            SocketFlags.None,
                            remoteEp);
                    }
                    catch (SocketException ex)
                    {
                        _logger.Warn($"Server UDP receive: {ex.Message}");
                        continue;
                    }
                    catch (ObjectDisposedException) { break; }

                    if (result.ReceivedBytes < HeaderSize) continue;

                    int clientId = BinaryPrimitives.ReadInt32LittleEndian(buffer);
                    if (!_clients.TryGetValue(clientId, out var conn)) continue;

                    var senderEp = (IPEndPoint)result.RemoteEndPoint;
                    if (!Equals(conn.UdpEndPoint, senderEp))
                    {
                        if (conn.UdpEndPoint != null)
                            _endPointToClients.TryRemove(conn.UdpEndPoint, out _);
                        conn.UdpEndPoint = senderEp;
                        _endPointToClients[senderEp] = clientId;
                        // Unblocks ServerUdpSendLoop on first endpoint discovery; TrySetResult is
                        // idempotent, so later NAT-rebinding updates are a no-op here.
                        conn.UdpReady.TrySetResult(true);
                    }

                    OnDataReceived?.Invoke(
                        clientId,
                        buffer.AsSpan(HeaderSize, result.ReceivedBytes - HeaderSize),
                        NetProtocolType.Udp);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { _logger.Error("Server UDP receive loop crashed", ex); }
            finally { ArrayPool<byte>.Shared.Return(buffer); }
        }

        // ---------- Client loops ----------

        private async Task ClientTcpReceiveLoop(CancellationToken ct)
        {
            var lengthBuf = new byte[HeaderSize];

            try
            {
                while (!ct.IsCancellationRequested)
                {
                    if (!await ReceiveExactAsync(_tcpToServer, lengthBuf, ct)) break;

                    int payloadLen = BinaryPrimitives.ReadInt32LittleEndian(lengthBuf);
                    if (payloadLen <= 0 || payloadLen > MaxPayloadSize)
                    {
                        _logger.Error($"Invalid TCP payload length: {payloadLen}");
                        break;
                    }

                    var payloadBuffer = ArrayPool<byte>.Shared.Rent(payloadLen);
                    try
                    {
                        if (!await ReceiveExactAsync(_tcpToServer, payloadBuffer.AsMemory(0, payloadLen), ct)) break;
                        OnDataReceived?.Invoke(0, payloadBuffer.AsSpan(0, payloadLen), NetProtocolType.Tcp);
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(payloadBuffer);
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (SocketException ex) when (ex.SocketErrorCode is SocketError.ConnectionReset or SocketError.ConnectionAborted)
            {
                _logger.Info($"TCP disconnected: {ex.Message}");
            }
            catch (Exception ex)
            {
                _logger.Error("TCP receive loop crashed", ex);
            }

            OnDisconnectedFromServer?.Invoke();
        }

        private async Task ClientTcpSendLoop(CancellationToken ct)
        {
            try
            {
                await foreach (var packet in _tcpOut.Reader.ReadAllAsync(ct))
                {
                    try
                    {
                        var mem = packet.Buffer.AsMemory(0, packet.Length);
                        int sent = 0;
                        while (sent < mem.Length)
                        {
                            int n = await _tcpToServer.SendAsync(mem.Slice(sent), SocketFlags.None, ct);
                            if (n == 0) return;
                            sent += n;
                        }
                    }
                    catch (SocketException ex)
                    {
                        _logger.Warn($"TCP send failed: {ex.Message}");
                        return;
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(packet.Buffer);
                    }
                }
            }
            catch (OperationCanceledException) { }
        }

        private async Task ClientUdpReceiveLoop(CancellationToken ct)
        {
            var buffer = ArrayPool<byte>.Shared.Rent(MaxPayloadSize);
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    SocketReceiveFromResult result;
                    EndPoint remoteEp = new IPEndPoint(IPAddress.IPv6Any, 0);
                    try
                    {
                        result = await _udp.ReceiveFromAsync(
                            new ArraySegment<byte>(buffer),
                            SocketFlags.None,
                            remoteEp);
                    }
                    catch (SocketException ex)
                    {
                        _logger.Warn($"UDP receive: {ex.Message}");
                        continue;
                    }
                    catch (ObjectDisposedException) { break; }

                    if (result.ReceivedBytes < HeaderSize) continue;

                    OnDataReceived?.Invoke(
                        0,
                        buffer.AsSpan(HeaderSize, result.ReceivedBytes - HeaderSize),
                        NetProtocolType.Udp);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { _logger.Error("Client UDP receive loop crashed", ex); }
            finally { ArrayPool<byte>.Shared.Return(buffer); }
        }

        private async Task ClientUdpSendLoop(CancellationToken ct)
        {
            try
            {
                await foreach (var packet in _udpOut.Reader.ReadAllAsync(ct))
                {
                    try
                    {
                        // SendToAsync on netstandard2.1 requires ArraySegment.
                        await _udp.SendToAsync(
                            new ArraySegment<byte>(packet.Buffer, 0, packet.Length),
                            SocketFlags.None,
                            _serverEndPoint);
                    }
                    catch (SocketException ex)
                    {
                        _logger.Warn($"UDP send failed: {ex.Message}");
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(packet.Buffer);
                    }
                }
            }
            catch (OperationCanceledException) { }
        }

        // ---------- Helpers ----------

        private void DisconnectClient(ClientConnection conn)
        {
            if (!_clients.TryRemove(conn.ClientId, out _)) return;

            if (conn.UdpEndPoint != null)
                _endPointToClients.TryRemove(conn.UdpEndPoint, out _);

            conn.Cts?.Cancel();
            try { conn.Tcp?.Close(); } catch { }

            OnClientDisconnected?.Invoke(conn.ClientId);
        }

        private static async Task<bool> ReceiveExactAsync(Socket socket, Memory<byte> buffer, CancellationToken ct)
        {
            int received = 0;
            while (received < buffer.Length)
            {
                ct.ThrowIfCancellationRequested();
                int n = await socket.ReceiveAsync(buffer.Slice(received), SocketFlags.None, ct);
                if (n == 0) return false;
                received += n;
            }
            return true;
        }

        private static int FrameForTcp(ReadOnlySpan<byte> payload, Span<byte> destination)
        {
            BinaryPrimitives.WriteInt32LittleEndian(destination, payload.Length);
            payload.CopyTo(destination[HeaderSize..]);
            return HeaderSize + payload.Length;
        }

        private static int FrameForUdp(int clientId, ReadOnlySpan<byte> payload, Span<byte> destination)
        {
            BinaryPrimitives.WriteInt32LittleEndian(destination, clientId);
            payload.CopyTo(destination[HeaderSize..]);
            return HeaderSize + payload.Length;
        }

        private static OutgoingPacket BuildPacket(int clientIdForUdp, ReadOnlySpan<byte> payload, NetProtocolType protocol)
        {
            int totalSize = HeaderSize + payload.Length;
            var buffer = ArrayPool<byte>.Shared.Rent(totalSize);
            int length = protocol == NetProtocolType.Tcp
                ? FrameForTcp(payload, buffer)
                : FrameForUdp(clientIdForUdp, payload, buffer);
            return new OutgoingPacket(buffer, length);
        }

        private static Channel<OutgoingPacket> CreateTcpChannel() =>
            Channel.CreateBounded<OutgoingPacket>(new BoundedChannelOptions(1024)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = false,
            });

        private static Channel<OutgoingPacket> CreateUdpChannel() =>
            Channel.CreateBounded<OutgoingPacket>(new BoundedChannelOptions(1024)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = false,
            });

        // ---------- Nested types ----------

        private sealed class ClientConnection
        {
            public int ClientId;
            public Socket Tcp;
            public IPEndPoint UdpEndPoint;
            public Channel<OutgoingPacket> TcpOut;
            public Channel<OutgoingPacket> UdpOut;
            public CancellationTokenSource Cts;
            public Task TcpReceiveTask, TcpSendTask, UdpSendTask;

            // Completed when first UDP packet from client arrives and UdpEndPoint is set.
            // ServerUdpSendLoop awaits this before draining the UDP send channel — until then,
            // outgoing UDP piles up in the bounded channel (DropOldest) rather than being dropped
            // in the loop.
            public readonly TaskCompletionSource<bool> UdpReady =
                new(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        private readonly struct OutgoingPacket
        {
            public readonly byte[] Buffer;
            public readonly int Length;

            public OutgoingPacket(byte[] buffer, int length)
            {
                Buffer = buffer;
                Length = length;
            }
        }
    }
}
