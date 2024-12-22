using System.Net;
using System.Net.Sockets;
using ServerVendor.Connect.RPC.Attributes;

public static class Program
{
    public static bool IsServer { get; private set; }

    public static void Main(string[] args)
    {
        Console.WriteLine("Введите 1 для запуска сервера, 2 для клиента:");
        var choice = Console.ReadLine();

        switch (choice)
        {
            case "1":
                StartServer();
                IsServer = true;
                break;
            case "2":
                StartClient();
                IsServer = false;
                break;
        }
    }

    private static void StartServer()
    {
        var serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        serverSocket.Bind(new IPEndPoint(IPAddress.Any, 5055));
        serverSocket.Listen(10);

        Console.WriteLine("Сервер ожидает подключения...");
        var clientSocket = serverSocket.Accept();
        Console.WriteLine($"Клиент подключен: {clientSocket.RemoteEndPoint}");

        var handler = new RpcHandler();
        
        Task.Run(() => RpcHelper.ListenForRpcCalls(clientSocket, handler));

        handler.ClientMethod("Привет от сервера!");

        Console.ReadLine();
    }

    private static void StartClient()
    {
        var clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        clientSocket.Connect("127.0.0.1", 5055);
        Console.WriteLine($"Клиент подключен к серверу: {clientSocket.RemoteEndPoint}");

        Task.Run(() => RpcHelper.ListenForRpcCalls(clientSocket, RpcHandler.Instance));

        RpcHandler.Instance.ServerMethod("Привет от клиента!");

        Console.ReadLine();
    }
}

public class RpcHandler
{
    private static RpcHandler _instace;
    
    public static RpcHandler Instance
    {
        get
        {
            if (_instace == null)
            {
                _instace = new RpcHandler();
                return _instace;
            }

            return _instace;
        }
    }


    [RPCAttributes.ServerRPC]
    public void ServerMethod(string message)
    {
        Console.WriteLine($"Server received: {message}");
    }

    [RPCAttributes.ClientRPC]
    public void ClientMethod(string message)
    {
        Console.WriteLine($"Client received: {message}");
    }
}
