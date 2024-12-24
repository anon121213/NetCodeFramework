using System.Net;
using System.Net.Sockets;
using System.Reflection;
using ServerVendor.Connect.Proxy;
using ServerVendor.Connect.RPC;
using ServerVendor.Connect.RPC.Attributes;

namespace ServerVendor;

public static class Program
{
    public static bool IsServer { get; private set; }
    public static Socket ServerSocket { get; private set; }
    public static List<Socket> ClientsSockets { get; } = new();

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
        ServerSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        ServerSocket.Bind(new IPEndPoint(IPAddress.Any, 5055));
        ServerSocket.Listen(10);

        Console.WriteLine("Сервер ожидает подключения...");
        
        var clientSocket = ServerSocket.Accept();
        ClientsSockets.Add(clientSocket);

        Console.WriteLine($"Клиент подключен: {clientSocket.RemoteEndPoint}");

        Console.WriteLine("Сервер готов к обработке RPC вызовов.");
        
        Task.Run(() => RpcProxy.ListenForRpcCalls(clientSocket));

        RpcProxy.RegisterRPCInstance<RpcHandler>(RpcHandler.Instance);
        
        var methodInfoClient = typeof(RpcHandler).GetMethod("ClientMethod");
        RpcProxy.TryInvokeRPC<RpcHandler>(methodInfoClient, "Привет от Сервера!");

        Console.ReadLine();
    }

    private static void StartClient()
    {
        var serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        serverSocket.Connect("127.0.0.1", 5055);
        ServerSocket = serverSocket;
        
        Console.WriteLine($"Клиент подключен к серверу: {serverSocket.RemoteEndPoint}");

        Console.WriteLine("Клиент готов к обработке RPC вызовов.");

        Task.Run(() => RpcProxy.ListenForRpcCalls(serverSocket));

        RpcProxy.RegisterRPCInstance<RpcHandler>(RpcHandler.Instance);
        
        var methodInfoClient = typeof(RpcHandler).GetMethod("ServerMethod");
        RpcProxy.TryInvokeRPC<RpcHandler>(methodInfoClient, "Привет от Клиента!");

        Console.ReadLine();
    }
}

public class RpcHandler : IRPCCaller
{
    private static RpcHandler _instance;
    
    public static RpcHandler Instance
    {
        get
        {
            if (_instance != null)
                return _instance;

            _instance = new RpcHandler();
            return _instance;
        }
    }

    [RPCAttributes.ServerRPC]
    public static void ServerMethod(string message)
    {
        Console.WriteLine($"Server received: {message}");
    }

    [RPCAttributes.ClientRPC]
    public static void ClientMethod(string message)
    {
        Console.WriteLine($"Client received: {message}");
    }
}