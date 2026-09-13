using MessagePack;

namespace Skynet.Data.Message
{
    [MessagePackObject]
    public class RpcMessage
    {
        [Key(0)] public int CallerTypeId { get; set; }
        [Key(1)] public int InstanceId { get; set; }
        [Key(2)] public int MethodId { get; set; }
        [Key(3)] public byte[] Payload { get; set; }
        [Key(4)] public int SenderId { get; set; }
    }
}