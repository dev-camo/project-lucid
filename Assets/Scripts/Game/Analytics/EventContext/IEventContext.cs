namespace Hardlight.Analytics
{
    // Original Game.Runtime02000027:26 genuine abstract accessors, no natural owners.
    public interface IEventContext
    {
        long EVTM { get; set; }
        string GID { get; set; }
        string ClientVersion { get; set; }
        string SessionID { get; set; }
        int EventIDX { get; set; }
        int OrbTotal { get; set; }
        int? AchievementStatueTotal { get; set; }
        int? MoonTotal { get; set; }
        int? ZonesTotal { get; set; }
        int? DreamPowerTier { get; set; }
        string BlueCoinsWalletTotalRange { get; set; }
        string XpAmountTotalRange { get; set; }
        bool? ChallengesState { get; set; }
    }
}
