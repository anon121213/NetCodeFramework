using System.Net.Sockets;
using System.Reflection;
using System.Text.Json;
using ServerVendor.Connect.Data;
using ServerVendor.Connect.RPC.Attributes;

public static class RpcHelper
{
    public static async Task ListenForRpcCalls(Socket socket, RpcHandler handler)
    {
        byte[] buffer = new byte[1024];
        
        while (true)
        {
            Console.WriteLine("Waiting for RPC calls...");
        
            int bytesRead = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), SocketFlags.None);

            if (bytesRead <= 0)
                continue;
            
            RpcMessage? message = DeserializeMessage(buffer.Take(bytesRead).ToArray());
            ProcessRpcMessage(message, handler);
        }
    }

    private static RpcMessage? DeserializeMessage(byte[] data)
    {
        return JsonSerializer.Deserialize<RpcMessage>(data);
    }

    private static void ProcessRpcMessage(RpcMessage? message, object handler)
    {
        var method = GetRpcMethod(handler, message.MethodName);
        if (method != null)
        {
            var parameters = ConvertParameters(message.Parameters, method.GetParameters());
            method.Invoke(handler, parameters);
        }
    }

    private static MethodInfo? GetRpcMethod(object handler, string methodName)
    {
        var methods = handler.GetType().GetMethods();
        return methods.FirstOrDefault(method =>
            method.Name == methodName
            && (method.GetCustomAttribute<RPCAttributes.ServerRPC>() != null 
                || method.GetCustomAttribute<RPCAttributes.ClientRPC>() != null));
    }

    private static object?[] ConvertParameters(JsonElement[] jsonElements, ParameterInfo[] parameterInfos)
    {
        var parameters = new object?[jsonElements.Length];
        for (int i = 0; i < jsonElements.Length; i++) 
            parameters[i] = JsonSerializer.Deserialize(jsonElements[i].GetRawText(), parameterInfos[i].ParameterType);
        return parameters;
    }
}