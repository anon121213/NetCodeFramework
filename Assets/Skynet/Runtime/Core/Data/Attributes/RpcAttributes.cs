using System;
using Skynet.RpcSystem;
using Skynet.RpcSystem.ProcessorsData;

namespace Skynet.Data.Attributes
{
    /// <summary>
    /// Marks a method as a server-side RPC — callers (clients) invoke it and the body runs on the server.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class ServerRpcAttribute : Attribute
    {
        public NetProtocolType Protocol { get; }
        public HandlerExecution Execution { get; }
    
        public ServerRpcAttribute(
            NetProtocolType protocol = NetProtocolType.Tcp,
            HandlerExecution execution = HandlerExecution.MainThread)
        {
            Protocol = protocol;
            Execution = execution;
        }
    }

    /// <summary>
    /// Marks a method as a client-side RPC — server invokes it and the body runs on client(s).
    /// The <see cref="Target"/> chooses the delivery shape:
    /// <see cref="RpcTarget.All"/> generates a broadcast wrapper <c>Xxx(...)</c>;
    /// <see cref="RpcTarget.Client"/> generates a targeted wrapper <c>Xxx(int clientId, ...)</c>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class ClientRpcAttribute : Attribute
    {
        public NetProtocolType Protocol { get; }
        public RpcTarget Target { get; }
        public HandlerExecution Execution { get; }
    
        public ClientRpcAttribute(
            NetProtocolType protocol = NetProtocolType.Tcp,
            RpcTarget target = RpcTarget.All,
            HandlerExecution execution = HandlerExecution.MainThread)
        {
            Protocol = protocol;
            Target = target;
            Execution = execution;
        }
    }
}
