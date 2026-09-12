namespace Skynet.NetworkComponents.RPCComponents
{
    public static class NetworkHashHelper
    {
        public static int ComputeIdForComponents(int netObjHash, System.Type type, int componentIndex)
        {
            unchecked
            {
                int typeHash = (int)2166136261;

                if (type.FullName == null)
                    return (netObjHash * 397 ^ typeHash) * 397 ^ componentIndex;
                
                foreach (char c in type.FullName)
                {
                    typeHash ^= c;
                    typeHash *= 16777619;
                }

                return (netObjHash * 397 ^ typeHash) * 397 ^ componentIndex;
            }
        }
    }
}