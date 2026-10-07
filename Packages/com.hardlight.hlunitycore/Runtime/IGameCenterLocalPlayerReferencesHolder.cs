namespace Hardlight
{
    public interface IGameCenterLocalPlayerReferencesHolder
    {
        INativeGameCenterLocalPlayer NativeGameCenterLocalPlayer { get; }
        IGameCenterLocalPlayerListener GameCenterLocalPlayerListener { get; }
        bool AreGameCenterLocalPlayerReferencesValid { get; }
    }
}
