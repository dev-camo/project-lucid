using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Hardlight
{
    // Original HLUnityCore.Runtime 0x020000f8. The readonly value, fields and
    // service operations preserve shipping behavior; offline replacements are
    // supplied through the separate service boundary.
    public readonly struct Leaderboard : IEquatable<Leaderboard>
    {
        public readonly string BaseLeaderboardID;
        public readonly string Title;
        public readonly LeaderboardType Type;
        public readonly string GroupIdentifier;
        public readonly DateTime StartDate;
        public readonly DateTime NextStartDate;
        public readonly double Duration;
        public readonly DateTime EndDate;
        public readonly int UniqueID;
        private const int LeaderboardToParsePropertiesCount = 7;
        private readonly string m_propertiesJoinToIdentifyLeaderboard;
        private readonly IGameCenterLeaderboardReferencesHolder m_gameCenterLeaderboardReferencesHolder;

        // 0x06000581: EndDate is calculated before the holder and identity.
        // The CRC identity omits title and duration; the equality identity
        // includes them. Both use invariant numeric and round-trip date text.
        public Leaderboard(in LeaderboardInitialisationData leaderboardInitialisationData,
            IGameCenterLeaderboardReferencesHolder gameCenterLeaderboardReferencesHolder)
        {
            BaseLeaderboardID = leaderboardInitialisationData.BaseLeaderboardID;
            Title = leaderboardInitialisationData.Title;
            Type = leaderboardInitialisationData.Type;
            GroupIdentifier = leaderboardInitialisationData.GroupIdentifier;
            StartDate = leaderboardInitialisationData.StartDate;
            NextStartDate = leaderboardInitialisationData.NextStartDate;
            Duration = leaderboardInitialisationData.Duration;
            EndDate = StartDate.AddSeconds(Duration);
            m_gameCenterLeaderboardReferencesHolder = gameCenterLeaderboardReferencesHolder;
            m_propertiesJoinToIdentifyLeaderboard = string.Join(GameCenterLeaderboard.Separator, new[]
            {
                BaseLeaderboardID, Title, ((long)Type).ToString(NumberFormatInfo.InvariantInfo),
                GroupIdentifier, StartDate.ToString(GameCenterLeaderboard.DateFormatString, CultureInfo.InvariantCulture),
                NextStartDate.ToString(GameCenterLeaderboard.DateFormatString, CultureInfo.InvariantCulture),
                Duration.ToString(NumberFormatInfo.InvariantInfo)
            });
            UniqueID = HLCRC32.GenerateInt(string.Join(GameCenterLeaderboard.Separator, new[]
            {
                BaseLeaderboardID, ((long)Type).ToString(NumberFormatInfo.InvariantInfo), GroupIdentifier,
                StartDate.ToString(GameCenterLeaderboard.DateFormatString, CultureInfo.InvariantCulture),
                NextStartDate.ToString(GameCenterLeaderboard.DateFormatString, CultureInfo.InvariantCulture)
            }));
        }

        // 0x06000582: retain the received identity after all public-constructor
        // calculations, including AddSeconds and CRC, have completed.
        private Leaderboard(in LeaderboardInitialisationData leaderboardInitialisationData,
            IGameCenterLeaderboardReferencesHolder gameCenterLeaderboardReferencesHolder,
            string propertiesJoinToIdentifyLeaderboard)
            : this(in leaderboardInitialisationData, gameCenterLeaderboardReferencesHolder)
        {
            m_propertiesJoinToIdentifyLeaderboard = propertiesJoinToIdentifyLeaderboard;
        }

        // 0x06000583: failed enum/date/number conversions intentionally retain
        // their defaults and do not make this method return false. Duration
        // uses the current culture even though construction formats invariantly.
        public static bool TryParseProperties(string propertiesJoinToIdentifyLeaderboard,
            IGameCenterLeaderboardReferencesHolder gameCenterLeaderboardReferencesHolder,
            out Leaderboard leaderboard)
        {
            leaderboard = default;
            if (string.IsNullOrEmpty(propertiesJoinToIdentifyLeaderboard) ||
                gameCenterLeaderboardReferencesHolder == null) return false;
            string[] properties = propertiesJoinToIdentifyLeaderboard.Split(
                GameCenterLeaderboard.SeparatorArray, StringSplitOptions.None);
            if (properties.Length != LeaderboardToParsePropertiesCount) return false;
            Enum.TryParse(properties[2], out LeaderboardType type);
            DateTime.TryParse(properties[4], DateTimeFormatInfo.InvariantInfo,
                DateTimeStyles.AdjustToUniversal, out DateTime startDate);
            DateTime.TryParse(properties[5], DateTimeFormatInfo.InvariantInfo,
                DateTimeStyles.AdjustToUniversal, out DateTime nextStartDate);
            double.TryParse(properties[6], out double duration);
            var leaderboardInitialisationData = new LeaderboardInitialisationData
            {
                BaseLeaderboardID = properties[0], Title = properties[1], Type = type,
                GroupIdentifier = properties[3], StartDate = startDate,
                NextStartDate = nextStartDate, Duration = duration
            };
            leaderboard = new Leaderboard(in leaderboardInitialisationData,
                gameCenterLeaderboardReferencesHolder, propertiesJoinToIdentifyLeaderboard);
            return true;
        }

        // 0x06000584, closure 0x0600058f..0x06000591 and iterator
        // 0x060005b3..0x060005b8. Cached listener removals only occur on normal
        // completion; the shipping Dispose has no cleanup.
        public IEnumerator LoadPreviousOccurrence(Action<bool, Leaderboard?> onLoadPreviousOccurrenceCompleted)
        {
            if (!m_gameCenterLeaderboardReferencesHolder.AreGameCenterLeaderboardReferencesValid)
            {
                onLoadPreviousOccurrenceCompleted?.Invoke(false, null);
                yield break;
            }
            int hashCodeToCheck = Guid.NewGuid().GetHashCode();
            Leaderboard? previousOccurrence = null;
            IGameCenterLeaderboardListener gameCenterLeaderboardListener =
                m_gameCenterLeaderboardReferencesHolder.GameCenterLeaderboardListener;
            gameCenterLeaderboardListener.OnLoadPreviousOccurrence += LoadPreviousOccurrenceEvent;
            bool loadSuccess = false;
            bool loadPreviousOccurrenceCompleted = false;
            gameCenterLeaderboardListener.OnLoadPreviousOccurrenceCompleted += LoadPreviousOccurrenceCompletedEvent;
            m_gameCenterLeaderboardReferencesHolder.NativeGameCenterLeaderboard.LoadPreviousOccurrence(
                hashCodeToCheck, m_propertiesJoinToIdentifyLeaderboard);
            while (!loadPreviousOccurrenceCompleted) yield return null;
            gameCenterLeaderboardListener.OnLoadPreviousOccurrence -= LoadPreviousOccurrenceEvent;
            gameCenterLeaderboardListener.OnLoadPreviousOccurrenceCompleted -= LoadPreviousOccurrenceCompletedEvent;
            onLoadPreviousOccurrenceCompleted?.Invoke(loadSuccess, previousOccurrence);

            void LoadPreviousOccurrenceEvent(int hashCode, Leaderboard leaderboard)
            {
                if (hashCode == hashCodeToCheck) previousOccurrence = leaderboard;
            }
            void LoadPreviousOccurrenceCompletedEvent(int hashCode, bool success)
            {
                if (hashCode != hashCodeToCheck) return;
                loadSuccess = success;
                loadPreviousOccurrenceCompleted = true;
            }
        }

        // 0x06000585, closure 0x06000592..0x06000594 and iterator
        // 0x060005ad..0x060005b2. The white texture is the original fallback;
        // a matching image event can replace it with null without a Unity test.
        public IEnumerator LoadImage(Action<bool, Texture2D> onLoadImageCompleted)
        {
            if (!m_gameCenterLeaderboardReferencesHolder.AreGameCenterLeaderboardReferencesValid)
            {
                onLoadImageCompleted?.Invoke(false, null);
                yield break;
            }
            int hashCodeToCheck = Guid.NewGuid().GetHashCode();
            Texture2D image = Texture2D.whiteTexture;
            IGameCenterLeaderboardListener gameCenterLeaderboardListener =
                m_gameCenterLeaderboardReferencesHolder.GameCenterLeaderboardListener;
            gameCenterLeaderboardListener.OnLoadImage += LoadImageEvent;
            bool loadSuccess = false;
            bool loadImageCompleted = false;
            gameCenterLeaderboardListener.OnLoadImageCompleted += LoadImageCompletedEvent;
            m_gameCenterLeaderboardReferencesHolder.NativeGameCenterLeaderboard.LoadImage(
                hashCodeToCheck, m_propertiesJoinToIdentifyLeaderboard);
            while (!loadImageCompleted) yield return null;
            gameCenterLeaderboardListener.OnLoadImage -= LoadImageEvent;
            gameCenterLeaderboardListener.OnLoadImageCompleted -= LoadImageCompletedEvent;
            onLoadImageCompleted?.Invoke(loadSuccess, image);

            void LoadImageEvent(int hashCode, Texture2D texture2D)
            {
                if (hashCode == hashCodeToCheck) image = texture2D;
            }
            void LoadImageCompletedEvent(int hashCode, bool success)
            {
                if (hashCode != hashCodeToCheck) return;
                loadSuccess = success;
                loadImageCompleted = true;
            }
        }

        // 0x06000586, closure 0x06000595..0x06000596 and iterator
        // 0x060005b9..0x060005be. This single-board path removes its handler
        // normally; the service's multi-board path leaves its handler attached.
        public IEnumerator SubmitScore(long score, ulong context, string identifier,
            Action<bool> onSubmitScoreCompleted)
        {
            if (!m_gameCenterLeaderboardReferencesHolder.AreGameCenterLeaderboardReferencesValid)
            {
                onSubmitScoreCompleted?.Invoke(false);
                yield break;
            }
            int hashCodeToCheck = Guid.NewGuid().GetHashCode();
            bool submitScoreSuccess = false;
            bool submitScoreCompleted = false;
            IGameCenterLeaderboardListener gameCenterLeaderboardListener =
                m_gameCenterLeaderboardReferencesHolder.GameCenterLeaderboardListener;
            gameCenterLeaderboardListener.OnSubmitScoreToLeaderboardCompleted += SubmitScoreToLeaderboardCompletedEvent;
            m_gameCenterLeaderboardReferencesHolder.NativeGameCenterLeaderboard.SubmitScore(
                hashCodeToCheck, score, context, identifier, m_propertiesJoinToIdentifyLeaderboard);
            while (!submitScoreCompleted) yield return null;
            gameCenterLeaderboardListener.OnSubmitScoreToLeaderboardCompleted -= SubmitScoreToLeaderboardCompletedEvent;
            onSubmitScoreCompleted?.Invoke(submitScoreSuccess);

            void SubmitScoreToLeaderboardCompletedEvent(int hashCode, bool success)
            {
                if (hashCode != hashCodeToCheck) return;
                submitScoreSuccess = success;
                submitScoreCompleted = true;
            }
        }

        // 0x06000587, closure 0x06000597..0x0600059b and iterator
        // 0x060005a1..0x060005a6. Querying total player count occurs after all
        // removals even when the caller did not provide a completion callback.
        public IEnumerator LoadEntriesForPlayerScope(LeaderboardPlayerScope playerScope,
            LeaderboardTimeScope timeScope, ulong rangeStartIndex, ulong rangeLength,
            List<LeaderboardEntry> loadedLeaderboardEntriesForPlayerScope,
            Action<bool, LeaderboardEntry?, long> onLoadEntriesForPlayerScopeCompleted)
        {
            if (!m_gameCenterLeaderboardReferencesHolder.AreGameCenterLeaderboardReferencesValid)
            {
                onLoadEntriesForPlayerScopeCompleted?.Invoke(false, null, 0L);
                yield break;
            }
            int hashCodeToCheck = Guid.NewGuid().GetHashCode();
            IGameCenterLeaderboardListener gameCenterLeaderboardListener =
                m_gameCenterLeaderboardReferencesHolder.GameCenterLeaderboardListener;
            gameCenterLeaderboardListener.OnLoadEntriesForPlayerScopeStarted += LoadEntriesForPlayerScopeStarted;
            LeaderboardEntry? localLeaderboardEntryForPlayerScope = null;
            gameCenterLeaderboardListener.OnLoadLocalEntryForPlayerScope += LoadLocalEntryForPlayerScope;
            gameCenterLeaderboardListener.OnLoadEntryForPlayerScope += LoadEntryForPlayerScope;
            bool loadSuccess = false;
            bool loadEntriesForPlayerScopeCompleted = false;
            gameCenterLeaderboardListener.OnLoadEntriesForPlayerScopeCompleted += LoadEntriesForPlayerScopeCompleted;
            INativeGameCenterLeaderboard nativeGameCenterLeaderboard =
                m_gameCenterLeaderboardReferencesHolder.NativeGameCenterLeaderboard;
            nativeGameCenterLeaderboard.LoadEntriesForPlayerScope(hashCodeToCheck, playerScope,
                timeScope, rangeStartIndex, rangeLength, m_propertiesJoinToIdentifyLeaderboard);
            while (!loadEntriesForPlayerScopeCompleted) yield return null;
            gameCenterLeaderboardListener.OnLoadEntriesForPlayerScopeStarted -= LoadEntriesForPlayerScopeStarted;
            gameCenterLeaderboardListener.OnLoadLocalEntryForPlayerScope -= LoadLocalEntryForPlayerScope;
            gameCenterLeaderboardListener.OnLoadEntryForPlayerScope -= LoadEntryForPlayerScope;
            gameCenterLeaderboardListener.OnLoadEntriesForPlayerScopeCompleted -= LoadEntriesForPlayerScopeCompleted;
            long totalPlayerCount = nativeGameCenterLeaderboard.GetLastLoadEntriesForPlayerScopeTotalPlayerCount();
            onLoadEntriesForPlayerScopeCompleted?.Invoke(loadSuccess, localLeaderboardEntryForPlayerScope, totalPlayerCount);

            void LoadEntriesForPlayerScopeStarted(int hashCode)
            {
                if (hashCode == hashCodeToCheck) loadedLeaderboardEntriesForPlayerScope.Clear();
            }
            void LoadLocalEntryForPlayerScope(int hashCode, LeaderboardEntry localLeaderboardEntry)
            {
                if (hashCode == hashCodeToCheck) localLeaderboardEntryForPlayerScope = localLeaderboardEntry;
            }
            void LoadEntryForPlayerScope(int hashCode, LeaderboardEntry leaderboardEntry)
            {
                if (hashCode == hashCodeToCheck) loadedLeaderboardEntriesForPlayerScope.Add(leaderboardEntry);
            }
            void LoadEntriesForPlayerScopeCompleted(int hashCode, bool success)
            {
                if (hashCode != hashCodeToCheck) return;
                loadSuccess = success;
                loadEntriesForPlayerScopeCompleted = true;
            }
        }

        // 0x06000588, closure 0x0600059c..0x060005a0 and iterator
        // 0x060005a7..0x060005ac. Both native architectures subscribe the
        // Started handler to the scope event (slot19) but remove it from the
        // players event (slot28). Preserve this mismatch and resulting retained
        // scope subscription rather than silently correcting the shipped code.
        public IEnumerator LoadEntriesForPlayers(IReadOnlyList<string> identifiers,
            LeaderboardTimeScope timeScope, List<LeaderboardEntry> loadedLeaderboardEntriesForPlayers,
            Action<bool, LeaderboardEntry?> onLoadEntriesForPlayersCompleted)
        {
            if (!m_gameCenterLeaderboardReferencesHolder.AreGameCenterLeaderboardReferencesValid)
            {
                onLoadEntriesForPlayersCompleted?.Invoke(false, null);
                yield break;
            }
            int hashCodeToCheck = Guid.NewGuid().GetHashCode();
            IGameCenterLeaderboardListener gameCenterLeaderboardListener =
                m_gameCenterLeaderboardReferencesHolder.GameCenterLeaderboardListener;
            gameCenterLeaderboardListener.OnLoadEntriesForPlayerScopeStarted += LoadEntriesForPlayersStarted;
            LeaderboardEntry? localLeaderboardEntryForPlayers = null;
            gameCenterLeaderboardListener.OnLoadLocalEntryForPlayers += LoadLocalEntryForPlayers;
            gameCenterLeaderboardListener.OnLoadEntryForPlayers += LoadEntryForPlayers;
            bool loadSuccess = false;
            bool loadEntriesForPlayersCompleted = false;
            gameCenterLeaderboardListener.OnLoadEntriesForPlayersCompleted += LoadEntriesForPlayersCompleted;
            m_gameCenterLeaderboardReferencesHolder.NativeGameCenterLeaderboard.LoadEntriesForPlayers(
                hashCodeToCheck, identifiers, timeScope, m_propertiesJoinToIdentifyLeaderboard);
            while (!loadEntriesForPlayersCompleted) yield return null;
            gameCenterLeaderboardListener.OnLoadEntriesForPlayersStarted -= LoadEntriesForPlayersStarted;
            gameCenterLeaderboardListener.OnLoadLocalEntryForPlayers -= LoadLocalEntryForPlayers;
            gameCenterLeaderboardListener.OnLoadEntryForPlayers -= LoadEntryForPlayers;
            gameCenterLeaderboardListener.OnLoadEntriesForPlayersCompleted -= LoadEntriesForPlayersCompleted;
            onLoadEntriesForPlayersCompleted?.Invoke(loadSuccess, localLeaderboardEntryForPlayers);

            void LoadEntriesForPlayersStarted(int hashCode)
            {
                if (hashCode == hashCodeToCheck) loadedLeaderboardEntriesForPlayers.Clear();
            }
            void LoadLocalEntryForPlayers(int hashCode, LeaderboardEntry localLeaderboardEntry)
            {
                if (hashCode == hashCodeToCheck) localLeaderboardEntryForPlayers = localLeaderboardEntry;
            }
            void LoadEntryForPlayers(int hashCode, LeaderboardEntry leaderboardEntry)
            {
                if (hashCode == hashCodeToCheck) loadedLeaderboardEntriesForPlayers.Add(leaderboardEntry);
            }
            void LoadEntriesForPlayersCompleted(int hashCode, bool success)
            {
                if (hashCode != hashCodeToCheck) return;
                loadSuccess = success;
                loadEntriesForPlayersCompleted = true;
            }
        }

        // 0x06000589..0x0600058d: hash equality is the original behavior,
        // including collisions and a null-identity fault on default values.
        public static bool operator ==(Leaderboard lhs, Leaderboard rhs) => lhs.GetHashCode() == rhs.GetHashCode();
        public static bool operator !=(Leaderboard lhs, Leaderboard rhs) => lhs.GetHashCode() != rhs.GetHashCode();
        public bool Equals(Leaderboard other) => GetHashCode() == other.GetHashCode();
        public override bool Equals(object obj) => obj is Leaderboard other && GetHashCode() == other.GetHashCode();
        public override int GetHashCode() => m_propertiesJoinToIdentifyLeaderboard.GetHashCode();

        // 0x0600058e: date text uses current culture; duration uses invariant
        // culture and an undefined enum contributes an empty name.
        public override string ToString() => string.Concat(new[]
        {
            "BaseLeaderboardID:", BaseLeaderboardID, " Title:", Title,
            " Type:", Enum.GetName(typeof(LeaderboardType), Type),
            " GroupIdentifier:", GroupIdentifier, " StartDate:", StartDate.ToString(),
            " NextStartDate:", NextStartDate.ToString(), " Duration:", Duration.ToString(CultureInfo.InvariantCulture)
        });
    }
}
