using System;
using System.Diagnostics;
using Skynet.Data.Attributes;
using Skynet.NetworkComponents;
using Skynet.RpcSystem;
using Skynet.RpcSystem.ProcessorsData;
using Skynet.Runner;

namespace Skynet.Tick
{
    internal sealed partial class ClockSyncService : NetworkService, IServerClockSync
    {
        private long _tickOffset;
        private bool _isSynced;
        private long _lastPingSentStopwatchTicks;
        private uint _pingCounter;
        private uint _lastPingTick;

        public bool IsSynced => Runner.IsServer || _isSynced;
        public long TickOffset => Runner.IsServer ? 0 : _tickOffset;
        public float CurrentServerTick =>
            (Runner.TickScheduler.CurrentTick + TickOffset) +
            Runner.TickScheduler.TimeSinceLastTick / Runner.TickScheduler.TickInterval;

        public ClockSyncService(NetworkRunner runner) : base(runner)
        {
        }

        protected override void OnNetworkReady()
        {
            if (!Runner.IsServer)
                Runner.TickScheduler.OnTick += TryPing;
        }

        private void TryPing(uint tick)
        {
            if (tick - _lastPingTick < 30) return;
            _lastPingTick = tick;
            _lastPingSentStopwatchTicks = Stopwatch.GetTimestamp();
            Ping(_lastPingSentStopwatchTicks);
        }

        [ServerRpc(NetProtocolType.Udp, HandlerExecution.Immediate)]
        private void HandlePing(int senderId, long clientStopwatchTicks)
        {
            Pong(senderId, clientStopwatchTicks, Runner.TickScheduler.CurrentTick);
        }

        [ClientRpc(NetProtocolType.Udp, RpcTarget.Client, HandlerExecution.Immediate)]
        private void HandlePong(long clientStopwatchTicks, uint serverTickAtReceive)
        {
            long nowStopwatchTicks = Stopwatch.GetTimestamp();
            long rttStopwatchTicks = nowStopwatchTicks - clientStopwatchTicks;
            double rttSeconds = (double)rttStopwatchTicks / Stopwatch.Frequency;
            double oneWaySeconds = rttSeconds * 0.5;

            double serverTickNow = serverTickAtReceive + oneWaySeconds / Runner.TickScheduler.TickInterval;
            long newOffset = (long)Math.Round(serverTickNow - Runner.TickScheduler.CurrentTick);

            _tickOffset = _isSynced ? (long)(_tickOffset * 0.9 + newOffset * 0.1) : newOffset;
            _isSynced = true;
        }
    }
}
