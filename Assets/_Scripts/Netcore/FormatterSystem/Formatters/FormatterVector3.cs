using System;
using MessagePack;
using MessagePack.Formatters;
using UnityEngine;

namespace _Scripts.Netcore.FormatterSystem.Formatters
{
    public class Vector3Formatter : IMessagePackFormatter<Vector3>
    {
        public Vector3 Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            var count = reader.ReadArrayHeader();
            if (count != 3)
                throw new InvalidOperationException("Invalid Vector3 format.");

            float x = reader.ReadSingle();
            float y = reader.ReadSingle();
            float z = reader.ReadSingle();

            return new Vector3(x, y, z);
        }

        public void Serialize(ref MessagePackWriter writer, Vector3 value, MessagePackSerializerOptions options)
        {
            writer.WriteArrayHeader(3);
            writer.Write(value.x);
            writer.Write(value.y);
            writer.Write(value.z);
        }
    }
}