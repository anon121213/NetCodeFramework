using System.Text.Json;

namespace ServerVendor.Connect.Data;

[Serializable]
public struct RpcMessage
{
    public string MethodName { get; set; }
    public JsonElement[] Parameters { get; set; }
}