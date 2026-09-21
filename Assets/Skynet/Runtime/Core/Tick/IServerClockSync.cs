namespace Skynet.Tick
{
    internal interface IServerClockSync
    {
        bool IsSynced { get; }
        long TickOffset { get; }
        float CurrentServerTick { get; }
    }
}