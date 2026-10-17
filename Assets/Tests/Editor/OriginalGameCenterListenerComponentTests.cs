using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Hardlight;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // Original GameCenter listener components (UnitySendMessage receiver role
    // inferred, not shown in any body), the stub save-game listener and the
    // MacOS access-point plugin. Every component lives on an owned GameObject
    // destroyed by the test; none of these types has statics.
    [TestFixture]
    public sealed class OriginalGameCenterListenerComponentTests
    {
        // Seven fields satisfy Leaderboard.TryParseProperties (count 7).
        private const string LeaderboardProperties = "lucid-board|Lucid|0|lucid-group|2026-01-01T00:00:00Z|2026-01-02T00:00:00Z|60";

        // Six fields satisfy LeaderboardEntry.TryParseProperties (count 6), so
        // parsing reaches the local player holder's native reference.
        private const string EntryProperties = "1|2026-01-01T00:00:00Z|100 pts|1|100|False";

        // Three fields satisfy SavedGame.TryParseProperties (count 3).
        private static string SavedGameProperties(string name) => name + "|lucid-device|2026-01-01T00:00:00Z";

        private readonly List<Object> m_owned = new List<Object>();

        [TearDown]
        public void DestroyOwnedObjects()
        {
            foreach (Object owned in m_owned) if (owned != null) Object.DestroyImmediate(owned);
            m_owned.Clear();
        }

        private T Owned<T>(string name) where T : Component
        {
            var owner = new GameObject(name);
            m_owned.Add(owner);
            return owner.AddComponent<T>();
        }

        // 0x060005cd x86-64 0x1ad6136/0x1ad6141 and 0x06000804 0x1ae9c36/
        // 0x1ae9c41: Component.gameObject then Object.name on every read.
        // A cached name or a stub-style null return would fail here.
        [Test]
        public void ListenerComponentsNameTheirOwningGameObjectOnEveryRead()
        {
            var leaderboard = Owned<GameCenterLeaderboardListener>("lucid-leaderboard-listener");
            var saveGame = Owned<GameCenterSaveGameListener>("lucid-save-listener");
            Assert.That(leaderboard.Name, Is.EqualTo("lucid-leaderboard-listener"));
            Assert.That(saveGame.Name, Is.EqualTo("lucid-save-listener"));
            leaderboard.gameObject.name = "lucid-renamed";
            Assert.That(leaderboard.Name, Is.EqualTo("lucid-renamed"));
            saveGame.gameObject.name = "lucid-save-renamed";
            Assert.That(saveGame.Name, Is.EqualTo("lucid-save-renamed"));
        }

        // 0x060005f4 0x1ad7a47 passes &m_hashCode to Int32.TryParse, so a failed
        // parse stores zero. 0x060005f7/0x060005fb/0x060005fd call
        // Boolean.TryParse and skip the event on failure (0x1ad7b92 etc.).
        // These three completions read only their own backing field
        // (+0x30/+0x50/+0x60); the rest are covered below.
        [Test]
        public void LeaderboardCompletionsParseBooleansAndReportTheLastParsedHash()
        {
            var listener = Owned<GameCenterLeaderboardListener>("lucid-leaderboard-listener");
            var events = new List<string>();
            listener.OnLoadLeaderboardsStarted += hash => events.Add("started:" + hash);
            listener.OnLoadLeaderboardsCompleted += (hash, success) => events.Add("loaded:" + hash + ":" + success);
            listener.OnSubmitScoreToLeaderboardCompleted += (hash, success) => events.Add("submitted:" + hash + ":" + success);
            listener.OnLoadImageCompleted += (hash, success) => events.Add("image:" + hash + ":" + success);

            listener.SetHashCode("41");
            listener.LoadLeaderboardsStarted();
            listener.LoadLeaderboardsCompleted("True");
            listener.LoadLeaderboardsCompleted("yes");
            listener.LoadImageCompleted(null);
            listener.SetHashCode("not-a-hash");
            listener.SubmitScoreToLeaderboardCompleted("false");
            listener.LoadImageCompleted(" true ");

            Assert.That(events, Is.EqualTo(new[] { "started:41", "loaded:41:True", "submitted:0:False", "image:0:True" }));
        }

        // 0x060005f0 0x1ad78b4/0x1ad78b9: a null argument or an existing holder
        // leaves the field. 0x060005f6 0x1ad7ac1 passes the holder to
        // Leaderboard.TryParseProperties, which rejects a null holder.
        // 0x060005f2 0x1ad7920/0x1ad7932 clears both holders but no subscriber.
        [Test]
        public void LeaderboardParsingUsesTheFirstNonNullHolderUntilClear()
        {
            var listener = Owned<GameCenterLeaderboardListener>("lucid-leaderboard-listener");
            var first = new OwnedLeaderboardHolder();
            var second = new OwnedLeaderboardHolder();
            var loaded = new List<IGameCenterLeaderboardReferencesHolder>();
            var previous = new List<IGameCenterLeaderboardReferencesHolder>();
            listener.OnLoadLeaderboard += (hash, leaderboard) => loaded.Add(HolderOf(leaderboard));
            listener.OnLoadPreviousOccurrence += (hash, leaderboard) => previous.Add(HolderOf(leaderboard));

            listener.LoadLeaderboard(LeaderboardProperties);
            listener.SetGameCenterLeaderboardReferencesHolder(null);
            listener.LoadLeaderboard(LeaderboardProperties);
            Assert.That(loaded, Is.Empty, "No holder means TryParseProperties fails.");

            listener.SetGameCenterLeaderboardReferencesHolder(first);
            listener.SetGameCenterLeaderboardReferencesHolder(second);
            listener.LoadLeaderboard(LeaderboardProperties);
            listener.LoadPreviousOccurrence(LeaderboardProperties);
            Assert.That(loaded, Is.EqualTo(new[] { first }));
            Assert.That(previous, Is.EqualTo(new[] { first }));

            listener.Clear();
            listener.LoadLeaderboard(LeaderboardProperties);
            Assert.That(loaded, Is.EqualTo(new[] { first }), "Clear released the holder.");
            listener.SetGameCenterLeaderboardReferencesHolder(second);
            listener.LoadLeaderboard(LeaderboardProperties);
            Assert.That(loaded, Is.EqualTo(new[] { first, second }), "Subscribers survive Clear.");
        }

        // 0x060005f8/0x060005fa/0x060005fe/0x06000601/0x06000602/0x06000605
        // each read their own event field (+0x38/+0x48/+0x68/+0x80/+0x88/+0xa0)
        // and m_hashCode (+0xb8); the completions skip the event when
        // Boolean.TryParse fails (0x1ad7c22, 0x1ad7d62, 0x1ad8282, 0x1ad84a2).
        [Test]
        public void LeaderboardRemainingCompletionsReadTheirOwnFieldsAndReportTheHash()
        {
            var listener = Owned<GameCenterLeaderboardListener>("lucid-leaderboard-listener");
            var events = new List<string>();
            listener.OnSubmitScoreToLeaderboardsCompleted += (hash, success) => events.Add("submits:" + hash + ":" + success);
            listener.OnLoadPreviousOccurrenceCompleted += (hash, success) => events.Add("previous:" + hash + ":" + success);
            listener.OnLoadEntriesForPlayerScopeStarted += hash => events.Add("scope-start:" + hash);
            listener.OnLoadEntriesForPlayerScopeCompleted += (hash, success) => events.Add("scope:" + hash + ":" + success);
            listener.OnLoadEntriesForPlayersStarted += hash => events.Add("players-start:" + hash);
            listener.OnLoadEntriesForPlayersCompleted += (hash, success) => events.Add("players:" + hash + ":" + success);

            listener.SetHashCode("12");
            listener.SubmitScoreToLeaderboardsCompleted("True");
            listener.LoadPreviousOccurrenceCompleted("false");
            listener.LoadEntriesForPlayerScopeStarted();
            listener.LoadEntriesForPlayerScopeCompleted("bad");
            listener.LoadEntriesForPlayersStarted();
            listener.LoadEntriesForPlayersCompleted("TRUE");
            listener.SubmitScoreToLeaderboardsCompleted(null);
            listener.SetHashCode("-3");
            listener.LoadEntriesForPlayerScopeCompleted("true");

            Assert.That(events, Is.EqualTo(new[]
            {
                "submits:12:True", "previous:12:False", "scope-start:12", "players-start:12",
                "players:12:True", "scope:-3:True",
            }));
        }

        // 0x060005fc x86-64 0x1ad7e74 loads the leaderboard holder (+0xa8) with
        // no null test, reads NativeGameCenterLeaderboard once, then slot 9
        // (GetLastImageLoadedHeight, 0x1ad7f5c) before slot 10 (width).
        // 0x060005f3 builds Texture2D(width, height, 53, false) into the out
        // argument before the IsNullOrEmpty and length checks.
        [Test]
        public void LeaderboardImageReadsHeightBeforeWidthAndBuildsTheTextureBeforeTheDataCheck()
        {
            var listener = Owned<GameCenterLeaderboardListener>("lucid-leaderboard-listener");
            Assert.Throws<NullReferenceException>(() => listener.LoadImage("lucid-image"), "The holder is not null-checked.");

            var native = new RecordingNativeLeaderboard { Height = 12, Width = 24 };
            var holder = new OwnedLeaderboardHolder { Native = native };
            var images = new List<Texture2D>();
            listener.OnLoadImage += (hash, texture) => { m_owned.Add(texture); images.Add(texture); };
            listener.SetGameCenterLeaderboardReferencesHolder(holder);
            listener.SetHashCode("5");

            listener.LoadImage(string.Empty);
            Assert.That(images, Is.Empty, "Empty image data raises no event.");
            Assert.That(native.Calls, Is.EqualTo(new[] { "Height", "Width" }), "The sizes are read before the data check.");

            // 24x12 ASTC 12x12 is two 16-byte blocks.
            listener.LoadImage(new string('a', 32));
            Assert.That(holder.NativeReads, Is.EqualTo(2), "One native reference read per call.");
            Assert.That(native.Calls, Is.EqualTo(new[] { "Height", "Width", "Height", "Width" }));
            Assert.That(images.Count, Is.EqualTo(1));
            Assert.That(images[0].width, Is.EqualTo(24));
            Assert.That(images[0].height, Is.EqualTo(12));
            Assert.That(images[0].format, Is.EqualTo((TextureFormat)53));
            Assert.That(images[0].mipmapCount, Is.EqualTo(1));

            MethodInfo tryGet = typeof(GameCenterLeaderboardListener).GetMethod("TryGetTexture2D", BindingFlags.NonPublic | BindingFlags.Static);
            foreach (string empty in new[] { null, string.Empty })
            {
                var arguments = new object[] { empty, 12, 24, null };
                Assert.That(tryGet.Invoke(null, arguments), Is.False);
                var built = (Texture2D)arguments[3];
                m_owned.Add(built);
                Assert.That(built, Is.Not.Null, "The texture is assigned before the data check.");
                Assert.That(built.width, Is.EqualTo(24));
                Assert.That(built.height, Is.EqualTo(12));
                Assert.That(built.format, Is.EqualTo((TextureFormat)53));
            }
        }

        // 0x060005ff/0x06000600/0x06000603/0x06000604 (0x1ad8101, 0x1ad81b1,
        // 0x1ad8321, 0x1ad83d1) pass the local player holder (+0xb0), not the
        // leaderboard holder (+0xa8), to LeaderboardEntry.TryParseProperties.
        // 0x060005f1 keeps the first nonnull holder; 0x060005f2 0x1ad7932
        // clears it.
        [Test]
        public void LeaderboardEntriesParseThroughTheFirstLocalPlayerHolderUntilClear()
        {
            var listener = Owned<GameCenterLeaderboardListener>("lucid-leaderboard-listener");
            var entryCallbacks = new Action<string>[]
            {
                listener.LoadLocalEntryForPlayerScope, listener.LoadEntryForPlayerScope,
                listener.LoadLocalEntryForPlayers, listener.LoadEntryForPlayers,
            };
            listener.SetGameCenterLeaderboardReferencesHolder(new OwnedLeaderboardHolder());
            foreach (Action<string> callback in entryCallbacks)
            {
                callback("lucid-one-field");
                Assert.Throws<NullReferenceException>(() => callback(EntryProperties),
                    callback.Method.Name + " must not parse through the leaderboard holder.");
            }

            var first = new LocalPlayerHolderProbe();
            var second = new LocalPlayerHolderProbe();
            listener.SetGameCenterLocalPlayerReferencesHolder(first);
            listener.SetGameCenterLocalPlayerReferencesHolder(second);
            listener.SetGameCenterLocalPlayerReferencesHolder(null);
            foreach (Action<string> callback in entryCallbacks)
                Assert.That(Assert.Throws<HolderReachedException>(() => callback(EntryProperties)).Holder,
                    Is.SameAs(first), callback.Method.Name);

            listener.Clear();
            Assert.Throws<NullReferenceException>(() => listener.LoadEntryForPlayers(EntryProperties), "Clear released the local player holder.");
            listener.SetGameCenterLocalPlayerReferencesHolder(second);
            Assert.That(Assert.Throws<HolderReachedException>(() => listener.LoadLocalEntryForPlayers(EntryProperties)).Holder,
                Is.SameAs(second));
        }

        // 0x06000829 0x1aeb54d: LoadGameData forwards its argument without a
        // parse, empty and null included. 0x06000823 0x1aeb0b7 parses the hash.
        // 0x06000826/28/2a/2b/2e each Boolean.TryParse and read only their own
        // field (+0x30/+0x40/+0x50/+0x58/+0x70).
        [Test]
        public void SaveGameCallbacksForwardDataUnparsedAndParseCompletions()
        {
            var listener = Owned<GameCenterSaveGameListener>("lucid-save-listener");
            var events = new List<string>();
            listener.OnFetchSavedGamesStarted += hash => events.Add("fetch-start:" + hash);
            listener.OnLoadGameData += (hash, data) => events.Add("data:" + hash + ":" + (data ?? "<null>"));
            listener.OnFetchSavedGamesCompleted += (hash, success) => events.Add("fetched:" + hash + ":" + success);
            listener.OnSaveGameDataCompleted += (hash, success) => events.Add("saved:" + hash + ":" + success);
            listener.OnLoadGameDataCompleted += (hash, success) => events.Add("loaded:" + hash + ":" + success);
            listener.OnDeleteSavedGamesWithNameCompleted += (hash, success) => events.Add("deleted:" + hash + ":" + success);
            listener.OnResolveConflictingSavedGamesStarted += hash => events.Add("resolve-start:" + hash);
            listener.OnResolveConflictingSavedGamesCompleted += (hash, success) => events.Add("resolved:" + hash + ":" + success);

            listener.SetHashCode("7");
            listener.FetchSavedGamesStarted();
            listener.LoadGameData(string.Empty);
            listener.LoadGameData(null);
            listener.FetchSavedGamesCompleted("true");
            listener.SaveGameDataCompleted("1");
            listener.LoadGameDataCompleted("False");
            listener.SetHashCode(string.Empty);
            listener.DeleteSavedGamesWithNameCompleted("TRUE");
            listener.ResolveConflictingSavedGamesStarted();
            listener.ResolveConflictingSavedGamesCompleted("maybe");

            Assert.That(events, Is.EqualTo(new[]
            {
                "fetch-start:7", "data:7:", "data:7:<null>", "fetched:7:True", "loaded:7:False",
                "deleted:0:True", "resolve-start:0",
            }));
        }

        // SavedGame.TryParseProperties 0x060008a5 x86-64 0x1aeb205 rejects
        // null/empty and 0x1aeb25b requires exactly three fields, so no saved
        // game event is raised. 0x06000830/0x06000832 (0x1aeb884/0x1aeb954)
        // invoke the parameterless conflict events without reading the hash.
        [Test]
        public void SaveGameRejectsUnparsableSavedGamesAndConflictNotificationsCarryNoHash()
        {
            var listener = Owned<GameCenterSaveGameListener>("lucid-save-listener");
            var events = new List<string>();
            listener.OnFetchSavedGame += (hash, savedGame) => events.Add("fetch");
            listener.OnSaveGameData += (hash, savedGame) => events.Add("save");
            listener.OnResolveConflictingSavedGame += (hash, savedGame) => events.Add("resolve");
            listener.OnDidModifySavedGame += savedGame => events.Add("modified");
            listener.OnHasConflictingSavedGame += savedGame => events.Add("conflict");
            listener.OnHasConflictingSavedGamesStarted += () => events.Add("conflicts-start");
            listener.OnHasConflictingSavedGamesCompleted += () => events.Add("conflicts-done");

            listener.FetchSavedGame(string.Empty);
            listener.SaveGameData(null);
            listener.ResolveConflictingSavedGame("lucid-one-field");
            listener.DidModifySavedGame(string.Empty);
            listener.HasConflictingSavedGame("lucid-one-field");
            Assert.That(events, Is.Empty);

            listener.HasConflictingSavedGamesStarted();
            listener.HasConflictingSavedGamesCompleted();
            Assert.That(events, Is.EqualTo(new[] { "conflicts-start", "conflicts-done" }));
        }

        // 0x06000825/0x06000827/0x0600082d (0x1aeb157 then 0x1aeb169) forward
        // a parsed SavedGame with m_hashCode (+0x98); 0x0600082f/0x06000831
        // (0x1aeb850, 0x1aeb910) forward it without reading the hash.
        [Test]
        public void SaveGameParsedCallbacksForwardTheSavedGameWithOrWithoutTheHash()
        {
            var listener = Owned<GameCenterSaveGameListener>("lucid-save-listener");
            var events = new List<string>();
            listener.OnFetchSavedGame += (hash, savedGame) => events.Add("fetch:" + hash + ":" + savedGame.Name);
            listener.OnSaveGameData += (hash, savedGame) => events.Add("save:" + hash + ":" + savedGame.Name);
            listener.OnResolveConflictingSavedGame += (hash, savedGame) => events.Add("resolve:" + hash + ":" + savedGame.Name);
            listener.OnDidModifySavedGame += savedGame => events.Add("modified:" + savedGame.Name);
            listener.OnHasConflictingSavedGame += savedGame => events.Add("conflict:" + savedGame.Name);

            listener.SetHashCode("21");
            listener.FetchSavedGame(SavedGameProperties("lucid-a"));
            listener.SaveGameData(SavedGameProperties("lucid-b"));
            listener.ResolveConflictingSavedGame(SavedGameProperties("lucid-c"));
            listener.DidModifySavedGame(SavedGameProperties("lucid-d"));
            listener.HasConflictingSavedGame(SavedGameProperties("lucid-e"));

            Assert.That(events, Is.EqualTo(new[]
            {
                "fetch:21:lucid-a", "save:21:lucid-b", "resolve:21:lucid-c", "modified:lucid-d", "conflict:lucid-e",
            }));
        }

        // 0x06000862 0x1aeb994 returns null. 0x06000881..0x0600088f each load
        // one backing field (+0x10..+0x80) and return when it is null.
        [Test]
        public void StubSaveGameListenerHasNoNameAndEachTriggerRaisesOnlyItsEvent()
        {
            var stub = new StubGameCenterSaveGameListener();
            Assert.That(stub.Name, Is.Null);
            RaiseEveryStubTrigger(stub);

            var events = new List<string>();
            stub.OnFetchSavedGamesStarted += hash => events.Add("FetchStarted:" + hash);
            stub.OnFetchSavedGame += (hash, savedGame) => events.Add("Fetch:" + hash);
            stub.OnFetchSavedGamesCompleted += (hash, success) => events.Add("FetchCompleted:" + hash + ":" + success);
            stub.OnSaveGameData += (hash, savedGame) => events.Add("Save:" + hash);
            stub.OnSaveGameDataCompleted += (hash, success) => events.Add("SaveCompleted:" + hash + ":" + success);
            stub.OnLoadGameData += (hash, data) => events.Add("Load:" + hash + ":" + data);
            stub.OnLoadGameDataCompleted += (hash, success) => events.Add("LoadCompleted:" + hash + ":" + success);
            stub.OnDeleteSavedGamesWithNameCompleted += (hash, success) => events.Add("DeleteCompleted:" + hash + ":" + success);
            stub.OnResolveConflictingSavedGamesStarted += hash => events.Add("ResolveStarted:" + hash);
            stub.OnResolveConflictingSavedGame += (hash, savedGame) => events.Add("Resolve:" + hash);
            stub.OnResolveConflictingSavedGamesCompleted += (hash, success) => events.Add("ResolveCompleted:" + hash + ":" + success);
            stub.OnDidModifySavedGame += savedGame => events.Add("Modified");
            stub.OnHasConflictingSavedGamesStarted += () => events.Add("ConflictsStarted");
            stub.OnHasConflictingSavedGame += savedGame => events.Add("Conflict");
            stub.OnHasConflictingSavedGamesCompleted += () => events.Add("ConflictsCompleted");
            RaiseEveryStubTrigger(stub);

            Assert.That(events, Is.EqualTo(new[]
            {
                "FetchStarted:1", "Fetch:2", "FetchCompleted:3:True", "Save:4", "SaveCompleted:5:False",
                "Load:6:lucid-data", "LoadCompleted:7:True", "DeleteCompleted:8:False", "ResolveStarted:9",
                "Resolve:10", "ResolveCompleted:11:True", "Modified", "ConflictsStarted", "Conflict",
                "ConflictsCompleted",
            }));
        }

        private static void RaiseEveryStubTrigger(StubGameCenterSaveGameListener stub)
        {
            stub.TriggerFetchSavedGamesStartedEvent(1);
            stub.TriggerFetchSavedGameEvent(2, default);
            stub.TriggerFetchSavedGamesCompletedEvent(3, true);
            stub.TriggerSaveGameDataEvent(4, default);
            stub.TriggerSaveGameDataCompletedEvent(5, false);
            stub.TriggerLoadGameDataEvent(6, "lucid-data");
            stub.TriggerLoadGameDataCompletedEvent(7, true);
            stub.TriggerDeleteSavedGamesWithNameCompletedEvent(8, false);
            stub.TriggerResolveConflictingSavedGamesStartedEvent(9);
            stub.TriggerResolveConflictingSavedGameEvent(10, default);
            stub.TriggerResolveConflictingSavedGamesCompletedEvent(11, true);
            stub.TriggerDidModifySavedGameEvent(default);
            stub.TriggerHasConflictingSavedGamesStartedEvent();
            stub.TriggerHasConflictingSavedGameEvent(default);
            stub.TriggerHasConflictingSavedGamesCompletedEvent();
        }

        // 0x06000541 x86-64 0x1acebe5 is a bare RET and 0x06000542 0x1acebf4
        // returns zero; neither reaches a _Unity_* import, so they run here.
        [Test]
        public void MacOSAccessPointFocusIsANoOpThatReportsUnfocused()
        {
            var plugin = new GameCenterAccessPointPluginMacOS();
            plugin.SetAccessPointFocused(true);
            Assert.That(plugin.IsAccessPointFocused(), Is.False);
            Assert.That(Calls(nameof(GameCenterAccessPointPluginMacOS.SetAccessPointFocused)), Is.Empty);
            Assert.That(Calls(nameof(GameCenterAccessPointPluginMacOS.IsAccessPointFocused)), Is.Empty);
        }

        // x86-64 0x1aceaf5..0x1acec07: each override jumps straight to one
        // _Unity_* symbol. The two "Size...ScreenCoordinate" overrides target
        // the imports without "Size" (0x1aceb95 -> _Unity_GetAccessPoint
        // ScreenCoordinateHeight, 0x1acebb5 -> ...Width). Read from IL so no
        // native import runs.
        [Test]
        public void MacOSAccessPointOverridesForwardToTheirOriginalImports()
        {
            var expected = new Dictionary<string, string>
            {
                { "Show", "Unity_ShowGameCenter" },
                { "ShowAchievements", "Unity_ShowAchievements" },
                { "IsAccessPointAvailable", "Unity_IsAccessPointAvailable" },
                { "ShowAccessPoint", "Unity_ShowAccessPoint" },
                { "IsAccessPointShown", "Unity_IsAccessPointShown" },
                { "HideAccessPoint", "Unity_HideAccessPoint" },
                { "GetAccessPointScreenCoordinateX", "Unity_GetAccessPointScreenCoordinateX" },
                { "GetAccessPointOriginX", "Unity_GetAccessPointOriginX" },
                { "GetAccessPointScreenCoordinateY", "Unity_GetAccessPointScreenCoordinateY" },
                { "GetAccessPointOriginY", "Unity_GetAccessPointOriginY" },
                { "GetAccessPointSizeScreenCoordinateHeight", "Unity_GetAccessPointScreenCoordinateHeight" },
                { "GetAccessPointSizeHeight", "Unity_GetAccessPointSizeHeight" },
                { "GetAccessPointSizeScreenCoordinateWidth", "Unity_GetAccessPointScreenCoordinateWidth" },
                { "GetAccessPointSizeWidth", "Unity_GetAccessPointSizeWidth" },
                { "IsPresentingGameCenter", "Unity_IsPresentingGameCenter" },
                { "SetShowHighlights", "Unity_SetShowHighlights" },
            };
            foreach (KeyValuePair<string, string> pair in expected)
            {
                MethodBase[] calls = Calls(pair.Key);
                Assert.That(calls.Select(call => call.Name), Is.EqualTo(new[] { pair.Value }), pair.Key);
            }
        }

        // Emission-only check: no native source gives the import library. The
        // "__Internal" string is inferred (see the plugin header); this pins
        // the emitted declaration so any change is reviewed.
        [Test]
        public void MacOSAccessPointImportLibraryIsTheInferredInternalEmissionCheck()
        {
            string[] overrides =
            {
                "Show", "ShowAchievements", "IsAccessPointAvailable", "ShowAccessPoint", "IsAccessPointShown",
                "HideAccessPoint", "GetAccessPointScreenCoordinateX", "GetAccessPointOriginX",
                "GetAccessPointScreenCoordinateY", "GetAccessPointOriginY", "GetAccessPointSizeScreenCoordinateHeight",
                "GetAccessPointSizeHeight", "GetAccessPointSizeScreenCoordinateWidth", "GetAccessPointSizeWidth",
                "IsPresentingGameCenter", "SetShowHighlights",
            };
            foreach (string name in overrides)
            {
                MethodBase import = Calls(name).Single();
                Assert.That((import.Attributes & MethodAttributes.PinvokeImpl) != 0, Is.True, import.Name);
                CustomAttributeData dll = import.GetCustomAttributesData().Single(a => a.AttributeType == typeof(DllImportAttribute));
                Assert.That(dll.ConstructorArguments.Single().Value, Is.EqualTo("__Internal"), import.Name);
            }
        }

        private static IGameCenterLeaderboardReferencesHolder HolderOf(Leaderboard leaderboard) =>
            (IGameCenterLeaderboardReferencesHolder)typeof(Leaderboard)
                .GetField("m_gameCenterLeaderboardReferencesHolder", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(leaderboard);

        // Leaderboard parsing reads no member; LoadImage reads only the native reference.
        private sealed class OwnedLeaderboardHolder : IGameCenterLeaderboardReferencesHolder
        {
            public INativeGameCenterLeaderboard Native;
            public int NativeReads;
            public bool IsStub => true;
            public INativeGameCenterLeaderboard NativeGameCenterLeaderboard { get { ++NativeReads; return Native; } }
            public IGameCenterLeaderboardListener GameCenterLeaderboardListener => null;
            public bool AreGameCenterLeaderboardReferencesValid => false;
        }

        // Stops LeaderboardEntry parsing at its first holder read and names the holder.
        private sealed class LocalPlayerHolderProbe : IGameCenterLocalPlayerReferencesHolder
        {
            public INativeGameCenterLocalPlayer NativeGameCenterLocalPlayer => throw new HolderReachedException(this);
            public IGameCenterLocalPlayerListener GameCenterLocalPlayerListener =>
                throw new AssertionException("Entry parsing read the local player listener.");
            public bool AreGameCenterLocalPlayerReferencesValid =>
                throw new AssertionException("Entry parsing read the local player validity.");
        }

        private sealed class HolderReachedException : Exception
        {
            public readonly object Holder;
            public HolderReachedException(object holder) => Holder = holder;
        }

        // Records the order of the image size reads; every other member is unused.
        private sealed class RecordingNativeLeaderboard : INativeGameCenterLeaderboard
        {
            public readonly List<string> Calls = new List<string>();
            public int Height;
            public int Width;
            public int GetLastImageLoadedHeight() { Calls.Add("Height"); return Height; }
            public int GetLastImageLoadedWidth() { Calls.Add("Width"); return Width; }
            public bool IsLegacyAPI() => throw Unused();
            public bool IsAvailable() => throw Unused();
            public void Initialise(string gameObjectName, string separator) => throw Unused();
            public void Deinitialise() => throw Unused();
            public void LoadLeaderboards(int hashCode, IReadOnlyList<string> leaderboardIDs) => throw Unused();
            public void SubmitScore(int hashCode, long score, ulong context, string identifier, IReadOnlyList<string> leaderboardIDs) => throw Unused();
            public void LoadPreviousOccurrence(int hashCode, string properties) => throw Unused();
            public void SubmitScore(int hashCode, long score, ulong context, string identifier, string properties) => throw Unused();
            public void LoadImage(int hashCode, string properties) => throw Unused();
            public void LoadEntriesForPlayerScope(int hashCode, LeaderboardPlayerScope playerScope, LeaderboardTimeScope timeScope,
                ulong rangeStartIndex, ulong rangeLength, string properties) => throw Unused();
            public long GetLastLoadEntriesForPlayerScopeTotalPlayerCount() => throw Unused();
            public void LoadEntriesForPlayers(int hashCode, IReadOnlyList<string> identifiers, LeaderboardTimeScope timeScope,
                string properties) => throw Unused();
            public void IssueLeaderboardChallenge(string leaderboardId, string message, UnityHLLeaderboardChallengeIssuedCallback callback) =>
                throw Unused();
            private static AssertionException Unused() => new AssertionException("LoadImage read an unrelated native member.");
        }

        private const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        // Every call/callvirt/newobj target of one plugin override, in IL order.
        private static MethodBase[] Calls(string overrideName)
        {
            MethodInfo method = typeof(GameCenterAccessPointPluginMacOS).GetMethods(Declared)
                .Single(m => m.Name == overrideName && !m.IsStatic);
            return OriginalILCallSequence.Calls(method).Select(call => call.Target).ToArray();
        }
    }
}
