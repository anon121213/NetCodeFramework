using System;
using MessagePack;

namespace _Scripts.Netcore.Data.Message
{
    [MessagePackObject]
    [Serializable]
    public class RpcMessage
    {
        [Key(0)] public string MethodName { get; set; }
        [Key(1)] public byte[][] Parameters { get; set; }
        [Key(2)] public string ClassType { get; set; }
        [Key(3)] public byte[] MethodParam { get; set; }
        [Key(4)] public int InstanceId { get; set; }
        [Key(5)] public CallerTypes CallerType { get; set; }
    }

    public enum CallerTypes
    {
        Service = 0,
        Behaviour = 1
    }
}