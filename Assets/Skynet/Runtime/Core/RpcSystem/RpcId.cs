namespace Skynet.RpcSystem
{
    /// <summary>
    /// Stable 32-bit identifier hashing used across the wire. Fed a canonical string
    /// (e.g. "MyNamespace.MyService" for a type, "MyNamespace.MyService.HandleShoot(Int32,Vector3)"
    /// for a method) it always yields the same int on every machine that runs the same code.
    /// <para/>
    /// This is the same algorithm the source generator uses to emit compile-time constants.
    /// Runtime callers rarely need to invoke it — IDs generally live as generated <c>const int</c>s.
    /// </summary>
    public static class RpcId
    {
        // FNV-1a 32-bit — cheap, well-distributed for short strings, and (crucially) deterministic
        // across .NET runtimes/versions/platforms (unlike string.GetHashCode which was randomised
        // in .NET Core 3.1+).
        public static int Fnv1a(string s)
        {
            if (s == null) return 0;
            unchecked
            {
                int hash = (int)2166136261;
                for (int i = 0; i < s.Length; i++)
                {
                    hash ^= s[i];
                    hash *= 16777619;
                }
                return hash;
            }
        }
    }
}
