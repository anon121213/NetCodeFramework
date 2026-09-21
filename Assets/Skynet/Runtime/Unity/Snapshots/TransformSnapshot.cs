using System;
using Skynet.Snapshots;
using UnityEngine;

namespace Skynet.Unity.Snapshots
{
    public readonly struct TransformSnapshot : ISnapshot
    {
        public uint Tick { get; }
        public TransformSnapshotFlags Flags { get; }
        public Vector3 Position { get; }
        public Quaternion Rotation { get; }
        public Vector3 Scale { get; }

        public TransformSnapshot(uint tick, TransformSnapshotFlags flags, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            Tick = tick;
            Flags = flags;
            Position = position;
            Rotation = rotation;
            Scale = scale;
        }
    }
    
    [Flags]
    public enum TransformSnapshotFlags
    {
        None = 0,
        Position = 1 << 0,
        Rotation = 1 << 1,
        Scale = 1 << 2,
        Teleport = 1 << 3,
    }
}