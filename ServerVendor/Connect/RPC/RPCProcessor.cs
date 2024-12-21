using System.Net.Sockets;
using System.Reflection;
using System.Text.Json;
using ServerVendor.Connect.Data;
using ServerVendor.Connect.RPCHelper.Attributes;

namespace ServerVendor.Connect.RPC
{
    public static class RpcProcessor
    {
        public static void HandleRpcCall(Socket socket, object handler, byte[] data)
        {
            try
            {
                Console.WriteLine($"[Server] Получены данные ({data.Length} байт): {BitConverter.ToString(data)}");

                // Десериализация сообщения
                var rpcMessage = JsonSerializer.Deserialize<RpcMessage>(data);
                if (rpcMessage == null)
                {
                    Console.WriteLine("[Server] Не удалось десериализовать сообщение.");
                    return;
                }

                Console.WriteLine($"[Server] Десериализовано сообщение: {JsonSerializer.Serialize(rpcMessage)}");

                // Поиск метода по имени
                var method = handler.GetType()
                    .GetMethods()
                    .FirstOrDefault(m => m.Name == rpcMessage.MethodName);

                if (method == null)
                {
                    Console.WriteLine($"[Server] Метод {rpcMessage.MethodName} не найден.");
                    return;
                }

                // Проверка, разрешен ли метод
                var isMethodAllowed = method.GetCustomAttribute<RPCAttributes.ServerRPC>() != null;

                if (isMethodAllowed)
                {
                    Console.WriteLine($"[Server] Метод {rpcMessage.MethodName} найден и разрешён для выполнения.");
                    // Десериализация параметров
                    var parameters = rpcMessage.Parameters
                        .Select((p, i) => JsonSerializer.Deserialize(p.ToString(), method.GetParameters()[i].ParameterType))
                        .ToArray();
                    
                    // Вызов метода с параметрами
                    method.Invoke(handler, parameters);
                }
                else
                {
                    Console.WriteLine($"[Server] Метод {rpcMessage.MethodName} не разрешён для выполнения.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Server] Ошибка обработки RPC вызова: {ex.Message}");
            }
        }
    }
}
