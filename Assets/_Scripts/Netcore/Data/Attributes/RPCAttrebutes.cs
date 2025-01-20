using System;

namespace _Scripts.Netcore.Data.Attributes
{
    [AttributeUsage(AttributeTargets.Method)]
    public class ServerRPC : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class ClientRPC : Attribute
    {
    }
}