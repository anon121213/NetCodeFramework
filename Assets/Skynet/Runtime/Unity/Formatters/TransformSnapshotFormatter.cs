using MessagePack;
using MessagePack.Formatters;
using Skynet.Unity.Snapshots;
using UnityEngine;

namespace Skynet.Unity.Formatters
{
    public sealed class TransformSnapshotFormatter : IMessagePackFormatter<TransformSnapshot>
    {
        public void Serialize(ref MessagePackWriter writer, TransformSnapshot value, MessagePackSerializerOptions options)
        {
            writer.Write((byte)value.Flags);
            writer.Write(value.Tick);
            
            if ((value.Flags & TransformSnapshotFlags.Position) != 0) MessagePackSerializer.Serialize(ref writer, value.Position, options);
            if ((value.Flags & TransformSnapshotFlags.Rotation) != 0) MessagePackSerializer.Serialize(ref writer, value.Rotation, options);
            if ((value.Flags & TransformSnapshotFlags.Scale) != 0) MessagePackSerializer.Serialize(ref writer, value.Scale, options);
        }

        public TransformSnapshot Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            var flags = (TransformSnapshotFlags)reader.ReadByte();
            var tick = reader.ReadUInt32();
            
            Vector3 pos = default;
            Quaternion rot = default;
            Vector3 scale = default;
            if ((flags & TransformSnapshotFlags.Position) != 0) pos = MessagePackSerializer.Deserialize<Vector3>(ref reader, options);
            if ((flags & TransformSnapshotFlags.Rotation) != 0) rot = MessagePackSerializer.Deserialize<Quaternion>(ref reader, options);
            if ((flags & TransformSnapshotFlags.Scale) != 0) scale = MessagePackSerializer.Deserialize<Vector3>(ref reader, options);
            
            return new TransformSnapshot(tick, flags, pos, rot, scale);
        }
    }
}