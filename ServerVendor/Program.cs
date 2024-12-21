using System.Net.Sockets;
using ServerVendor.Connect;
using ServerVendor.Connect.Client;
using ServerVendor.Connect.RPCHelper;

namespace ServerVendor;

public class Vendor
{
    public static readonly bool IsServer = true;
    
    public static void Main()
    {
        IConnectable connectable;
        Socket socket;
        
        if (IsServer)
            connectable = new Connect.Server.Server();
        else
            connectable = new Client();
        
        socket = connectable.Start();
        
        RpcHelper.RegisterCallBack((IOnEventCallback)connectable);
        RpcHelper.Initialize(socket);
    }
}

public class RpcHandler
{
    public void MyRpcMethod(string message)
    {
        Console.WriteLine("Received message in HandlerB: " + message);
    }
}