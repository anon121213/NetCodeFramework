using System;

namespace Skynet.Data.Attributes
{
    [AttributeUsage(AttributeTargets.Method)]
    public class ServerRpc : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class ClientRpc : Attribute
    {
    }
}