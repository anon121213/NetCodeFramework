using System.Net.Sockets;

namespace ServerVendor.Connect;

public interface IConnectable
{
    Socket Start();
}