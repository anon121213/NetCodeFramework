using System;
using MessagePack;

namespace _Scripts.Netcore.NetworkComponents.NetworkVariableComponent.Data
{
    [MessagePackObject]
    [Serializable]
    public struct NetworkVariableMessage
    {
        [Key(0)] public string VariableName;
        [Key(1)] public byte[] SerializedValue;
    }
}