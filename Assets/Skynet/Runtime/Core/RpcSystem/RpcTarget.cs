namespace Skynet.RpcSystem
{
    /// <summary>
    /// Where a <c>[ClientRpc]</c> is delivered when the server sends it.
    /// The source generator emits a different wrapper method shape per value:
    /// <list type="bullet">
    ///   <item><description><see cref="All"/> — <c>Xxx(...)</c>: broadcast to every connected client.</description></item>
    ///   <item><description><see cref="Client"/> — <c>Xxx(int clientId, ...)</c>: unicast to a specific client.</description></item>
    /// </list>
    /// Meaningless for <c>[ServerRpc]</c> — server is the sole recipient there.
    /// </summary>
    public enum RpcTarget
    {
        All = 0,
        Client = 1,
    }
}
