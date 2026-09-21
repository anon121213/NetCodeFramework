namespace Skynet.Snapshots
{
    public sealed class SnapshotBuffer<T> where T : ISnapshot
    {
        private readonly T[] _entries;

        private uint _lastTick = uint.MaxValue;
        private int _nextTickIndex;
        private int _count;
        
        public SnapshotBuffer(int capacity) => 
            _entries = new T[capacity];

        public void Insert(T snapshot)
        {
            if (snapshot.Tick <= _lastTick && _lastTick != uint.MaxValue)
                return;

            _entries[_nextTickIndex] = snapshot;
            _nextTickIndex = (_nextTickIndex + 1) % _entries.Length;
            _lastTick = snapshot.Tick;
            if (_count < _entries.Length) _count++;
        }

        public bool TryGetStraddling(float renderTick, out T older, out T newer, out float t)
        {
            older = default;
            newer = default;
            t = 0;
            
            int olderTickIndex = -1;
            int newerTickIndex = -1;
            
            float olderTick = 0;
            float newerTick = float.MaxValue;
            
            for (int i = 0; i < _count; i++)
            {
                if (_entries[i].Tick <= renderTick && _entries[i].Tick > olderTick)
                {
                    olderTickIndex = i;
                    olderTick = _entries[i].Tick;
                }
                else if (_entries[i].Tick > renderTick && _entries[i].Tick < newerTick)
                {
                    newerTickIndex = i;
                    newerTick = _entries[i].Tick;
                }
            }

            if (newerTickIndex == -1 || olderTickIndex == -1)
                return false;
            
            older = _entries[olderTickIndex];
            newer = _entries[newerTickIndex];
            t = (renderTick - older.Tick) / (newer.Tick - older.Tick);
            return true;
        }

        public bool TryGetLastTwo(out T newer, out T older)
        {
            newer = default;
            older = default;

            if (_count < 2)
                return false;

            uint newerTick = 0;
            uint olderTick = 0;
            int newerIndex = -1;
            int olderIndex = -1;

            for (int i = 0; i < _count; i++)
            {
                uint tick = _entries[i].Tick;
                if (tick > newerTick)
                {
                    olderTick = newerTick;
                    olderIndex = newerIndex;
                    newerTick = tick;
                    newerIndex = i;
                }
                else if (tick > olderTick)
                {
                    olderTick = tick;
                    olderIndex = i;
                }
            }

            if (newerIndex == -1 || olderIndex == -1)
                return false;

            newer = _entries[newerIndex];
            older = _entries[olderIndex];
            return true;
        }
    }
}