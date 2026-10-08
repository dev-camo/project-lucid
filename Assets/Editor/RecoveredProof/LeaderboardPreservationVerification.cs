using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using Hardlight;
using UnityEngine;

namespace ProjectLucid.Verification
{
    public static class LeaderboardPreservationVerification
    {
        private const BindingFlags InstanceFields = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        private const BindingFlags StaticFields = BindingFlags.Static | BindingFlags.NonPublic;
        private static void Check(bool value, string message, ref int count)
        {
            if (!value) throw new InvalidOperationException(message);
            ++count;
        }
        private static bool Throws<T>(Action action) where T : Exception
        {
            try { action(); return false; }
            catch (T) { return true; }
            catch (TargetInvocationException error) when (error.InnerException is T) { return true; }
        }
        private static Delegate Subscribers(object listener, string name) =>
            (Delegate)listener.GetType().GetField(name, InstanceFields).GetValue(listener);
        private static int SubscriberCount(object listener, string name) => Subscribers(listener, name)?.GetInvocationList().Length ?? 0;
        private static string Identity(Leaderboard value) => (string)typeof(Leaderboard)
            .GetField("m_propertiesJoinToIdentifyLeaderboard", InstanceFields).GetValue(value);
        private static string Identity(LeaderboardEntry value) => (string)typeof(LeaderboardEntry)
            .GetField("m_propertiesJoinToIdentifyLeaderboardEntry", InstanceFields).GetValue(value);

        // Restore the actual registry, every preexisting row, and all delegate identities.
        private sealed class ActionSnapshot : IDisposable
        {
            private readonly Dictionary<SystemAction, Dictionary<ISystem, Action<object>>> lookup;
            private readonly List<KeyValuePair<SystemAction, Dictionary<ISystem, Action<object>>>> rows;
            private readonly List<List<KeyValuePair<ISystem, Action<object>>>> entries;
            public ActionSnapshot()
            {
                lookup = (Dictionary<SystemAction, Dictionary<ISystem, Action<object>>>)typeof(ProcessManager)
                    .GetField("s_systemActionLookup", StaticFields).GetValue(null);
                rows = new List<KeyValuePair<SystemAction, Dictionary<ISystem, Action<object>>>>(lookup);
                entries = new List<List<KeyValuePair<ISystem, Action<object>>>>();
                foreach (var row in rows) entries.Add(new List<KeyValuePair<ISystem, Action<object>>>(row.Value));
            }
            public void Dispose()
            {
                lookup.Clear();
                for (int i = 0; i < rows.Count; ++i)
                {
                    rows[i].Value.Clear();
                    foreach (var pair in entries[i]) rows[i].Value.Add(pair.Key, pair.Value);
                    lookup.Add(rows[i].Key, rows[i].Value);
                }
            }
        }
        private static LeaderboardInitialisationData BoardData() => new LeaderboardInitialisationData
        {
            BaseLeaderboardID = "board", Title = "Title", Type = LeaderboardType.Recurring, GroupIdentifier = "group",
            StartDate = new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc),
            NextStartDate = new DateTime(2024, 1, 3, 3, 4, 5, DateTimeKind.Utc), Duration = 1.5
        };

