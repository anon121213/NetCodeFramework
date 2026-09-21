using System;
using System.Collections.Generic;
using Skynet.NetworkComponents;
using Skynet.Tick;

namespace Skynet.Spawner
{
    public class NetworkObjectsContainer : INetworkObjectContainer, IDisposable
    {
        private readonly List<NetworkObject> _objects = new(64);
        private readonly Dictionary<int, int> _idToIndex = new(64);
        private readonly List<NetworkObject> _pendingRemove = new();

        //TODO Make iteration depth
        private bool _iterating;

        private readonly INetworkTickScheduler _scheduler;

        public IReadOnlyList<NetworkObject> NetworkObjects => _objects;

        public NetworkObjectsContainer(INetworkTickScheduler scheduler)
        {
            _scheduler = scheduler;
            _scheduler.OnTick += Tick;
        }

        private void Tick(uint tick)
        {
            _iterating = true;
            try
            {
                for (int i = 0; i < _objects.Count; i++)
                    _objects[i].OnNetworkUpdate(tick);
            }
            finally
            {
                _iterating = false;
            }

            if (_pendingRemove.Count > 0)
            {
                for (int i = 0; i < _pendingRemove.Count; i++)
                    RemoveImmediate(_pendingRemove[i]);
                _pendingRemove.Clear();
            }
        }

        public bool AddNetworkObject(NetworkObject obj)
        {
            if (_idToIndex.ContainsKey(obj.NetworkObjectId)) return false;
            _idToIndex[obj.NetworkObjectId] = _objects.Count;
            _objects.Add(obj);
            return true;
        }

        public bool RemoveNetworkObject(NetworkObject obj)
        {
            if (!_idToIndex.ContainsKey(obj.NetworkObjectId)) return false;

            if (_iterating)
            {
                _pendingRemove.Add(obj);
                return true;
            }

            return RemoveImmediate(obj);
        }

        public bool TryGetNetworkObject(int id, out NetworkObject obj)
        {
            if (_idToIndex.TryGetValue(id, out int idx)) { obj = _objects[idx]; return true; }
            obj = null;
            return false;
        }

        private bool RemoveImmediate(NetworkObject obj)
        {
            if (!_idToIndex.TryGetValue(obj.NetworkObjectId, out int idx))
                return false;

            int lastIdx = _objects.Count - 1;
            if (idx != lastIdx)
            {
                var moved = _objects[lastIdx];
                _objects[idx] = moved;
                _idToIndex[moved.NetworkObjectId] = idx;
            }
            _objects.RemoveAt(lastIdx);
            _idToIndex.Remove(obj.NetworkObjectId);
            return true;
        }

        public void Dispose()
        {
            _scheduler.OnTick -= Tick;
        }
    }
}
