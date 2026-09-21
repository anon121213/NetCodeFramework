using System;

namespace Skynet.Tick
{
    /// <summary>
    /// Fires a fixed-rate tick on the main thread, decoupled from Unity frame rate. Subscribers
    /// (NetworkTransform, NetworkRigidbody, etc.) detect delta and send snapshots on each tick,
    /// so recipients see a steady, deterministic packet stream regardless of the sender's fps.
    /// </summary>
    public interface INetworkTickScheduler
    {
        /// <summary>Raised on every network tick. Argument is <see cref="CurrentTick"/>.</summary>
        event Action<uint> OnTick;

        /// <summary>Monotonic tick counter. 0 until the first tick fires.</summary>
        uint CurrentTick { get; }

        /// <summary>Configured tick rate in Hz.</summary>
        int TickRate { get; }

        /// <summary>Seconds between two ticks (<c>1f / TickRate</c>).</summary>
        float TickInterval { get; }
        
        float TimeSinceLastTick { get; }
    }
}
