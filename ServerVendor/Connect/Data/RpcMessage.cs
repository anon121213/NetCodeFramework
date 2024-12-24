using System.Text.Json;

namespace ServerVendor.Connect.Data
{
    [Serializable]
    public class RpcMessage
    {
        public string MethodName { get; set; }
        public JsonElement[] Parameters { get; set; }
        public string ClassType { get; set; }
    }
}