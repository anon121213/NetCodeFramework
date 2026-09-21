namespace Skynet.Tick
{
    public interface IServerClockSync
    {
        bool IsSynced { get; }
        long TickOffset { get; }
        float CurrentServerTick { get; }
    }
}