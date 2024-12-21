using System.Net.Sockets;
using System.Text.Json;
using ServerVendor.Connect.Data;
using ServerVendor.Connect.RPCHelper;

namespace ServerVendor.Connect.Client
{
    public class Client : IConnectable, IOnEventCallback
    {
        public Socket Start()
        {
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.Connect("127.0.0.1", 5055);
            
            SendEvent(socket);
            
            return socket;
        }

        public void SendEvent(Socket socket)
        {
            var messageA = new RpcMessage
            {
                MethodName = "MyRpcMethod",
                Parameters = [JsonSerializer.SerializeToElement("Hello from client!")]
            };
            RpcHelper.SendRpcMessage(messageA, socket);
        }

        public void NotifyListeners(RpcMessage message)
        {
            Console.WriteLine(message.Parameters[0].GetString());
        }
    }
}