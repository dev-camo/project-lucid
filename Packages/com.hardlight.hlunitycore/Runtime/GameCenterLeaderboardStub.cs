using System;
using System.Collections.Generic;
using System.Globalization;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
    // Whole original HLUnityCore.Runtime 0x02000112. This is the authentic
    // shipping simulated provider; future desktop adapters remain separate.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class GameCenterLeaderboardStub : INativeGameCenterLeaderboard
    {
        private const double OneDayDurationSeconds = 86400.0;
        private readonly IGameCenterLeaderboardListenerCallbackHandler m_gameCenterLeaderboardListenerCallbackHandler;
        private IGameCenterLeaderboardReferencesHolder m_gameCenterLeaderboardReferencesHolder;
        private IGameCenterLocalPlayerReferencesHolder m_gameCenterLocalPlayerReferencesHolder;
        private readonly Dictionary<string, DateTime> m_leaderboardIDToStartDate = new Dictionary<string, DateTime>();
        private long m_totalPlayerCount;

        // 0x060006d1: dictionary initialization precedes Object constructor;
        // callback handler assignment follows it, with no argument guard.
        public GameCenterLeaderboardStub(IGameCenterLeaderboardListenerCallbackHandler gameCenterLeaderboardListenerCallbackHandler)
        {
            m_gameCenterLeaderboardListenerCallbackHandler = gameCenterLeaderboardListenerCallbackHandler;
        }

        // 0x060006d2: compiler-generated destructor finally calls Object.Finalize.
        ~GameCenterLeaderboardStub()
        {
            m_gameCenterLeaderboardReferencesHolder = null;
            m_gameCenterLocalPlayerReferencesHolder = null;
        }

        // 0x060006d3
        public void SetGameCenterLeaderboardReferencesHolder(IGameCenterLeaderboardReferencesHolder gameCenterLeaderboardReferencesHolder)
        {
            if (gameCenterLeaderboardReferencesHolder != null && m_gameCenterLeaderboardReferencesHolder == null)
                m_gameCenterLeaderboardReferencesHolder = gameCenterLeaderboardReferencesHolder;
        }

        // 0x060006d4
        public void SetGameCenterLocalPlayerReferencesHolder(IGameCenterLocalPlayerReferencesHolder gameCenterLocalPlayerReferencesHolder)
        {
            if (gameCenterLocalPlayerReferencesHolder != null && m_gameCenterLocalPlayerReferencesHolder == null)
                m_gameCenterLocalPlayerReferencesHolder = gameCenterLocalPlayerReferencesHolder;
        }

        // 0x060006d5
        public bool IsAvailable() { return true; }
        // 0x060006d6
        public bool IsLegacyAPI() { return false; }
        // 0x060006d7: complete shipping body is empty.
        public void Initialise(string gameObjectName, string separator) { }
        // 0x060006d8: complete shipping body is empty.
        public void Deinitialise() { }

        // 0x060006d9: callback precedes dictionary insertion, including on reload.
        public void LoadLeaderboards(int hashCode, IReadOnlyList<string> leaderboardIDs)
        {
            m_gameCenterLeaderboardListenerCallbackHandler.TriggerLoadLeaderboardsStartedEvent(hashCode);
            for (int i = 0; i < leaderboardIDs.Count; i++)
            {
                string leaderboardID = leaderboardIDs[i];
                DateTime startDate = DateTime.UtcNow;
                DateTime nextStartDate = startDate.AddSeconds(OneDayDurationSeconds);
                var data = new LeaderboardInitialisationData
                {
                    BaseLeaderboardID = leaderboardID,
                    Title = "LeaderboardTitle_" + leaderboardID,
                    Type = (LeaderboardType)0,
                    GroupIdentifier = "GroupIdentifier_" + leaderboardID,
                    StartDate = startDate,
                    NextStartDate = nextStartDate,
                    Duration = 6000.0
                };
                var leaderboard = new Leaderboard(in data, m_gameCenterLeaderboardReferencesHolder);
                m_gameCenterLeaderboardListenerCallbackHandler.TriggerLoadLeaderboardEvent(hashCode, leaderboard);
                if (!m_leaderboardIDToStartDate.ContainsKey(leaderboardID))
                    m_leaderboardIDToStartDate.Add(leaderboardID, startDate);
            }
            m_gameCenterLeaderboardListenerCallbackHandler.TriggerLoadLeaderboardsCompletedEvent(hashCode, true);
        }

        // 0x060006da: score/context/identifier/list are intentionally ignored.
        public void SubmitScore(int hashCode, long score, ulong context, string identifier, IReadOnlyList<string> leaderboardIDs)
        {
            m_gameCenterLeaderboardListenerCallbackHandler.TriggerSubmitScoreToLeaderboardsCompletedEvent(hashCode, true);
        }

        // 0x060006db: dictionary indexer faults and successful completion remain.
        public void LoadPreviousOccurrence(int hashCode, string propertiesJoinToIdentifyLeaderboard)
        {
            if (Leaderboard.TryParseProperties(propertiesJoinToIdentifyLeaderboard, m_gameCenterLeaderboardReferencesHolder, out Leaderboard leaderboard) &&
                leaderboard.StartDate == m_leaderboardIDToStartDate[leaderboard.BaseLeaderboardID])
            {
                DateTime nextStartDate = leaderboard.StartDate;
                DateTime startDate = nextStartDate.AddSeconds(-OneDayDurationSeconds);
                var data = new LeaderboardInitialisationData
                {
                    BaseLeaderboardID = leaderboard.BaseLeaderboardID,
                    Title = leaderboard.Title,
                    Type = leaderboard.Type,
                    GroupIdentifier = leaderboard.GroupIdentifier,
                    StartDate = startDate,
                    NextStartDate = nextStartDate,
                    Duration = OneDayDurationSeconds
                };
                var previousOccurrence = new Leaderboard(in data, m_gameCenterLeaderboardReferencesHolder);
                m_gameCenterLeaderboardListenerCallbackHandler.TriggerLoadPreviousOccurrenceEvent(hashCode, previousOccurrence);
            }
            m_gameCenterLeaderboardListenerCallbackHandler.TriggerLoadPreviousOccurrenceCompletedEvent(hashCode, true);
        }

        // 0x060006dc
        public void SubmitScore(int hashCode, long score, ulong context, string identifier, string propertiesJoinToIdentifyLeaderboard)
        {
            m_gameCenterLeaderboardListenerCallbackHandler.TriggerSubmitScoreToLeaderboardCompletedEvent(hashCode, true);
        }

        // 0x060006dd: whiteTexture is obtained before reading callback receiver.
        public void LoadImage(int hashCode, string propertiesJoinToIdentifyLeaderboard)
        {
            Texture2D texture2D = Texture2D.whiteTexture;
            m_gameCenterLeaderboardListenerCallbackHandler.TriggerLoadImageEvent(hashCode, texture2D);
            m_gameCenterLeaderboardListenerCallbackHandler.TriggerLoadImageCompletedEvent(hashCode, true);
        }
        // 0x060006de
        public int GetLastImageLoadedHeight() { return 0; }
        // 0x060006df
        public int GetLastImageLoadedWidth() { return 0; }

        // 0x060006e0: rangeLength is re-read through the signed stored count after
        // callbacks. Local rank uses rangeStartIndex; generated ranks start at1.
        public void LoadEntriesForPlayerScope(int hashCode, LeaderboardPlayerScope playerScope, LeaderboardTimeScope timeScope,
            ulong rangeStartIndex, ulong rangeLength, string propertiesJoinToIdentifyLeaderboard)
        {
            m_totalPlayerCount = unchecked((long)rangeLength);
            m_gameCenterLeaderboardListenerCallbackHandler.TriggerLoadEntriesForPlayerScopeStartedEvent(hashCode);
            INativeGameCenterLocalPlayer nativeGameCenterLocalPlayer = m_gameCenterLocalPlayerReferencesHolder.NativeGameCenterLocalPlayer;
            string localPlayerProperties = nativeGameCenterLocalPlayer.GetLocalPlayerPropertiesJoin();
            string localPlayerIdentifier = nativeGameCenterLocalPlayer.GamePlayerID;
            Player.TryParseProperties(localPlayerProperties, localPlayerIdentifier, m_gameCenterLocalPlayerReferencesHolder, out Player localPlayer);
            long score = 10000L;
            var localData = new LeaderboardEntryInitialisationData
            {
                Date = DateTime.UtcNow,
                FormattedScore = score.ToString(NumberFormatInfo.CurrentInfo),
                Player = localPlayer,
                Rank = unchecked((long)rangeStartIndex),
                Score = score
            };
            var localEntry = new LeaderboardEntry(in localData, true);
            m_gameCenterLeaderboardListenerCallbackHandler.TriggerLoadLocalEntryForPlayerScopeEvent(hashCode, localEntry);
            long rank = 1L;
            for (int i = 0; i < m_totalPlayerCount; i++)
            {
                if (i == 0)
                    m_gameCenterLeaderboardListenerCallbackHandler.TriggerLoadEntryForPlayerScopeEvent(hashCode, localEntry);
                else
                {
                    string identifier = Guid.NewGuid().ToString();
                    string properties = nativeGameCenterLocalPlayer.GetPlayerPropertiesJoin(identifier);
                    Player.TryParseProperties(properties, identifier, m_gameCenterLocalPlayerReferencesHolder, out Player player);
                    var data = new LeaderboardEntryInitialisationData
                    {
                        Date = DateTime.UtcNow,
                        FormattedScore = score.ToString(NumberFormatInfo.CurrentInfo),
                        Player = player,
                        Rank = rank,
                        Score = score
                    };
                    var entry = new LeaderboardEntry(in data, false);
                    m_gameCenterLeaderboardListenerCallbackHandler.TriggerLoadEntryForPlayerScopeEvent(hashCode, entry);
                }
                score = unchecked(score - 1L);
                rank = unchecked(rank + 1L);
            }
            m_gameCenterLeaderboardListenerCallbackHandler.TriggerLoadEntriesForPlayerScopeCompletedEvent(hashCode, true);
        }
        // 0x060006e1
        public long GetLastLoadEntriesForPlayerScopeTotalPlayerCount() { return m_totalPlayerCount; }

        // 0x060006e2: invariant score text here is distinct from player-scope.
        public void LoadEntriesForPlayers(int hashCode, IReadOnlyList<string> identifiers, LeaderboardTimeScope timeScope,
            string propertiesJoinToIdentifyLeaderboard)
        {
            m_gameCenterLeaderboardListenerCallbackHandler.TriggerLoadEntriesForPlayersStartedEvent(hashCode);
            long score = 10000L;
            INativeGameCenterLocalPlayer nativeGameCenterLocalPlayer = m_gameCenterLocalPlayerReferencesHolder.NativeGameCenterLocalPlayer;
            string localPlayerProperties = nativeGameCenterLocalPlayer.GetLocalPlayerPropertiesJoin();
            string localPlayerIdentifier = nativeGameCenterLocalPlayer.GamePlayerID;
            Player.TryParseProperties(localPlayerProperties, localPlayerIdentifier, m_gameCenterLocalPlayerReferencesHolder, out Player localPlayer);
            var localData = new LeaderboardEntryInitialisationData
            {
                Date = DateTime.UtcNow,
                FormattedScore = score.ToString(NumberFormatInfo.InvariantInfo),
                Player = localPlayer,
                Rank = 1L,
                Score = score
            };
            var localEntry = new LeaderboardEntry(in localData, true);
            m_gameCenterLeaderboardListenerCallbackHandler.TriggerLoadLocalEntryForPlayersEvent(hashCode, localEntry);
            long rank = 1L;
            for (int i = 0; i < identifiers.Count; i++)
            {
                string identifier = identifiers[i];
                score = unchecked(score - 1L);
                string properties = nativeGameCenterLocalPlayer.GetPlayerPropertiesJoin(identifier);
                Player.TryParseProperties(properties, identifier, m_gameCenterLocalPlayerReferencesHolder, out Player player);
                rank = unchecked(rank + 1L);
                var data = new LeaderboardEntryInitialisationData
                {
                    Date = DateTime.UtcNow,
                    FormattedScore = score.ToString(NumberFormatInfo.InvariantInfo),
                    Player = player,
                    Rank = rank,
                    Score = score
                };
                var entry = new LeaderboardEntry(in data, false);
                m_gameCenterLeaderboardListenerCallbackHandler.TriggerLoadEntryForPlayersEvent(hashCode, entry);
            }
            m_gameCenterLeaderboardListenerCallbackHandler.TriggerLoadEntriesForPlayersCompletedEvent(hashCode, true);
        }
        // 0x060006e3
        public void IssueLeaderboardChallenge(string leaderboardId, string message, UnityHLLeaderboardChallengeIssuedCallback callback)
        {
            callback?.Invoke(true, string.Empty);
        }
    }
}
