using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using ProjectLucid.Offline;

namespace ProjectLucid.Tests
{
    // Owned managed leaderboard objects only: no GameCenter plugin, native
    // leaderboard or Apple service is reached. Shared registry rows and the
    // macOS listener's static callback maps are borrowed and put back.
    public sealed class OriginalLeaderboardGapTests
    {
        private const BindingFlags AnyStatic = BindingFlags.Static | BindingFlags.NonPublic;
        private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private static IDictionary Registry => (IDictionary)typeof(ProcessManager)
            .GetField("s_systemDictionary", AnyStatic).GetValue(null);
        private static IDictionary ActionLookup => (IDictionary)typeof(ProcessManager)
            .GetField("s_systemActionLookup", AnyStatic).GetValue(null);

        // LeaderboardManager..ctor 0x0600061a, x86-64 0x53a920: the field
        // initializers call GetSystemRef<GameCenter> 0x53a99f (+0x10) and then
        // GetSystemRef<GameCenterLeaderboard> 0x53a9c5 (+0x18) before
        // Object..ctor 0x53a9df and RegisterSystem 0x53a9f0. Fresh registry rows
        // are appended in creation order once the dictionary has no free slots,
        // so swapping the two initializers, or moving either lookup into the
        // body after registration, fails here. The offline body stops at
        // LocalPersonalRecords.RequireReady and rolls its own registration back.
        [Test]
        public void InitializersCreateGameCenterThenLeaderboardRowsBeforeTheManagerRow()
        {
#if PROJECT_LUCID_ORIGINAL_GAMECENTER
            throw new InvalidOperationException("Owned offline case forbids original GameCenter.");
#endif
            Assert.That(LocalPersonalRecords.IsReady, Is.False, "The offline body must stop at its readiness check.");
            string[] expected =
            {
                ProcessManager.GetDefaultName<GameCenter>(),
                ProcessManager.GetDefaultName<GameCenterLeaderboard>(),
                ProcessManager.GetDefaultName<LeaderboardManager>()
            };
            IDictionary registry = Registry;
            var borrowed = new List<KeyValuePair<string, object>>();
            var pads = new List<string>();
            bool constructed = false;
            try
            {
                // An earlier row cannot show creation order. Rows with a live
                // system are foreign ownership and are never detached.
                foreach (string name in expected)
                {
                    if (!registry.Contains(name)) continue;
                    object row = registry[name];
                    Assert.That(RowReference(row).IsNull(), Is.True, "Foreign live system in row " + name);
                    borrowed.Add(new KeyValuePair<string, object>(name, row));
                    registry.Remove(name);
                }
                // Earlier removals leave reusable slots; fill them so new rows append.
                while (FreeCount(registry) > 0)
                {
                    string pad = "Lucid owned registry pad " + Guid.NewGuid().ToString("N");
                    pads.Add(pad);
                    ProcessManager.GetSystemRef(pad);
                }
                int before = registry.Count;
                constructed = true;
                var fault = Assert.Throws<InvalidOperationException>(() => new LeaderboardManager());
                Assert.That(fault.Message, Is.EqualTo("Local records are not ready."));
                List<string> created = registry.Keys.Cast<string>().Skip(before).ToList();
                Assert.That(created, Is.EqualTo(expected));
                Assert.That(ProcessManager.IsSystemNull<LeaderboardManager>(), Is.True, "Rollback leaves only the empty row.");
            }
            finally
            {
                if (constructed)
                    foreach (string name in expected)
                        if (registry.Contains(name))
                        {
                            Assert.That(RowReference(registry[name]).IsNull(), Is.True, "Owned row gained a system: " + name);
                            registry.Remove(name);
                        }
                foreach (string pad in pads) registry.Remove(pad);
                foreach (KeyValuePair<string, object> row in borrowed) registry[row.Key] = row.Value;
            }
        }

