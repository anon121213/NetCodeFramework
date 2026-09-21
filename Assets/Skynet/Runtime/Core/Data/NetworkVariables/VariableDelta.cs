using MessagePack;

namespace Skynet.Data.NetworkVariables
{
    [MessagePackObject]
    public readonly struct VariableDelta
    {
        [Key(0)] public int Index { get; }
        [Key(1)] public byte[] Payload { get; }

        public VariableDelta(int index, byte[] payload)
        {
            Index = index;
            Payload = payload;
        }
    }
}