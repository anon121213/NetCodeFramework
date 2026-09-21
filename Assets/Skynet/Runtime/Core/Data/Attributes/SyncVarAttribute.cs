using System;
using Skynet.RpcSystem.ProcessorsData;

namespace Skynet.Data.Attributes
{
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public sealed class SyncVarAttribute : Attribute
    {
        public Authority Authority { get; }
        public NetProtocolType Protocol { get; }
    
        public SyncVarAttribute(
            Authority authority = Authority.Auto,
            NetProtocolType protocol = NetProtocolType.Tcp)
        {
            Authority = authority;
            Protocol = protocol;
        }
    }

    public enum Authority
    {
        Auto = 0,
        Owner = 1,
        Server = 2
    }
}