using System.Dynamic;
using System.Net.Sockets;
using System.Reflection;
using System.Text.Json;
using ServerVendor;
using ServerVendor.Connect.Data;
using ServerVendor.Connect.RPCHelper.Attributes;

public class RpcProxy : DynamicObject
{
    private readonly Socket _socket;

    public RpcProxy(Socket socket)
    {
        _socket = socket;
    }

    public override bool TryInvokeMember(InvokeMemberBinder binder, object[] args, out object result)
    {
        var method = GetRpcMethod(binder.Name);
        if (method != null)
        {
            var parameters = new JsonElement[args.Length];
            for (int i = 0; i < args.Length; i++)
            {
                parameters[i] = JsonSerializer.SerializeToElement(args[i]);
            }

            var message = new RpcMessage
            {
                MethodName = binder.Name,
                Parameters = parameters
            };

            byte[] data = SerializeMessage(message);
            
            try
            {
                _socket.Send(data);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }

            result = null;
            return true;
        }

        result = null;
        return false;
    }

    private MethodInfo GetRpcMethod(string methodName)
    {
        var types = Assembly.GetExecutingAssembly().GetTypes();
        foreach (var type in types)
        {
            var methods = type.GetMethods();
            foreach (var method in methods)
            {
                if (method.Name == methodName && 
                    (method.GetCustomAttribute<RPCAttributes.ServerRPC>() != null 
                     || method.GetCustomAttribute<RPCAttributes.ClientRPC>() != null))
                {
                    return method;
                }
            }
        }
        return null;
    }

    private byte[] SerializeMessage(RpcMessage message)
    {
        return JsonSerializer.SerializeToUtf8Bytes(message);
    }
}
