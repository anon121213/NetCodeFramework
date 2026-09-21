using System;
using System.Diagnostics;
using Skynet.Data.Attributes;
using Skynet.NetworkComponents.RpcComponents;
using Skynet.RpcSystem;
using Skynet.RpcSystem.ProcessorsData;
using Skynet.Runner;

namespace Skynet.Tick
{
    public sealed partial class ClockSyncService : NetworkService, IServerClockSync
    {
        private readonly INetworkRunner _networkRunner;
        private readonly INetworkTickScheduler _tickScheduler;
        
        private long _tickOffset;
        private bool _isSynced;
        private long _lastPingSentStopwatchTicks;
        private uint _pingCounter;
        private uint _lastPingTick;
        
        public bool IsSynced => _networkRunner.IsServer || _isSynced;
        public long TickOffset => _networkRunner.IsServer ? 0 : _tickOffset;
        public float CurrentServerTick =>
            (_tickScheduler.CurrentTick + TickOffset) +
            _tickScheduler.TimeSinceLastTick / _tickScheduler.TickInterval;

        public ClockSyncService(INetworkRunner networkRunner, INetworkTickScheduler tickScheduler, 
            IRpcHandlerRegistry registry, IRpcSender sender)
        {
            _networkRunner = networkRunner;
            _tickScheduler = tickScheduler;
            
            InitializeRpc(registry, sender);
        }
        
        protected override void OnNetworkReady() {
            if (!_networkRunner.IsServer)
                _tickScheduler.OnTick += TryPing;
        }
        
        private void TryPing(uint tick) {
            if (tick - _lastPingTick < 30) return;
            _lastPingTick = tick;
            _lastPingSentStopwatchTicks = Stopwatch.GetTimestamp();
            Ping(_lastPingSentStopwatchTicks);
        }   
        
        [ServerRpc(NetProtocolType.Udp, HandlerExecution.Immediate)]
        private void HandlePing(int senderId, long clientStopwatchTicks) {
            Pong(senderId, clientStopwatchTicks, _tickScheduler.CurrentTick);
        }

        [ClientRpc(NetProtocolType.Udp, RpcTarget.Client, HandlerExecution.Immediate)]
        private void HandlePong(long clientStopwatchTicks, uint serverTickAtReceive) {
            long nowStopwatchTicks = Stopwatch.GetTimestamp();
            long rttStopwatchTicks = nowStopwatchTicks - clientStopwatchTicks;
            double rttSeconds = (double)rttStopwatchTicks / Stopwatch.Frequency;
            double oneWaySeconds = rttSeconds * 0.5;
    
            double serverTickNow = serverTickAtReceive + oneWaySeconds / _tickScheduler.TickInterval;
            long newOffset = (long)Math.Round(serverTickNow - _tickScheduler.CurrentTick);
    
            _tickOffset = _isSynced ? (long)(_tickOffset * 0.9 + newOffset * 0.1) : newOffset;
            _isSynced = true;
        }
    }
}