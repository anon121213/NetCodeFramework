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
        
        // Запуск прослушивания RPC вызовов
        Task.Run(() => RpcHelper.ListenForRpcCalls(clientSocket, handler));

        // Серверный метод для обработки
        handler.ServerMethod("Привет от сервера!");

        Console.ReadLine();
    }

    private static void StartClient()
    {
        var clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        var handler = new RpcHandler();

        clientSocket.Connect("127.0.0.1", 5055);
        Console.WriteLine($"Клиент подключен к серверу: {clientSocket.RemoteEndPoint}");

        // Запуск прослушивания RPC вызовов
        Task.Run(() => RpcHelper.ListenForRpcCalls(clientSocket, handler));

        // Клиентский метод для обработки
        handler.ClientMethod("Привет от клиента!");

        Console.ReadLine();
    }
}

public class RpcHandler
{
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