        public static int RecordParsingAndIdentity()
        {
            int count = 0;
            CultureInfo previous = CultureInfo.CurrentCulture;
            using (new ActionSnapshot())
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
                var playerHolder = new GameCenterLocalPlayer(true);
                var service = new GameCenterLeaderboard(playerHolder, forceStub: true);
                var holder = (IGameCenterLeaderboardReferencesHolder)service;
                var data = BoardData(); var board = new Leaderboard(in data, holder);
                const string expected = "board|Title|1|group|2024-01-02T03:04:05.0000000Z|2024-01-03T03:04:05.0000000Z|1.5";
                Check(Identity(board) == expected, "Constructor must retain seven ordered invariant identity parts.", ref count);
                Check(board.EndDate == data.StartDate.AddMilliseconds(1500), "EndDate must add duration to StartDate.", ref count);
                Check(board.GetHashCode() == expected.GetHashCode(), "Equality hash must come from the full identity.", ref count);
                var changed = data; changed.Title = "Other"; changed.Duration = 7;
                var different = new Leaderboard(in changed, holder);
                Check(board.UniqueID == different.UniqueID && Identity(board) != Identity(different), "CRC omits title/duration while equality identity retains them.", ref count);
                foreach (string invalid in new[] { null, "", "a", "a|b|c|d|e|f", "a|b|c|d|e|f|g|h" })
                {
                    Check(!Leaderboard.TryParseProperties(invalid, holder, out Leaderboard reset), "Incorrect record shape must fail.", ref count);
                    Check(reset.BaseLeaderboardID == null, "Rejected record parsing must reset the output.", ref count);
                }
                Check(!Leaderboard.TryParseProperties(expected, null, out Leaderboard ignored), "Missing holder must reject a leaderboard.", ref count);
                Check(Leaderboard.TryParseProperties("a|b|invalid|d|bad|bad|bad", holder, out Leaderboard permissive), "Conversions do not reject a seven-part record.", ref count);
                Check(permissive.Type == LeaderboardType.Classic && permissive.StartDate == default && permissive.NextStartDate == default && permissive.Duration == 0,
                    "Invalid converted fields retain their defaults.", ref count);
                Check(permissive.GetHashCode() == "a|b|invalid|d|bad|bad|bad".GetHashCode(), "Parsed identity must retain the exact received text.", ref count);
                Check(Leaderboard.TryParseProperties("a|b|recurring|d|bad|bad|0", holder, out Leaderboard lower) && lower.Type == LeaderboardType.Classic,
                    "Enum names remain case sensitive while conversion failure is accepted.", ref count);
                Check(Leaderboard.TryParseProperties("a|b|7|d|bad|bad|0", holder, out Leaderboard unnamed) && (long)unnamed.Type == 7,
                    "Undefined numeric enum values are retained.", ref count);
                Check(unnamed.ToString().Contains(" Type: GroupIdentifier:"), "Undefined enum display contributes no name.", ref count);
                Check(Leaderboard.TryParseProperties(expected, holder, out Leaderboard parsed) && board == parsed && board.Equals((object)parsed), "Record roundtrip must preserve equality.", ref count);
                Check(!board.Equals(null) && !board.Equals("board"), "Object equality rejects unrelated values.", ref count);
                Check(Throws<NullReferenceException>(() => default(Leaderboard).GetHashCode()), "Default leaderboard must retain its hash null fault.", ref count);
                Check(!default(Leaderboard).Equals((object)"other"), "Default record object equality rejects other types before hashing.", ref count);
                var badDuration = data; badDuration.Duration = double.MaxValue;
                Check(Throws<ArgumentException>(() => { var value = new Leaderboard(in badDuration, holder); }), "Out-of-range finite date arithmetic retains its original fault.", ref count);
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
                Check(Leaderboard.TryParseProperties("a|b|0|d|bad|bad|1,5", holder, out Leaderboard french) && french.Duration == 1.5,
                    "Duration parsing must use the current culture.", ref count);
                Check(Leaderboard.TryParseProperties("a|b|0|d|bad|bad|1.5", holder, out Leaderboard invariantInFrench) && invariantInFrench.Duration == 0,
                    "Invariant decimal text is not forced during current-culture parsing.", ref count);
                Check(french.ToString().EndsWith(" Duration:1.5", StringComparison.Ordinal), "Duration display remains invariant.", ref count);
                CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
                Check(Leaderboard.TryParseProperties("a|b|0|d|2024-01-02T03:04:05+02:00|2024-01-03T03:04:05+02:00|0", holder, out Leaderboard utc) &&
                    utc.StartDate == new DateTime(2024, 1, 2, 1, 4, 5, DateTimeKind.Utc), "Date parsing retains AdjustToUniversal.", ref count);
                Check(Enum.GetUnderlyingType(typeof(LeaderboardPlayerScope)) == typeof(long) && Enum.GetUnderlyingType(typeof(LeaderboardTimeScope)) == typeof(long),
                    "Original scope enums remain Int64.", ref count);

                var entryData = new LeaderboardEntryInitialisationData { Context = ulong.MaxValue, Date = data.StartDate, FormattedScore = "display", Rank = -2, Score = long.MinValue,
                    Player = new Player("player-a", "name-a", "lookup-a", playerHolder) };
                var entry = new LeaderboardEntry(in entryData, true);
                const string expectedEntry = "18446744073709551615|2024-01-02T03:04:05.0000000Z|display|-2|-9223372036854775808|True";
                Check(Identity(entry) == expectedEntry && entry.GetHashCode() == expectedEntry.GetHashCode(), "Entry identity must retain six ordered invariant parts.", ref count);
                var otherData = entryData; otherData.Player = new Player("player-b", "name-b", "lookup-b", playerHolder);
                Check(entry == new LeaderboardEntry(in otherData, true), "Entry equality deliberately excludes Player.", ref count);
                Check(LeaderboardEntry.TryParseProperties(expectedEntry, playerHolder, out LeaderboardEntry entryParsed) && entryParsed == entry && entryParsed.IsLocalPlayer,
                    "Entry roundtrip retains local selection and exact identity.", ref count);
                Check(entryParsed.Player.GamePlayerID == ((IGameCenterLocalPlayerReferencesHolder)playerHolder).NativeGameCenterLocalPlayer.GamePlayerID,
                    "Local entry parsing must fetch the genuine local player.", ref count);
                string remoteText = expectedEntry.Replace("|True", "|False");
                Check(LeaderboardEntry.TryParseProperties(remoteText, playerHolder, out LeaderboardEntry remote) && !remote.IsLocalPlayer && remote.Player.DisplayName == "Stub Player",
                    "Remote entry parsing uses the genuine player lookup.", ref count);
                foreach (string invalid in new[] { null, "", "a", "-1|2024-01-02|x|1|1|True", "0|bad|x|1|1|True", "0|2024-01-02|x|bad|1|True", "0|2024-01-02|x|1|bad|True", "0|2024-01-02|x|1|1|bad" })
                {
                    Check(!LeaderboardEntry.TryParseProperties(invalid, null, out LeaderboardEntry reset), "Bad entry conversion must reject before native holder access.", ref count);
                    Check(reset.FormattedScore == null && reset.Context == 0, "Rejected entry parsing resets output.", ref count);
                }
                Check(Throws<NullReferenceException>(() => LeaderboardEntry.TryParseProperties(expectedEntry, null, out LeaderboardEntry reset)), "Valid entry text with a missing holder retains its null fault.", ref count);
                Check(Throws<NullReferenceException>(() => default(LeaderboardEntry).GetHashCode()), "Default entry retains its hash null fault.", ref count);
                Check(!entry.Equals((object)"entry") && !entry.Equals(null), "Entry object equality rejects unrelated types.", ref count);
                Check(entry.ToString().Contains(" Player:'GamePlayerID:player-a DisplayName:name-a' Rank:-2"), "Entry display retains quotes and field order.", ref count);
            }
            finally { CultureInfo.CurrentCulture = previous; }
            return count;
        }

        public static int ShippingRequestsAndRecurrence()
        {
            int count = 0;
            using (new ActionSnapshot())
            {
                var players = new GameCenterLocalPlayer(true);
                var service = new GameCenterLeaderboard(players, forceStub: true);
                var refs = (IGameCenterLeaderboardReferencesHolder)service;
                var listener = (GameCenterLeaderboardListenerStub)refs.GameCenterLeaderboardListener;
                Check(refs.AreGameCenterLeaderboardReferencesValid && refs.IsStub && refs.NativeGameCenterLeaderboard is GameCenterLeaderboardStub,
                    "Explicit local route must use the original complete providers.", ref count);
                Check(service.IsAvailable() && !service.IsLegacyAPI() && listener.Name == null, "Original local availability and listener name are retained.", ref count);
                service.Initialise(); service.Deinitialise();
                var boards = new List<Leaderboard> { default };
                int completions = 0; bool success = false;
                IEnumerator load = service.LoadLeaderboards(new[] { "first", "second" }, boards, value => { ++completions; success = value; });
                Check(boards.Count == 1 && completions == 0, "Creating an iterator must not execute its request.", ref count);
                Check(!load.MoveNext() && success && completions == 1 && boards.Count == 2, "Shipping synchronous request clears the list, adds boards and completes without yielding.", ref count);
                Check(boards[0].BaseLeaderboardID == "first" && boards[1].Title == "LeaderboardTitle_second" && boards[0].GroupIdentifier == "GroupIdentifier_first",
                    "Shipping board identity/title/group literals are retained.", ref count);
                Check(boards[0].Duration == 6000 && boards[0].NextStartDate - boards[0].StartDate == TimeSpan.FromDays(1), "Initial duration and recurrence interval remain distinct.", ref count);
                Check(SubscriberCount(listener, "OnLoadLeaderboardsStarted") == 0 && SubscriberCount(listener, "OnLoadLeaderboard") == 0 && SubscriberCount(listener, "OnLoadLeaderboardsCompleted") == 0,
                    "Normal board load removes all three subscribers.", ref count);
                Check(Throws<NotSupportedException>(() => load.Reset()), "Original iterator Reset must throw.", ref count);
                ((IDisposable)load).Dispose();
                Leaderboard? previous = null;
                Check(!boards[0].LoadPreviousOccurrence((value, record) => { success = value; previous = record; }).MoveNext() && success && previous.HasValue,
                    "Current occurrence must return its shipping previous occurrence synchronously.", ref count);
                Check(previous.Value.StartDate == boards[0].StartDate.AddDays(-1) && previous.Value.NextStartDate == boards[0].StartDate && previous.Value.Duration == 86400,
                    "Previous occurrence uses a full day and retains the current start as next date.", ref count);
                Check(SubscriberCount(listener, "OnLoadPreviousOccurrence") == 0 && SubscriberCount(listener, "OnLoadPreviousOccurrenceCompleted") == 0, "Normal recurrence lookup removes both listeners.", ref count);
                var differentData = BoardData(); differentData.BaseLeaderboardID = "first";
                var otherDate = new Leaderboard(in differentData, refs); previous = default;
                Check(!otherDate.LoadPreviousOccurrence((value, record) => { success = value; previous = record; }).MoveNext() && success && !previous.HasValue,
                    "A different start date completes successfully without a previous occurrence.", ref count);
                int scoreCalls = 0;
                Check(!service.SubmitScore(long.MinValue, ulong.MaxValue, null, null, value => { ++scoreCalls; success = value; }).MoveNext() && success && scoreCalls == 1,
                    "Shipping multi-board score completes true even for ignored null inputs.", ref count);
                Check(SubscriberCount(listener, "OnSubmitScoreToLeaderboardsCompleted") == 1, "Multi-board score keeps its original subscriber after completion.", ref count);
                Check(!service.SubmitScore(0, 0, "score", new string[0], null).MoveNext() && SubscriberCount(listener, "OnSubmitScoreToLeaderboardsCompleted") == 2,
                    "Repeated multi-board scores retain independent original subscribers.", ref count);
                Check(!boards[0].SubmitScore(7, 9, null, value => { success = value; }).MoveNext() && success && SubscriberCount(listener, "OnSubmitScoreToLeaderboardCompleted") == 0,
                    "Single-board score removes its completion subscriber normally.", ref count);
                int challengeCalls = 0; string message = null;
                service.IssueChallenge(null, null, (value, text) => { ++challengeCalls; success = value; message = text; }); service.IssueChallenge(null, null, null);
                Check(challengeCalls == 1 && success && message == string.Empty, "Shipping leaderboard challenge uses true/empty callback and tolerates null.", ref count);
                Check(!load.MoveNext() && completions == 1, "Completed iterator must not complete again.", ref count);
            }
            return count;
        }

        public static int EntryListsAndRetainedSubscription()
        {
            int count = 0;
            using (new ActionSnapshot())
            {
                var service = new GameCenterLeaderboard(new GameCenterLocalPlayer(true), forceStub: true);
                var refs = (IGameCenterLeaderboardReferencesHolder)service;
                var listener = (GameCenterLeaderboardListenerStub)refs.GameCenterLeaderboardListener;
                var boards = new List<Leaderboard>(); service.LoadLeaderboards(new[] { "board" }, boards, null).MoveNext();
                var entries = new List<LeaderboardEntry> { default };
                bool success = false; LeaderboardEntry? local = null; long total = -1;
                Check(!boards[0].LoadEntriesForPlayerScope(LeaderboardPlayerScope.Global, LeaderboardTimeScope.AllTime, 10, 3, entries,
                    (value, entry, number) => { success = value; local = entry; total = number; }).MoveNext(), "Shipping scope request finishes synchronously.", ref count);
                Check(success && total == 3 && local.HasValue && entries.Count == 3, "Scope request clears old entries and preserves total/local result.", ref count);
                Check(local.Value.IsLocalPlayer && local.Value.Rank == 10 && local.Value.Score == 10000 && entries[0] == local.Value,
                    "Local scope entry retains requested start rank and is also first in the list.", ref count);
                Check(entries[1].Rank == 2 && entries[1].Score == 9999 && !entries[1].IsLocalPlayer && entries[2].Rank == 3 && entries[2].Score == 9998,
                    "Simulated scope entries retain separate generated ranks and descending scores.", ref count);
                Check(SubscriberCount(listener, "OnLoadEntriesForPlayerScopeStarted") == 0 && SubscriberCount(listener, "OnLoadLocalEntryForPlayerScope") == 0 &&
                    SubscriberCount(listener, "OnLoadEntryForPlayerScope") == 0 && SubscriberCount(listener, "OnLoadEntriesForPlayerScopeCompleted") == 0,
                    "Normal scope request removes all four listeners.", ref count);
                var selected = new List<LeaderboardEntry> { entries[0] }; local = null;
                Check(!boards[0].LoadEntriesForPlayers(new[] { "a", "b" }, LeaderboardTimeScope.Today, selected, (value, entry) => { success = value; local = entry; }).MoveNext(),
                    "Shipping selected-player request finishes synchronously.", ref count);
                Check(success && local.HasValue && local.Value.IsLocalPlayer && selected.Count == 3 && selected[0] == entries[0],
                    "Original mismatched Started subscription leaves the caller's existing entry in place.", ref count);
                Check(selected[1].Rank == 2 && selected[1].Score == 9999 && selected[2].Rank == 3 && selected[2].Score == 9998,
                    "Selected-player entries retain predecremented scores and incremented ranks.", ref count);
                Check(SubscriberCount(listener, "OnLoadEntriesForPlayerScopeStarted") == 1 && SubscriberCount(listener, "OnLoadEntriesForPlayersStarted") == 0,
                    "Normal selected-player completion retains its scope Started subscriber.", ref count);
                Check(SubscriberCount(listener, "OnLoadLocalEntryForPlayers") == 0 && SubscriberCount(listener, "OnLoadEntryForPlayers") == 0 && SubscriberCount(listener, "OnLoadEntriesForPlayersCompleted") == 0,
                    "The other selected-player handlers are removed normally.", ref count);
                Delegate retained = Subscribers(listener, "OnLoadEntriesForPlayerScopeStarted");
                int retainedHash = (int)retained.Target.GetType().GetField("hashCodeToCheck", InstanceFields).GetValue(retained.Target);
                listener.TriggerLoadEntriesForPlayerScopeStartedEvent(unchecked(retainedHash + 1));
                Check(selected.Count == 3, "Retained handler must filter an unrelated request hash.", ref count);
                listener.TriggerLoadEntriesForPlayerScopeStartedEvent(retainedHash);
                Check(selected.Count == 0, "Matching retained scope handler still clears the completed players list.", ref count);
                listener.Clear();
                Check(SubscriberCount(listener, "OnLoadEntriesForPlayerScopeStarted") == 1, "Original Stub Clear keeps subscribers.", ref count);
                Check(!boards[0].LoadEntriesForPlayerScope(LeaderboardPlayerScope.FriendsOnly, LeaderboardTimeScope.Week, ulong.MaxValue, ulong.MaxValue, entries,
                    (value, entry, number) => { success = value; local = entry; total = number; }).MoveNext() && success && total == -1 && entries.Count == 0 && local.Value.Rank == -1,
                    "Unsigned range values retain signed count/rank conversion without inventing a clamp.", ref count);
            }
            return count;
        }

        public static int RequestFaultsAndDisposal()
        {
            int count = 0;
            using (new ActionSnapshot())
            {
                var service = new GameCenterLeaderboard(new GameCenterLocalPlayer(true), forceStub: true);
                var listener = (GameCenterLeaderboardListenerStub)((IGameCenterLeaderboardReferencesHolder)service).GameCenterLeaderboardListener;
                int completions = 0;
                IEnumerator faulted = service.LoadLeaderboards(new[] { "board" }, null, value => ++completions);
                Check(Throws<NullReferenceException>(() => faulted.MoveNext()) && completions == 0, "Started list fault must precede completion.", ref count);
                Check(SubscriberCount(listener, "OnLoadLeaderboardsStarted") == 1 && SubscriberCount(listener, "OnLoadLeaderboard") == 1 && SubscriberCount(listener, "OnLoadLeaderboardsCompleted") == 1,
                    "Faulted load retains all three original subscribers.", ref count);
                ((IDisposable)faulted).Dispose();
                Check(SubscriberCount(listener, "OnLoadLeaderboardsStarted") == 1 && SubscriberCount(listener, "OnLoadLeaderboardsCompleted") == 1,
                    "Original empty Dispose must not add fault cleanup.", ref count);
                var fresh = new GameCenterLeaderboard(new GameCenterLocalPlayer(true), forceStub: true);
                var refs = (IGameCenterLeaderboardReferencesHolder)fresh;
                var data = BoardData(); data.BaseLeaderboardID = "uncached";
                var missing = new Leaderboard(in data, refs);
                IEnumerator recurrence = missing.LoadPreviousOccurrence((value, result) => ++completions);
                Check(Throws<KeyNotFoundException>(() => recurrence.MoveNext()) && completions == 0, "Unknown original occurrence must retain direct dictionary fault.", ref count);
                var freshListener = (GameCenterLeaderboardListenerStub)refs.GameCenterLeaderboardListener;
                ((IDisposable)recurrence).Dispose();
                Check(SubscriberCount(freshListener, "OnLoadPreviousOccurrence") == 1 && SubscriberCount(freshListener, "OnLoadPreviousOccurrenceCompleted") == 1,
                    "Faulted previous occurrence keeps both subscribers through disposal.", ref count);
                IEnumerator unstarted = fresh.LoadLeaderboards(null, null, null); ((IDisposable)unstarted).Dispose();
                Check(SubscriberCount(freshListener, "OnLoadLeaderboardsStarted") == 0, "Disposing an unstarted iterator creates no subscribers.", ref count);
                Check(Throws<NullReferenceException>(() => default(Leaderboard).SubmitScore(0, 0, null, null).MoveNext()), "Default record request must retain missing-holder fault.", ref count);
            }
            return count;
        }

        private static void Dispatch(string name, string parameter)
        {
            IntPtr namePtr = IntPtr.Zero, parameterPtr = IntPtr.Zero;
            try
            {
                namePtr = Marshal.StringToHGlobalAuto(name);
                if (parameter != null) parameterPtr = Marshal.StringToHGlobalAuto(parameter);
                GameCenterLeaderboardListenerMacOS.Callback(namePtr, parameterPtr);
            }
            finally
            {
                if (namePtr != IntPtr.Zero) Marshal.FreeHGlobal(namePtr);
                if (parameterPtr != IntPtr.Zero) Marshal.FreeHGlobal(parameterPtr);
            }
        }
        public static int ManagedCallbackRouting()
        {
            int count = 0;
            FieldInfo stringMap = typeof(GameCenterLeaderboardListenerMacOS).GetField("s_stringCallbackMap", StaticFields);
            FieldInfo plainMap = typeof(GameCenterLeaderboardListenerMacOS).GetField("s_callbackMap", StaticFields);
            object oldString = stringMap.GetValue(null), oldPlain = plainMap.GetValue(null);
            try
            {
                var first = new GameCenterLeaderboardListenerMacOS();
                Check(first.Name == null, "Original Mac listener has no GameObject name.", ref count);
                Check(((IReadOnlyDictionary<string, Action<string>>)stringMap.GetValue(null)).Count == 15 && ((IReadOnlyDictionary<string, Action>)plainMap.GetValue(null)).Count == 3,
                    "Constructor publishes fifteen string and three plain callbacks.", ref count);
                int oldCalls = 0, started = 0, completed = 0, hash = -1; bool success = false;
                first.OnLoadLeaderboardsStarted += value => ++oldCalls;
                var second = new GameCenterLeaderboardListenerMacOS();
                second.OnLoadLeaderboardsStarted += value => { ++started; hash = value; };
                second.OnLoadLeaderboardsCompleted += (value, result) => { ++completed; hash = value; success = result; };
                Dispatch("SetHashCode", "-12"); Dispatch("LoadLeaderboardsStarted", null);
                Check(started == 1 && hash == -12 && oldCalls == 0, "Newest listener replaces static map targets.", ref count);
                Dispatch("LoadLeaderboardsStarted", "");
                Check(started == 2 && hash == -12, "Empty parameter chooses the plain callback map.", ref count);
                Dispatch("LoadLeaderboardsCompleted", "invalid");
                Check(completed == 0, "Malformed boolean suppresses completion.", ref count);
                Dispatch("LoadLeaderboardsCompleted", "False");
                Check(completed == 1 && !success && hash == -12, "Valid false completion retains request hash.", ref count);
                Dispatch("SetHashCode", "invalid"); Dispatch("LoadLeaderboardsStarted", null);
                Check(started == 3 && hash == 0, "Invalid hash text resets stored hash to zero.", ref count);
                second.Clear(); Dispatch("LoadLeaderboardsCompleted", "True");
                Check(completed == 2 && success, "Clear retains callback maps and subscribers.", ref count);
                Check(GameCenterLeaderboardUtility.IsStubNeeded(true) && !GameCenterLeaderboardUtility.IsStubNeeded(false), "Original factory helper returns only its argument.", ref count);
                Check(GameCenterLeaderboardUtility.CreateNativeGameCenterLeaderboard(null, null, null, null, false) is GameCenterLeaderboardMacOS,
                    "Original false factory route is retained without invoking imports.", ref count);
            }
            finally { stringMap.SetValue(null, oldString); plainMap.SetValue(null, oldPlain); }
            return count;
        }

        // This scenario requires the real Unity engine and explicitly selects the original shipping stub.
        public static int ShippingImageRequestInUnity()
        {
            int count = 0;
            using (new ActionSnapshot())
            {
                var service = new GameCenterLeaderboard(new GameCenterLocalPlayer(true), forceStub: true);
                var refs = (IGameCenterLeaderboardReferencesHolder)service;
                var listener = (GameCenterLeaderboardListenerStub)refs.GameCenterLeaderboardListener;
                var data = BoardData(); var board = new Leaderboard(in data, refs);
                Texture2D image = null; bool success = false; int completions = 0;
                IEnumerator load = board.LoadImage((value, result) => { success = value; image = result; ++completions; });
                Check(!load.MoveNext() && success && completions == 1 && ReferenceEquals(image, Texture2D.whiteTexture), "Original local image request returns whiteTexture synchronously.", ref count);
                Check(SubscriberCount(listener, "OnLoadImage") == 0 && SubscriberCount(listener, "OnLoadImageCompleted") == 0, "Normal image request removes both listeners.", ref count);
                ((IDisposable)load).Dispose();
                Check(!load.MoveNext() && completions == 1, "Completed image iterator remains complete.", ref count);
            }
            return count;
        }
    }
}
