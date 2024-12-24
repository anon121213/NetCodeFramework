using MessagePack;
using System.Net.Sockets;
using System.Reflection;
using ServerVendor.Connect.Data;
using ServerVendor.Connect.RPC.Attributes;

namespace ServerVendor.Connect.Proxy
{
    public static class RpcProxy
    {
        private static readonly Socket _serverSocket = Program.ServerSocket;

        private static Dictionary<Type, IRPCCaller> _callers = new();

        public static void RegisterRPCInstance<T>(IRPCCaller caller) where T : IRPCCaller =>
            _callers[typeof(T)] = caller;

        public static bool TryInvokeRPC<TObject>(MethodInfo methodInfo, params object[] parameters) where TObject : class
        {
            if (methodInfo.GetCustomAttribute<RPCAttributes.ClientRPC>() == null &&
                methodInfo.GetCustomAttribute<RPCAttributes.ServerRPC>() == null)
            {
                Console.WriteLine($"Method: {methodInfo.Name} must have RPC attributes.");
                return false;
            }

            if (!_callers.ContainsKey(typeof(TObject)))
            {
                Console.WriteLine($"{typeof(TObject)} must be registered.");
                return false;
            }

            var serializedParameters = parameters.Select(param => 
                MessagePackSerializer.Serialize(param)).ToArray();

            var message = new RpcMessage
            {
                MethodName = methodInfo.Name,
                Parameters = serializedParameters,
                ClassType = typeof(TObject).ToString()
            };

            byte[] data = SerializeMessage(message);

            try
            {
                if (methodInfo.GetCustomAttribute<RPCAttributes.ClientRPC>() != null)
                    foreach (var socket in Program.ClientsSockets)
                        socket.Send(data);
                
                else if (methodInfo.GetCustomAttribute<RPCAttributes.ServerRPC>() != null) 
                    _serverSocket.Send(data);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error: {e.Message}");
                return false;
            }

            return true;
        }

        private static byte[] SerializeMessage(RpcMessage message) => 
            MessagePackSerializer.Serialize(message);

        public static async Task ListenForRpcCalls(Socket socket)
        {
            byte[] buffer = new byte[1024 * 64];

            while (true)
            {
                Console.WriteLine("Waiting for RPC calls...");

                int bytesRead = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), SocketFlags.None);

                if (bytesRead <= 0)
                    continue;

                RpcMessage? message = DeserializeMessage(buffer.Take(bytesRead).ToArray());

                if (message != null) 
                    ProcessRpcMessage(Type.GetType(message.ClassType), message);
            }
        }

        private static RpcMessage? DeserializeMessage(byte[] data)
        {
            try
            {
                return MessagePackSerializer.Deserialize<RpcMessage>(data);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Deserialization failed: {ex.Message}");
                return null;
            }
        }

        private static void ProcessRpcMessage(Type? callerType, RpcMessage message)
        {
            if (callerType == null) return;

            var method = GetRpcMethod(callerType, message);

            if (method == null) return;

            var parameters = ConvertParameters(message.Parameters, method.GetParameters());

            if (!_callers.TryGetValue(callerType, out IRPCCaller rpcCaller)) return;

            method.Invoke(rpcCaller, parameters);
        }

        private static MethodInfo? GetRpcMethod(Type callerType, RpcMessage message) =>
            callerType.GetMethods().FirstOrDefault(m => m.Name == message.MethodName);

        private static object?[] ConvertParameters(byte[][] serializedParameters, ParameterInfo[] parameterInfos)
        {
            var parameters = new object?[serializedParameters.Length];
            for (int i = 0; i < serializedParameters.Length; i++)
                parameters[i] = MessagePackSerializer.Deserialize
                    (parameterInfos[i].ParameterType, serializedParameters[i]);

            return parameters;
        }
    }

    public interface IRPCCaller
    {
    }
}
