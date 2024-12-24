using System.Net.Sockets;
using System.Reflection;
using System.Text.Json;
using ServerVendor.Connect.Data;
using ServerVendor.Connect.RPC.Attributes;

namespace ServerVendor.Connect.Proxy;

public static class RpcProxy
{
    private static readonly Socket _serverSocket = Program.ServerSocket;

    // Регистратор экземпляров RPC
    private static Dictionary<Type, IRPCCaller> _callers = new();

    // Регистрируем экземпляр
    public static void RegisterRPCInstance<T>(IRPCCaller caller) where T : IRPCCaller =>
        _callers[typeof(T)] = caller;

    // Попытка вызова RPC с передачей параметров
    public static bool TryInvokeRPC<TObject>(MethodInfo methodInfo, params object[] parameters) where TObject : class
    {
        // Проверяем, есть ли у метода атрибуты RPC
        if (methodInfo.GetCustomAttribute<RPCAttributes.ClientRPC>() == null &&
            methodInfo.GetCustomAttribute<RPCAttributes.ServerRPC>() == null)
        {
            Console.WriteLine($"Method: {methodInfo.Name} must have RPC attributes.");
            return false;
        }

        // Проверка на регистрацию
        if (!_callers.ContainsKey(typeof(TObject)))
        {
            Console.WriteLine($"{typeof(TObject)} must be registered.");
            return false;
        }

        // Сериализуем параметры
        var serializedParameters = parameters.Select(param => JsonSerializer.SerializeToElement(param)).ToArray();

        // Формируем сообщение RPC
        var message = new RpcMessage
        {
            MethodName = methodInfo.Name,
            Parameters = serializedParameters,
            ClassType = typeof(TObject).ToString()
        };

        // Сериализуем сообщение в байты
        byte[] data = SerializeMessage(message);

        try
        {
            // Отправляем сообщение в зависимости от типа метода
            if (methodInfo.GetCustomAttribute<RPCAttributes.ClientRPC>() != null)
            {
                foreach (var socket in Program.ClientsSockets)
                {
                    socket.Send(data);
                }
            }
            else if (methodInfo.GetCustomAttribute<RPCAttributes.ServerRPC>() != null)
            {
                _serverSocket.Send(data);
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error: {e.Message}");
            return false;
        }

        return true;
    }

    // Сериализация RPC-сообщения в байты
    private static byte[] SerializeMessage(RpcMessage message) =>
        JsonSerializer.SerializeToUtf8Bytes(message);

    // Метод для прослушивания RPC-запросов
    public static async Task ListenForRpcCalls(Socket socket)
    {
        byte[] buffer = new byte[1024 * 64];

        while (true)
        {
            Console.WriteLine("Waiting for RPC calls...");

            int bytesRead = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), SocketFlags.None);

            if (bytesRead <= 0)
                continue;

            // Десериализуем сообщение
            RpcMessage? message = DeserializeMessage(buffer.Take(bytesRead).ToArray());

            if (message != null)
            {
                // Обрабатываем RPC-сообщение
                ProcessRpcMessage(Type.GetType(message.ClassType), message);
            }
        }
    }

    // Десериализация сообщения
    private static RpcMessage? DeserializeMessage(byte[] data) =>
        JsonSerializer.Deserialize<RpcMessage>(data);

    // Обработка RPC-сообщений
    private static void ProcessRpcMessage(Type? callerType, RpcMessage message)
    {
        if (callerType == null) return;

        var method = GetRpcMethod(callerType, message);

        if (method == null) return;

        var parameters = ConvertParameters(message.Parameters, method.GetParameters());

        // Получаем зарегистрированный экземпляр для вызова метода
        if (!_callers.TryGetValue(callerType, out IRPCCaller rpcCaller)) return;

        // Вызываем метод
        method.Invoke(rpcCaller, parameters);
    }

    // Получаем метод из типа по имени метода
    private static MethodInfo? GetRpcMethod(Type callerType, RpcMessage message) =>
        callerType.GetMethods().FirstOrDefault(m => m.Name == message.MethodName);

    // Преобразуем параметры JSON в объекты
    private static object?[] ConvertParameters(JsonElement[] jsonElements, ParameterInfo[] parameterInfos)
    {
        var parameters = new object?[jsonElements.Length];
        for (int i = 0; i < jsonElements.Length; i++)
        {
            parameters[i] = JsonSerializer.Deserialize(jsonElements[i].GetRawText(), parameterInfos[i].ParameterType);
        }

        return parameters;
    }
}

public interface IRPCCaller
{
}