        // GameCenterLeaderboard.OnAppShutdown 0x0600055e, x86-64 0x1acf8a0:
        // after the validity test the listener's interface Clear 0x1acf95c runs,
        // then only a MonoBehaviour listener is destroyed 0x1acf9c1. There is no
        // Deinitialise call and no UnsubscribeFromAction. Adding either, or
        // skipping Clear, fails here. The macOS listener is a plain object, so
        // Clear's placement before the destroy branch is not observable.
        [Test]
        public void AppShutdownClearsTheListenerWithoutDeinitialisingOrUnsubscribing()
        {
            FieldInfo stringMap = typeof(GameCenterLeaderboardListenerMacOS).GetField("s_stringCallbackMap", AnyStatic);
            FieldInfo plainMap = typeof(GameCenterLeaderboardListenerMacOS).GetField("s_callbackMap", AnyStatic);
            FieldInfo holder = typeof(GameCenterLeaderboardListenerMacOS).GetField("m_gameCenterLeaderboardReferencesHolder", AnyInstance);
            object priorStringMap = stringMap.GetValue(null), priorPlainMap = plainMap.GetValue(null);
            IDictionary actions = ActionLookup;
            bool hadShutdownRow = actions.Contains(SystemAction.AppShutdown);
            var native = new RecordingNativeLeaderboard();
            GameCenterLeaderboard service = null;
            try
            {
                // A debug native with forceStub false selects the real macOS listener.
                service = new GameCenterLeaderboard(null, native, false);
                var listener = (GameCenterLeaderboardListenerMacOS)((IGameCenterLeaderboardReferencesHolder)service).GameCenterLeaderboardListener;
                Assert.That(holder.GetValue(listener), Is.SameAs(service));
                Assert.That(native.Calls, Is.EqualTo(new[] { "SetLeaderboardHolder", "SetLocalPlayerHolder", "SetCallbackHandler" }));
                native.Calls.Clear();

                ProcessManager.ProcessSystemAction(service, SystemAction.AppShutdown);
                Assert.That(holder.GetValue(listener), Is.Null, "Clear released the listener's references.");
                Assert.That(native.Calls, Is.Empty, "AppShutdown does not deinitialise the native leaderboard.");
                Assert.That(((IDictionary)actions[SystemAction.AppShutdown]).Contains(service), Is.True,
                    "The AppShutdown subscription is retained.");
                Assert.That(((IGameCenterLeaderboardReferencesHolder)service).AreGameCenterLeaderboardReferencesValid, Is.True);
            }
            finally
            {
                if (service != null) ProcessManager.UnsubscribeFromAllActions(service);
                if (!hadShutdownRow && actions.Contains(SystemAction.AppShutdown) &&
                    ((IDictionary)actions[SystemAction.AppShutdown]).Count == 0)
                    actions.Remove(SystemAction.AppShutdown);
                stringMap.SetValue(null, priorStringMap);
                plainMap.SetValue(null, priorPlainMap);
            }
        }

        // Leaderboard.LoadPreviousOccurrence 0x06000584 (MoveNext x86-64
        // 0x1ad45b0) and LoadImage 0x06000585 (MoveNext 0x1ad3e60): the
        // validity call (0x1ad4728 / 0x1ad3fc8) branches straight to the null-
        // checked callback (0x1ad485a / 0x1ad4099) with false and a default
        // value, then ends. No listener, native or hash is read. Requesting the
        // native anyway, subscribing first, or reporting the white texture
        // fallback for an invalid holder fails here.
        [Test]
        public void InvalidHolderCompletesBothRequestsFalseOnTheFirstStepWithoutListeners()
        {
            var holder = new InvalidReferencesHolder();
            var data = new LeaderboardInitialisationData { BaseLeaderboardID = "lucid-owned-board", Title = "Owned" };
            var board = new Leaderboard(in data, holder);
            var calls = new List<string>();

            IEnumerator previous = board.LoadPreviousOccurrence((success, record) => calls.Add("previous " + success + " " + record.HasValue));
            IEnumerator image = board.LoadImage((success, texture) => calls.Add("image " + success + " " + ReferenceEquals(texture, null)));
            Assert.That(calls, Is.Empty, "Both requests are lazy iterators.");
            Assert.That(holder.ValidityReads, Is.Zero);

            Assert.That(previous.MoveNext(), Is.False);
            Assert.That(calls, Is.EqualTo(new[] { "previous False False" }));
            Assert.That(image.MoveNext(), Is.False);
            Assert.That(calls, Is.EqualTo(new[] { "previous False False", "image False True" }));
            Assert.That(holder.ValidityReads, Is.EqualTo(2));

            Assert.That(board.LoadPreviousOccurrence(null).MoveNext(), Is.False, "A missing callback is skipped.");
            Assert.That(board.LoadImage(null).MoveNext(), Is.False);
            Assert.That(holder.ValidityReads, Is.EqualTo(4));
        }

