using System;

namespace Skynet.Tick
{
    public sealed class NetworkTickSettings
    {
        public int TickRate { get; }

        public NetworkTickSettings(int tickRate = 30)
        {
            if (tickRate <= 0 || tickRate > 240)
                throw new ArgumentOutOfRangeException(nameof(tickRate),
                    "Network tick rate must be in the (0, 240] Hz range.");

            TickRate = tickRate;
        }
    }
}
