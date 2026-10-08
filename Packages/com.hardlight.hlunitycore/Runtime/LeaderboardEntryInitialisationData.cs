using System;

namespace Hardlight
{
    // Original HLUnityCore.Runtime 0x02000104: mutable input data, no methods.
    public struct LeaderboardEntryInitialisationData
    {
        public ulong Context;
        public DateTime Date;
        public string FormattedScore;
        public Player Player;
        public long Rank;
        public long Score;
    }
}
