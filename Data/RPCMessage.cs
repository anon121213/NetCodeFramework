using System;
using MessagePack;

namespace CodeBase.Network.Data
{
    [MessagePackObject]
    [Serializable]
    public class RpcMessage
    {
        [Key(0)] public string MethodName { get; set; }
        [Key(1)] public byte[][] Parameters { get; set; }
        [Key(2)] public string ClassType { get; set; }
        [Key(3)] public byte[] MethodParam { get; set; }
    }
}