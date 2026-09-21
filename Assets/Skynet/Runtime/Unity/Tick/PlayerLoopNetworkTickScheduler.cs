using System;
using Skynet.Tick;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.PlayerLoop;

namespace Skynet.Unity.Tick
{
    /// <summary>
    /// Ticks by hooking Unity's PlayerLoop instead of relying on a scene MonoBehaviour. Registered
    /// as a DI singleton and installed on construction, removed on <see cref="Dispose"/>.
    /// The pulse runs at the start of the <c>Update</c> phase, so every tick lands on the main
    /// thread before user <c>Update</c>/<c>LateUpdate</c> code executes.
    /// </summary>
    public sealed class PlayerLoopNetworkTickScheduler : INetworkTickScheduler, IDisposable
    {
        private uint _currentTick;
        private bool _installed;

        public event Action<uint> OnTick;
        public uint CurrentTick => _currentTick;
        public int TickRate { get; }
        public float TickInterval { get; }
        public float TimeSinceLastTick { get; private set; }

        public PlayerLoopNetworkTickScheduler(NetworkTickSettings settings)
        {
            TickRate = settings.TickRate;
            TickInterval = 1f / settings.TickRate;

            Install();
        }

        public void Dispose() => Uninstall();

        private void Pulse()
        {
            TimeSinceLastTick += Time.unscaledDeltaTime;

            if (TimeSinceLastTick > TickInterval * 8f)
                TimeSinceLastTick = TickInterval;

            while (TimeSinceLastTick >= TickInterval)
            {
                TimeSinceLastTick -= TickInterval;
                _currentTick++;
                OnTick?.Invoke(_currentTick);
            }
        }

        private void Install()
        {
            if (_installed) return;

            var loop = PlayerLoop.GetCurrentPlayerLoop();
            for (int i = 0; i < loop.subSystemList.Length; i++)
            {
                if (loop.subSystemList[i].type != typeof(Update)) continue;

                var children = loop.subSystemList[i].subSystemList;
                var extended = new PlayerLoopSystem[children.Length + 1];
                extended[0] = new PlayerLoopSystem
                {
                    type = typeof(PlayerLoopNetworkTickScheduler),
                    updateDelegate = Pulse,
                };
                Array.Copy(children, 0, extended, 1, children.Length);
                loop.subSystemList[i].subSystemList = extended;
                break;
            }
            PlayerLoop.SetPlayerLoop(loop);
            _installed = true;
        }

        private void Uninstall()
        {
            if (!_installed) return;

            var loop = PlayerLoop.GetCurrentPlayerLoop();
            for (int i = 0; i < loop.subSystemList.Length; i++)
            {
                if (loop.subSystemList[i].type != typeof(Update)) continue;

                var children = loop.subSystemList[i].subSystemList;
                int keep = 0;
                for (int j = 0; j < children.Length; j++)
                    if (children[j].type != typeof(PlayerLoopNetworkTickScheduler)) keep++;

                if (keep == children.Length) break;

                var filtered = new PlayerLoopSystem[keep];
                int w = 0;
                for (int j = 0; j < children.Length; j++)
                    if (children[j].type != typeof(PlayerLoopNetworkTickScheduler))
                        filtered[w++] = children[j];

                loop.subSystemList[i].subSystemList = filtered;
                break;
            }
            PlayerLoop.SetPlayerLoop(loop);
            _installed = false;
        }
    }
}
