using System;
using MessagePack;
using MessagePack.Formatters;
using UnityEngine;

namespace _Scripts.Netcore.FormatterSystem.Formatters
{
    public class QuaternionFormatter : IMessagePackFormatter<Quaternion>
    {
        public Quaternion Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            var count = reader.ReadArrayHeader();
            
            if (count != 4)
                throw new InvalidOperationException("Invalid Quaternion format.");

            float x = reader.ReadSingle();
            float y = reader.ReadSingle();
            float z = reader.ReadSingle();
            float w = reader.ReadSingle();

            return new Quaternion(x, y, z, w);
        }

        public void Serialize(ref MessagePackWriter writer, Quaternion value, MessagePackSerializerOptions options)
        {
            writer.WriteArrayHeader(4);
            writer.Write(value.x);
            writer.Write(value.y);
            writer.Write(value.z);
            writer.Write(value.w);
        }
    }
}