namespace HardlightProject
{
    // Original Game.Runtime020006f7/0600261e..06002620: genuine complete
    //three-member abstract contract. It contains no native method bodies.
    public interface IMissionTimer
    {
        float GetTimeRemainingSeconds();
        float GetTimeLimitSeconds();
        bool IsTimerActive { get; }
    }
}
