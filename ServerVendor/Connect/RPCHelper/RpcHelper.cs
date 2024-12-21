using System.Net.Sockets;
using System.Reflection;
using System.Text.Json;
using ServerVendor.Connect.Data;

namespace ServerVendor.Connect.RPCHelper;

public static class RpcHelper
{
    private static readonly List<IOnEventCallback> _callbacks = new();
    
    public static void RegisterCallBack(IOnEventCallback callback) => 
        _callbacks.Add(callback);

    public static void Initialize(Socket socket) => 
        ListenForEvents(socket);

    public static void SendRpcMessage(RpcMessage message, Socket socket)
    {
        byte[] data = SerializeMessage(message);
        socket.Send(data);
    }

    public static byte[] SerializeMessage(RpcMessage message)
    {
        return JsonSerializer.SerializeToUtf8Bytes(message);
    }

    public static RpcMessage DeserializeMessage(byte[] data)
    {
        return JsonSerializer.Deserialize<RpcMessage>(data);
    }
    
    private static void ListenForEvents(Socket socket)
    {
        byte[] buffer = new byte[1024];
        while (true)
        {
            int bytesRead = socket.Receive(buffer);
            if (bytesRead > 0)
            {
                RpcMessage eventMessage = DeserializeMessage(buffer.Take(bytesRead).ToArray());
                Notify(eventMessage);
            }
            else
            {
                break;
            }
        }
    }

    private static void Notify(RpcMessage message)
    {
        foreach (var callback in _callbacks) 
            callback.NotifyListeners(message);
    }
    
    public static void ProcessRpcMessage(RpcMessage message, object handler)
    {
        var method = handler.GetType().GetMethod(message.MethodName);
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

public interface IOnEventCallback
{
    void NotifyListeners(RpcMessage message);   
    void SendEvent(Socket clientSocket);
}