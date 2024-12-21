using System.Net;
using System.Net.Sockets;
using ServerVendor.Connect.RPC;
using ServerVendor.Connect.RPCHelper;
using ServerVendor.Connect.RPCHelper.Attributes;

public class Program
{
    public static void Main(string[] args)
    {
        Console.WriteLine("Введите 1 для запуска сервера, 2 для клиента:");
        var choice = Console.ReadLine();

        if (choice == "1")
        {
            StartServer();
        }
        else if (choice == "2")
        {
            StartClient();
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

        RpcHelper.ListenForRpcCalls(clientSocket, handler);

        var buffer = new byte[1024];
        while (true)
        {
            try
            {
                var bytesRead = clientSocket.Receive(buffer);
                if (bytesRead > 0)
                {
                    Console.WriteLine(
                        $"Получены данные ({bytesRead} байт): {BitConverter.ToString(buffer, 0, bytesRead)}");
                    RpcProcessor.HandleRpcCall(clientSocket, handler, buffer[..bytesRead]);
                }
                else
                {
                    Console.WriteLine("Клиент разорвал соединение.");
                    break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка на сервере: {ex.Message}");
                break;
            }
        }

        Console.WriteLine("Сервер готов к обработке RPC вызовов.");
    }

    private static void StartClient()
    {
        var clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        try
        {
            clientSocket.Connect("127.0.0.1", 5055);
            Console.WriteLine($"Клиент подключен к серверу: {clientSocket.RemoteEndPoint}");

            dynamic rpcProxy = new RpcProxy(clientSocket);

            Console.WriteLine("Отправка RPC вызова: ServerMethod(\"Привет от клиента!\")");
            rpcProxy.ServerMethod("Привет от клиента 1!");
            //rpcProxy.EmptyMeth();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка на клиенте: {ex.Message}");
        }
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

    [RPCAttributes.ServerRPC]
    public void EmptyMeth()
    {
        Console.WriteLine("Empty Method");
    }
}