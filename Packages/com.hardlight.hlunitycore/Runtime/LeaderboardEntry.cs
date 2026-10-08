using System;
using System.Globalization;

namespace Hardlight
{
    // Original HLUnityCore.Runtime 0x02000103. These are the original record
    // semantics, including hash collisions and faults on default-valued records.
    public readonly struct LeaderboardEntry : IEquatable<LeaderboardEntry>
    {
        public readonly ulong Context;
        public readonly DateTime Date;
        public readonly string FormattedScore;
        public readonly Player Player;
        public readonly long Rank;
        public readonly long Score;
        public readonly bool IsLocalPlayer;
        private const int LeaderboardEntryToParsePropertiesCount = 6;
        private readonly string m_propertiesJoinToIdentifyLeaderboardEntry;

        // 0x060005bf: the six-part identity deliberately omits Player. Numeric
        // identity formatting is invariant; display formatting below is current.
        public LeaderboardEntry(in LeaderboardEntryInitialisationData leaderboardEntryInitialisationData, bool isLocalPlayer)
        {
            Context = leaderboardEntryInitialisationData.Context;
            Date = leaderboardEntryInitialisationData.Date;
            FormattedScore = leaderboardEntryInitialisationData.FormattedScore;
            Player = leaderboardEntryInitialisationData.Player;
            Rank = leaderboardEntryInitialisationData.Rank;
            Score = leaderboardEntryInitialisationData.Score;
            IsLocalPlayer = isLocalPlayer;
            m_propertiesJoinToIdentifyLeaderboardEntry = string.Join("|", new string[]
            {
                Context.ToString(NumberFormatInfo.InvariantInfo),
                Date.ToString("o", CultureInfo.InvariantCulture),
                FormattedScore,
                Rank.ToString(NumberFormatInfo.InvariantInfo),
                Score.ToString(NumberFormatInfo.InvariantInfo),
                IsLocalPlayer.ToString()
            });
        }

        // 0x060005c0: the canonical identity is generated first, then replaced
        // with the exact received text. Preserve the constructor's allocations.
        private LeaderboardEntry(in LeaderboardEntryInitialisationData leaderboardEntryInitialisationData,
            bool isLocalPlayer, string propertiesJoinToIdentifyLeaderboardEntry)
            : this(in leaderboardEntryInitialisationData, isLocalPlayer)
        {
            m_propertiesJoinToIdentifyLeaderboardEntry = propertiesJoinToIdentifyLeaderboardEntry;
        }

        // 0x060005c1: numeric parsing uses its original default culture, whereas
        // the date uses invariant culture and AdjustToUniversal. Failed parsing
        // resets out without querying native player references. The remote player
        // lookup uses the entire received identity, rather than an extracted part.
        public static bool TryParseProperties(string propertiesJoinToIdentifyLeaderboardEntry,
            IGameCenterLocalPlayerReferencesHolder gameCenterLocalPlayerReferencesHolder,
            out LeaderboardEntry leaderboardEntry)
        {
            leaderboardEntry = default;
            if (string.IsNullOrEmpty(propertiesJoinToIdentifyLeaderboardEntry)) return false;
            string[] properties = propertiesJoinToIdentifyLeaderboardEntry.Split(GameCenterLeaderboard.SeparatorArray, StringSplitOptions.None);
            if (properties.Length != LeaderboardEntryToParsePropertiesCount) return false;
            if (!ulong.TryParse(properties[0], out ulong context)) return false;
            if (!DateTime.TryParse(properties[1], CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out DateTime date)) return false;
            if (!long.TryParse(properties[3], out long rank)) return false;
            if (!long.TryParse(properties[4], out long score)) return false;
            if (!bool.TryParse(properties[5], out bool isLocalPlayer)) return false;
            string formattedScore = properties[2];
            INativeGameCenterLocalPlayer nativeGameCenterLocalPlayer = gameCenterLocalPlayerReferencesHolder.NativeGameCenterLocalPlayer;
            string identifier = isLocalPlayer ? nativeGameCenterLocalPlayer.GamePlayerID : propertiesJoinToIdentifyLeaderboardEntry;
            string playerProperties = isLocalPlayer
                ? nativeGameCenterLocalPlayer.GetLocalPlayerPropertiesJoin()
                : nativeGameCenterLocalPlayer.GetPlayerPropertiesJoin(identifier);
            if (!Player.TryParseProperties(playerProperties, identifier, gameCenterLocalPlayerReferencesHolder, out Player player)) return false;
            LeaderboardEntryInitialisationData data = new LeaderboardEntryInitialisationData
            {
                Context = context,
                Date = date,
                FormattedScore = formattedScore,
                Player = player,
                Rank = rank,
                Score = score
            };
            leaderboardEntry = new LeaderboardEntry(in data, isLocalPlayer, propertiesJoinToIdentifyLeaderboardEntry);
            return true;
        }

        // 0x060005c2-0x060005c6: equality compares identity hash codes only.
        public static bool operator ==(LeaderboardEntry lhs, LeaderboardEntry rhs) => lhs.GetHashCode() == rhs.GetHashCode();
        public static bool operator !=(LeaderboardEntry lhs, LeaderboardEntry rhs) => lhs.GetHashCode() != rhs.GetHashCode();
        public bool Equals(LeaderboardEntry other) => GetHashCode() == other.GetHashCode();
        public override bool Equals(object obj) => obj is LeaderboardEntry other && this == other;
        public override int GetHashCode() => m_propertiesJoinToIdentifyLeaderboardEntry.GetHashCode();

        // 0x060005c7: the original twelve strings retain current-culture numeric
        // and date display, and the quote around Player; IsLocalPlayer is absent.
        public override string ToString() => string.Concat(new string[]
        {
            "Context:", Context.ToString(), " Date:", Date.ToString(),
            " FormattedScore:", FormattedScore, " Player:'", Player.ToString(),
            "' Rank:", Rank.ToString(), " Score:", Score.ToString()
        });
    }
}
