namespace Skynet.NetworkComponents
{
    /// <summary>
    /// Marker interface implemented by every framework-native network component
    /// (NetworkObject, NetworkBehaviour, and derivatives). Used by spawn-time
    /// discovery (<c>GetComponentInChildren&lt;INetworkComponent&gt;()</c>) to decide
    /// whether a prefab is actually network-capable.
    /// </summary>
    public interface INetworkComponent
    {
    }
}
