namespace Hardlight
{
    // Original HLUnityCore.Runtime 0x020000f7; 4 genuine abstract APIs.
    // Preserve the complete shipping contract; these declarations have no method bodies.
    public interface IGameCenterLeaderboardReferencesHolder
    {
        // 0x0600057d
        System.Boolean IsStub { get; }
        // 0x0600057e
        Hardlight.INativeGameCenterLeaderboard NativeGameCenterLeaderboard { get; }
        // 0x0600057f
        Hardlight.IGameCenterLeaderboardListener GameCenterLeaderboardListener { get; }
        // 0x06000580
        System.Boolean AreGameCenterLeaderboardReferencesValid { get; }
    }
}