        private static SystemRef RowReference(object row) =>
            (SystemRef)row.GetType().GetField("SystemRef", AnyInstance).GetValue(row);

        private static int FreeCount(object dictionary)
        {
            FieldInfo field = dictionary.GetType().GetField("_freeCount", AnyInstance) ??
                dictionary.GetType().GetField("freeCount", AnyInstance);
            Assert.That(field, Is.Not.Null, "The runtime dictionary must expose its free-slot count.");
            return (int)field.GetValue(dictionary);
        }

        private sealed class InvalidReferencesHolder : IGameCenterLeaderboardReferencesHolder
        {
            public int ValidityReads;
            public bool IsStub => throw new AssertionException("An invalid request read IsStub.");
            public INativeGameCenterLeaderboard NativeGameCenterLeaderboard =>
                throw new AssertionException("An invalid request read the native leaderboard.");
            public IGameCenterLeaderboardListener GameCenterLeaderboardListener =>
                throw new AssertionException("An invalid request read the listener.");
            public bool AreGameCenterLeaderboardReferencesValid { get { ++ValidityReads; return false; } }
        }

        // Owned debug native: records every call and performs none.
        private sealed class RecordingNativeLeaderboard : IDebugGameCenterLeaderboard
        {
            public readonly List<string> Calls = new List<string>();
            public void SetGameCenterLeaderboardReferencesHolder(IGameCenterLeaderboardReferencesHolder holder) => Calls.Add("SetLeaderboardHolder");
            public void SetGameCenterLocalPlayerReferencesHolder(IGameCenterLocalPlayerReferencesHolder holder) => Calls.Add("SetLocalPlayerHolder");
            public void SetGameCenterLeaderboardListenerCallbackHandler(IGameCenterLeaderboardListenerCallbackHandler handler) => Calls.Add("SetCallbackHandler");
            public bool IsLegacyAPI() { Calls.Add("IsLegacyAPI"); return false; }
            public bool IsAvailable() { Calls.Add("IsAvailable"); return false; }
            public void Initialise(string gameObjectName, string separator) => Calls.Add("Initialise");
            public void Deinitialise() => Calls.Add("Deinitialise");
            public void LoadLeaderboards(int hashCode, IReadOnlyList<string> leaderboardIDs) => Calls.Add("LoadLeaderboards");
            public void SubmitScore(int hashCode, long score, ulong context, string identifier, IReadOnlyList<string> leaderboardIDs) => Calls.Add("SubmitScores");
            public void LoadPreviousOccurrence(int hashCode, string properties) => Calls.Add("LoadPreviousOccurrence");
            public void SubmitScore(int hashCode, long score, ulong context, string identifier, string properties) => Calls.Add("SubmitScore");
            public void LoadImage(int hashCode, string properties) => Calls.Add("LoadImage");
            public int GetLastImageLoadedHeight() { Calls.Add("Height"); return 0; }
            public int GetLastImageLoadedWidth() { Calls.Add("Width"); return 0; }
            public void LoadEntriesForPlayerScope(int hashCode, LeaderboardPlayerScope playerScope, LeaderboardTimeScope timeScope,
                ulong rangeStartIndex, ulong rangeLength, string properties) => Calls.Add("LoadEntriesForPlayerScope");
            public long GetLastLoadEntriesForPlayerScopeTotalPlayerCount() { Calls.Add("TotalPlayerCount"); return 0; }
            public void LoadEntriesForPlayers(int hashCode, IReadOnlyList<string> identifiers, LeaderboardTimeScope timeScope,
                string properties) => Calls.Add("LoadEntriesForPlayers");
            public void IssueLeaderboardChallenge(string leaderboardId, string message, UnityHLLeaderboardChallengeIssuedCallback callback) =>
                Calls.Add("IssueLeaderboardChallenge");
        }
    }
}
