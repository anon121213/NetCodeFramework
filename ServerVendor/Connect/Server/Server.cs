using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using ServerVendor.Connect.Data;
using ServerVendor.Connect.RPCHelper;

namespace ServerVendor.Connect.Server
{
    public class Server : IConnectable, IOnEventCallback
    {
        public Socket Start()
        {
            var handler = new RpcHandler();
            var serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            serverSocket.Bind(new IPEndPoint(IPAddress.Any, 5055));
            serverSocket.Listen(10);

            Console.WriteLine("Server is listening on port 5055...");

            var clientSocket = serverSocket.Accept();
            byte[] buffer = new byte[1024];
            int bytesRead = clientSocket.Receive(buffer);
            RpcMessage message = RpcHelper.DeserializeMessage(buffer.Take(bytesRead).ToArray());

            RpcHelper.ProcessRpcMessage(message, handler);

            SendEvent(clientSocket);
            
            return serverSocket;
        }

        public void SendEvent(Socket clientSocket)
        {
            var eventMessage = new RpcMessage
            {
                MethodName = "EventFromServer",
                Parameters = [JsonSerializer.SerializeToElement("This is an event from server!")]
            };
            RpcHelper.SendRpcMessage(eventMessage, clientSocket);
        }

        public void NotifyListeners(RpcMessage message)
        {
            Console.WriteLine(message.Parameters[0].GetString());
        }
    }
}
