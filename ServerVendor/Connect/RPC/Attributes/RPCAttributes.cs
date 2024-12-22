namespace ServerVendor.Connect.RPC.Attributes;

public class RPCAttributes
{
    [AttributeUsage(AttributeTargets.Method)]
    public class ServerRPC : Attribute { }

    [AttributeUsage(AttributeTargets.Method)]
    public class ClientRPC : Attribute { }
}