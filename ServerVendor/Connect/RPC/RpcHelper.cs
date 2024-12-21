using System.Net.Sockets;
using System.Reflection;
using System.Text.Json;
using ServerVendor.Connect.Data;
using ServerVendor.Connect.RPCHelper.Attributes;

namespace ServerVendor.Connect.RPCHelper;

public static class RpcHelper
{
    public static void ListenForRpcCalls(Socket socket, RpcHandler handler)
    {
        byte[] buffer = new byte[1024];
        while (true)
        {
            int bytesRead = socket.Receive(buffer);
            if (bytesRead > 0)
            {
                RpcMessage message = DeserializeMessage(buffer.Take(bytesRead).ToArray());
                ProcessRpcMessage(message, handler);
            }
            else
            {
                break;
            }
        }
    }

    private static RpcMessage DeserializeMessage(byte[] data)
    {
        return JsonSerializer.Deserialize<RpcMessage>(data);
    }

    private static void ProcessRpcMessage(RpcMessage message, object handler)
    {
        var method = GetRpcMethod(handler, message.MethodName);
        if (method != null)
        {
            var parameters = ConvertParameters(message.Parameters, method.GetParameters());
            method.Invoke(handler, parameters);
        }
        else
        {
            Console.WriteLine("Method not found");
        }
    }

    private static MethodInfo GetRpcMethod(object handler, string methodName)
    {
        var methods = handler.GetType().GetMethods();
        foreach (var method in methods)
        {
            if (method.Name == methodName &&
                (method.GetCustomAttribute<RPCAttributes.ServerRPC>() != null || method.GetCustomAttribute<RPCAttributes.ClientRPC>() != null))
            {
                return method;
            }
        }
        return null;
    }

    private static object[] ConvertParameters(JsonElement[] jsonElements, ParameterInfo[] parameterInfos)
    {
        var parameters = new object[jsonElements.Length];
        for (int i = 0; i < jsonElements.Length; i++)
        {
            parameters[i] = JsonSerializer.Deserialize(jsonElements[i].GetRawText(), parameterInfos[i].ParameterType);
        }
        return parameters;
    }
}
