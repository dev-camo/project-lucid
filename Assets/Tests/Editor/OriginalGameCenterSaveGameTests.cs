using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using Hardlight;
using NUnit.Framework;

namespace ProjectLucid.Tests
{
    // Behaviour of the restored HLUnityCore save-game cluster, checked against the
    // 1.10.1 x86-64 bodies (MethodDef token, then RVA). Each case owns the
    // ProcessManager action rows it creates.
    [TestFixture]
    public sealed class OriginalGameCenterSaveGameTests
    {
        private const string Join = "Slot A|Mac Studio|2024-01-02T03:04:05+02:00";

        private Dictionary<SystemAction, Dictionary<ISystem, Action<object>>> m_actions;
        private HashSet<SystemAction> m_priorActionRows;
        private readonly List<ISystem> m_ownedSystems = new List<ISystem>();
        private CultureInfo m_priorCulture;

        [SetUp]
        public void SetUp()
        {
            m_actions = (Dictionary<SystemAction, Dictionary<ISystem, Action<object>>>)typeof(ProcessManager)
                .GetField("s_systemActionLookup", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            m_priorActionRows = new HashSet<SystemAction>(m_actions.Keys);
            m_priorCulture = CultureInfo.CurrentCulture;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (ISystem system in m_ownedSystems) ProcessManager.UnsubscribeFromAllActions(system);
            m_ownedSystems.Clear();
            foreach (SystemAction action in new List<SystemAction>(m_actions.Keys))
                if (!m_priorActionRows.Contains(action) && m_actions[action].Count == 0) m_actions.Remove(action);
            CultureInfo.CurrentCulture = m_priorCulture;
        }

        // 0x060008a5 @0x1AEB1A0: DateTime.TryParse(..., InvariantCulture, AdjustToUniversal (0x10)).
        // A None/AssumeLocal parse would keep 03:04 or a local kind instead of 01:04 UTC.
        [Test]
        public void ParsingKeepsTheWholeJoinAndAdjustsTheDateToUniversal()
        {
            Assert.That(SavedGame.TryParseProperties(Join, out SavedGame savedGame), Is.True);
            Assert.That(savedGame.Name, Is.EqualTo("Slot A"));
            Assert.That(savedGame.DeviceName, Is.EqualTo("Mac Studio"));
            Assert.That(savedGame.ModificationDate, Is.EqualTo(new DateTime(2024, 1, 2, 1, 4, 5)));
            Assert.That(savedGame.ModificationDate.Kind, Is.EqualTo(DateTimeKind.Utc));
            Assert.That(((ISavedGameIdentifier)savedGame).PropertiesJoinToIdentifySavedGame, Is.SameAs(Join));

            // StringSplitOptions.None keeps empty segments.
            Assert.That(SavedGame.TryParseProperties("||2024-01-02T03:04:05Z", out SavedGame empty), Is.True);
            Assert.That(empty.Name, Is.EqualTo(string.Empty));
            Assert.That(empty.DeviceName, Is.EqualTo(string.Empty));
        }

        // 0x060008a5 @0x1AEB1A0: the out value is cleared first; empty input, any count other than
        // three "|" fields, a foreign separator or an unparsable date all fail.
        [Test]
        public void ParsingRejectsMalformedJoinsAndClearsTheResult()
        {
            string[] joins =
            {
                null, "", "Slot A|2024-01-02T03:04:05Z", "Slot A|Mac|2024-01-02T03:04:05Z|extra",
                "Slot A,Mac,2024-01-02T03:04:05Z", "Slot A|Mac|not a date",
            };
            foreach (string join in joins)
            {
                Assert.That(SavedGame.TryParseProperties(join, out SavedGame savedGame), Is.False, join);
                Assert.That(savedGame.Name, Is.Null, join);
                Assert.That(savedGame.DeviceName, Is.Null, join);
                Assert.That(savedGame.ModificationDate, Is.EqualTo(default(DateTime)), join);
                Assert.That(((ISavedGameIdentifier)savedGame).PropertiesJoinToIdentifySavedGame, Is.Null, join);
            }
        }

        // 0x060008a8/0x060008a9/0x060008aa @0x1AED610/0x1AED820/0x1AED7F0: identity is
        // the hash of the join string alone, so equal fields written with a different
        // date spelling are not equal, and a default value faults instead of comparing.
        // Hash-only versus ordinal string comparison is not distinguished here: that
        // would need a string-hash collision pair proven stable on the player runtime.
        [Test]
        public void EqualityComparesOnlyTheJoinHashAndFaultsOnDefault()
        {
            SavedGame.TryParseProperties(Join, out SavedGame first);
            SavedGame.TryParseProperties(new string(Join.ToCharArray()), out SavedGame second);
            SavedGame.TryParseProperties("Slot A|Mac Studio|2024-01-02T01:04:05Z", out SavedGame sameFields);
            Assert.That(sameFields.Name, Is.EqualTo(first.Name));
            Assert.That(sameFields.DeviceName, Is.EqualTo(first.DeviceName));
            Assert.That(sameFields.ModificationDate, Is.EqualTo(first.ModificationDate));

            Assert.That(first == second, Is.True);
            Assert.That(first != second, Is.False);
            Assert.That(first.Equals((object)second), Is.True);
            Assert.That(first.GetHashCode(), Is.EqualTo(Join.GetHashCode()));
            Assert.That(first == sameFields, Is.False);
            Assert.That(first != sameFields, Is.True);
            Assert.That(first.Equals((object)Join), Is.False);
            Assert.That(first.Equals(null), Is.False);

            Assert.Throws<NullReferenceException>(() => default(SavedGame).GetHashCode());
            Assert.Throws<NullReferenceException>(() => first.Equals(default(SavedGame)));
            Assert.Throws<NullReferenceException>(() => { bool _ = default(SavedGame) == first; });
        }

        // 0x060008ab @0x1AED920: fixed label order; the date goes through
        // DateTimeFormatInfo.get_CurrentInfo into DateTime.ToString(IFormatProvider)
        // [0x060005e3], so the text follows the current culture, not the invariant one.
        [Test]
        public void ToStringUsesOriginalLabelsAndCurrentCultureDate()
        {
            SavedGame.TryParseProperties("Slot A|Mac Studio|2024-01-02T03:04:05Z", out SavedGame savedGame);
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            Assert.That(savedGame.ToString(), Is.EqualTo("Name:Slot A DeviceName:Mac Studio ModificationDate:01/02/2024 03:04:05"));
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            Assert.That(savedGame.ToString(), Is.EqualTo("Name:Slot A DeviceName:Mac Studio ModificationDate:02.01.2024 03:04:05"));
        }

        // 0x060008a5 @0x1AEB1A0: the provider is InvariantCulture, so a month-first
        // date stays month-first under a day-first current culture (a CurrentCulture
        // parse would give 1 February). Noon UTC keeps the day clear of zone shifts.
        [Test]
        public void ParsingUsesTheInvariantCultureNotTheCurrentOne()
        {
            CultureInfo.CurrentCulture = new CultureInfo("en-GB");
            Assert.That(SavedGame.TryParseProperties("Slot A|Mac|01/02/2024 12:00:00Z", out SavedGame savedGame), Is.True);
            Assert.That(savedGame.ModificationDate.Month, Is.EqualTo(1));
            Assert.That(savedGame.ModificationDate.Day, Is.EqualTo(2));
        }

        // 0x0600089a..0x060008a1 @0x1AED020..0x1AED380: synchronous success with no data events, in order.
        [Test]
        public void StubCompletesEveryRequestSuccessfullyWithoutDataEvents()
        {
            var handler = new RecordingHandler();
            var stub = new StubNativeGameCenterSaveGame(handler);
            stub.Initialise("listener", GameCenterSaveGame.Separator);
            stub.Deinitialise();
            Assert.That(handler.Calls, Is.Empty);
            Assert.That(stub.IsAvailable(), Is.True);

            stub.FetchSavedGames(11);
            stub.SaveGameData(12, "name", "data");
            stub.LoadGameData(13, Join);
            stub.DeleteSavedGamesWithName(14, "name");
            stub.ResolveConflictingSavedGames(15, Join, "data");
            Assert.That(handler.Calls, Is.EqualTo(new[]
            {
                "FetchStarted:11", "FetchCompleted:11:True",
                "SaveCompleted:12:True",
                "LoadCompleted:13:True",
                "DeleteCompleted:14:True",
                "ResolveStarted:15", "ResolveCompleted:15:True",
            }));

            // 0x06000899 @0x1AE9BF0 stores the handler unguarded.
            Assert.Throws<NullReferenceException>(() => new StubNativeGameCenterSaveGame(null).FetchSavedGames(1));
        }

        // 0x06000802/0x06000803 @0x1AE6640/0x1AE6540: forceStub selects the stub types
        // without reporting; outside forceStub the factories log the unsupported-platform
        // error (String.Format of the boxed Application.platform, then HLOutput.LogError)
        // and return null. GameCenterSaveGameListener (0x02000134) is compiled in but not
        // constructed here. The error is captured through an owned HLUnityCore.
        [Test]
        public void FactoriesReturnStubsOnlyWhenForced()
        {
            using (var log = new OwnedCoreErrorLog())
            {
                var handler = new RecordingHandler();
                INativeGameCenterSaveGame native = GameCenterSaveGameUtility.CreateNativeGameCenterSaveGame(handler, true);
                Assert.That(native, Is.InstanceOf<StubNativeGameCenterSaveGame>());
                native.DeleteSavedGamesWithName(21, "name");
                Assert.That(handler.Calls, Is.EqualTo(new[] { "DeleteCompleted:21:True" }));
                Assert.That(GameCenterSaveGameUtility.CreateGameCenterSaveGameListener(true), Is.InstanceOf<StubGameCenterSaveGameListener>());
                Assert.That(log.Errors, Is.Empty);

                Assert.That(GameCenterSaveGameUtility.CreateNativeGameCenterSaveGame(handler), Is.Null);
                Assert.That(GameCenterSaveGameUtility.CreateGameCenterSaveGameListener(), Is.Null);
                Assert.That(log.Errors, Is.EqualTo(new[]
                {
                    string.Format("[GameCenterSaveGame] CreateNativeGameCenterSaveGame - Native gameCenter save game is not supporter on platform '{0}'.:0",
                        UnityEngine.Application.platform),
                    string.Format("[GameCenterSaveGame] CreateGameCenterSaveGameListener - GameCenter save game listener is not supporter on platform '{0}'.:0",
                        UnityEngine.Application.platform),
                }));
            }
        }

        // 0x060007bb @0x1AE6350 subscribes AppInitialise (3) and AppShutdown (4) only.
        // Without native support every guarded entry point is inert, and each iterator
        // reports failure on its first step, not when it is created
        // (0x060007c6..0x060007fe @0x1AE7230..0x1AE9640).
        // HLOutput.LogError only reaches a registered HLUnityCore, so construction's
        // two factory errors go to an owned core and are asserted in 0x060007bb's
        // order (listener factory, then native). That core forwards to its handler
        // only, so no Unity error log is expected; nothing else in the service logs.
        [Test]
        public void UnsupportedServiceSubscribesAppActionsAndFailsEachRequestLazily()
        {
            using (var log = new OwnedCoreErrorLog())
                UnsupportedServiceBody(log);
        }

        private void UnsupportedServiceBody(OwnedCoreErrorLog log)
        {
            var saveGame = new GameCenterSaveGame();
            Assert.That(log.Errors, Is.EqualTo(new[]
            {
                string.Format("[GameCenterSaveGame] CreateGameCenterSaveGameListener - GameCenter save game listener is not supporter on platform '{0}'.:0",
                    UnityEngine.Application.platform),
                string.Format("[GameCenterSaveGame] CreateNativeGameCenterSaveGame - Native gameCenter save game is not supporter on platform '{0}'.:0",
                    UnityEngine.Application.platform),
            }));
            m_ownedSystems.Add(saveGame);
            foreach (SystemAction action in Enum.GetValues(typeof(SystemAction)))
            {
                bool expected = action == SystemAction.AppInitialise || action == SystemAction.AppShutdown;
                bool subscribed = m_actions.TryGetValue(action, out var row) && row.ContainsKey(saveGame);
                Assert.That(subscribed, Is.EqualTo(expected), action.ToString());
            }
            m_actions[SystemAction.AppInitialise][saveGame](null);
            m_actions[SystemAction.AppShutdown][saveGame](null);

            Assert.That(saveGame.IsAvailable(), Is.False);
            saveGame.Initialise();
            saveGame.Deinitialise();

            var results = new List<string>();
            IEnumerator fetch = saveGame.FetchSavedGames((success, games) => results.Add("fetch:" + success + ":" + (games == null)));
            IEnumerator save = saveGame.SaveGameData("name", "data", (success, game) => results.Add("save:" + success + ":" + (game.Name == null)));
            IEnumerator load = saveGame.LoadGameData(default, (success, data) => results.Add("load:" + success + ":" + (data == null)));
            IEnumerator delete = saveGame.DeleteSavedGame("name", success => results.Add("delete:" + success));
            IEnumerator resolve = saveGame.ResolveConflictingSavedGames("data", (success, games) => results.Add("resolve:" + success + ":" + (games == null)));
            Assert.That(results, Is.Empty);

            Assert.That(fetch.MoveNext(), Is.False);
            Assert.That(save.MoveNext(), Is.False);
            Assert.That(load.MoveNext(), Is.False);
            Assert.That(delete.MoveNext(), Is.False);
            Assert.That(results, Is.EqualTo(new[] { "fetch:False:True", "save:False:True", "load:False:True", "delete:False" }));

            // 0x060007f2 @0x1AE8DD0: the params overload yields the list overload's enumerator once.
            Assert.That(resolve.MoveNext(), Is.True);
            Assert.That(resolve.Current, Is.InstanceOf<IEnumerator>());
            Assert.That(results.Count, Is.EqualTo(4));
            Assert.That(((IEnumerator)resolve.Current).MoveNext(), Is.False);
            Assert.That(results[4], Is.EqualTo("resolve:False:True"));
            Assert.That(resolve.MoveNext(), Is.False);
            Assert.That(log.Errors.Count, Is.EqualTo(2), "The unsupported requests and app actions report nothing further.");
        }

        // 0x060007bc/0x060007bd @0x1AE6760/0x1AE6A80 and 0x060007bf..0x060007c2: the four
        // listener events are wired only by AppInitialise and removed by AppShutdown.
        // Conflicts accumulate in one live list, which Started clears and Completed
        // publishes as the same instance every time.
        [Test]
        public void StubServiceWiresListenerEventsOnlyBetweenAppInitialiseAndShutdown()
        {
            GameCenterSaveGame saveGame = CreateStubService(out StubGameCenterSaveGameListener listener);
            Assert.That(saveGame.IsAvailable(), Is.True);
            saveGame.Initialise();
            saveGame.Deinitialise();

            var modified = new List<string>();
            var published = new List<IReadOnlyList<SavedGame>>();
            saveGame.OnDidModifySavedGame += savedGame => modified.Add(Id(savedGame));
            saveGame.OnHasConflictingSavedGames += savedGames => published.Add(savedGames);
            SavedGame a = Parse("A|Mac|2024-01-02T03:04:05Z");
            SavedGame b = Parse("B|Mac|2024-01-02T03:04:05Z");
            SavedGame c = Parse("C|Mac|2024-01-02T03:04:05Z");

            // Before AppInitialise nothing is forwarded.
            listener.TriggerDidModifySavedGameEvent(a);
            listener.TriggerHasConflictingSavedGameEvent(a);
            listener.TriggerHasConflictingSavedGamesCompletedEvent();
            Assert.That(modified, Is.Empty);
            Assert.That(published, Is.Empty);

            m_actions[SystemAction.AppInitialise][saveGame](null);
            listener.TriggerDidModifySavedGameEvent(a);
            Assert.That(modified, Is.EqualTo(new[] { Id(a) }));

            listener.TriggerHasConflictingSavedGamesStartedEvent();
            listener.TriggerHasConflictingSavedGameEvent(a);
            listener.TriggerHasConflictingSavedGameEvent(b);
            listener.TriggerHasConflictingSavedGamesCompletedEvent();
            Assert.That(published.Count, Is.EqualTo(1));
            Assert.That(Ids(published[0]), Is.EqualTo(new[] { Id(a), Id(b) }));

            listener.TriggerHasConflictingSavedGamesStartedEvent();
            listener.TriggerHasConflictingSavedGameEvent(c);
            listener.TriggerHasConflictingSavedGamesCompletedEvent();
            Assert.That(published.Count, Is.EqualTo(2));
            Assert.That(published[1], Is.SameAs(published[0]));
            Assert.That(Ids(published[0]), Is.EqualTo(new[] { Id(c) }));

            // After AppShutdown the listener no longer reaches the service.
            m_actions[SystemAction.AppShutdown][saveGame](null);
            listener.TriggerDidModifySavedGameEvent(b);
            listener.TriggerHasConflictingSavedGamesStartedEvent();
            listener.TriggerHasConflictingSavedGameEvent(a);
            listener.TriggerHasConflictingSavedGamesCompletedEvent();
            Assert.That(modified, Is.EqualTo(new[] { Id(a) }));
            Assert.That(published.Count, Is.EqualTo(2));
            Assert.That(Ids(published[0]), Is.EqualTo(new[] { Id(c) }));
        }

        // 0x060007e6 @0x1AE8060 with closure 0x060007cf..0x060007d1: only events carrying
        // the request hash are kept, a foreign completion after the real one does not
        // change the result, the stub's synchronous completion ends the iterator with no
        // yield, handlers are removed afterwards, and the caller receives the shared
        // fetched list, which the next matching Started clears.
        [Test]
        public void FetchKeepsOnlyMatchingEventsInTheSharedListAndUnsubscribes()
        {
            GameCenterSaveGame saveGame = CreateStubService(out StubGameCenterSaveGameListener listener);
            SavedGame a = Parse("A|Mac|2024-01-02T03:04:05Z");
            SavedGame b = Parse("B|Mac|2024-01-02T03:04:05Z");
            SavedGame c = Parse("C|Mac|2024-01-02T03:04:05Z");
            int hash = 0;
            bool emit = true;
            bool foreignStarted = false;
            listener.OnFetchSavedGamesStarted += started =>
            {
                if (foreignStarted) return;
                hash = started;
                int own = started;
                // Subscribed after the iterator's own handler, so it runs after it.
                listener.OnFetchSavedGamesCompleted += (completed, success) =>
                {
                    if (completed == own) listener.TriggerFetchSavedGamesCompletedEvent(own + 1, false);
                };
            };
            listener.OnFetchSavedGamesCompleted += (completed, success) =>
            {
                if (completed != hash || !emit) return;
                emit = false;
                listener.TriggerFetchSavedGameEvent(hash, a);
                listener.TriggerFetchSavedGameEvent(hash + 1, b);
                listener.TriggerFetchSavedGameEvent(hash, c);
                // 0x060007cf clears only for the request hash; a foreign Started
                // before completion must keep a and c.
                foreignStarted = true;
                listener.TriggerFetchSavedGamesStartedEvent(hash + 1);
                foreignStarted = false;
            };

            var results = new List<(bool success, IReadOnlyList<SavedGame> savedGames)>();
            Assert.That(saveGame.FetchSavedGames((success, savedGames) => results.Add((success, savedGames))).MoveNext(), Is.False);
            Assert.That(results.Count, Is.EqualTo(1));
            Assert.That(results[0].success, Is.True);
            Assert.That(Ids(results[0].savedGames), Is.EqualTo(new[] { Id(a), Id(c) }));

            int firstHash = hash;
            listener.TriggerFetchSavedGameEvent(firstHash, b);
            Assert.That(Ids(results[0].savedGames), Is.EqualTo(new[] { Id(a), Id(c) }));

            Assert.That(saveGame.FetchSavedGames((success, savedGames) => results.Add((success, savedGames))).MoveNext(), Is.False);
            Assert.That(results.Count, Is.EqualTo(2));
            Assert.That(results[1].success, Is.True);
            Assert.That(results[1].savedGames, Is.SameAs(results[0].savedGames));
            Assert.That(results[1].savedGames, Is.Empty);
        }

        // 0x060007fe/0x060007ec/0x060007e0 @0x1AE9640/0x1AE8780/0x1AE7C40: save keeps the
        // last matching save event and reports a default SavedGame without one; load
        // keeps the matching data only; delete completes on the stub's own completion.
        [Test]
        public void SaveLoadAndDeleteKeepOnlyMatchingEvents()
        {
            GameCenterSaveGame saveGame = CreateStubService(out StubGameCenterSaveGameListener listener);
            SavedGame a = Parse("A|Mac|2024-01-02T03:04:05Z");
            SavedGame b = Parse("B|Mac|2024-01-02T03:04:05Z");
            SavedGame c = Parse("C|Mac|2024-01-02T03:04:05Z");
            bool emit = true;
            listener.OnSaveGameDataCompleted += (hash, success) =>
            {
                if (!emit) return;
                emit = false;
                listener.TriggerSaveGameDataEvent(hash, a);
                listener.TriggerSaveGameDataEvent(hash, c);
                listener.TriggerSaveGameDataEvent(hash + 1, b);
            };
            var saved = new List<(bool success, SavedGame savedGame)>();
            Assert.That(saveGame.SaveGameData("name", "data", (success, savedGame) => saved.Add((success, savedGame))).MoveNext(), Is.False);
            Assert.That(saveGame.SaveGameData("name", "data", (success, savedGame) => saved.Add((success, savedGame))).MoveNext(), Is.False);
            Assert.That(saved.Count, Is.EqualTo(2));
            Assert.That(saved[0].success, Is.True);
            Assert.That(Id(saved[0].savedGame), Is.EqualTo(Id(c)));
            Assert.That(saved[1].success, Is.True);
            Assert.That(saved[1].savedGame.Name, Is.Null);
            Assert.That(Id(saved[1].savedGame), Is.Null);

            emit = true;
            listener.OnLoadGameDataCompleted += (hash, success) =>
            {
                if (!emit) return;
                emit = false;
                listener.TriggerLoadGameDataEvent(hash, "matching");
                listener.TriggerLoadGameDataEvent(hash + 1, "foreign");
            };
            var loaded = new List<string>();
            Assert.That(saveGame.LoadGameData(a, (success, data) => loaded.Add(success + ":" + data)).MoveNext(), Is.False);
            Assert.That(saveGame.LoadGameData(a, (success, data) => loaded.Add(success + ":" + (data == null))).MoveNext(), Is.False);
            Assert.That(loaded, Is.EqualTo(new[] { "True:matching", "True:True" }));

            var deleted = new List<bool>();
            Assert.That(saveGame.DeleteSavedGame("name", deleted.Add).MoveNext(), Is.False);
            Assert.That(deleted, Is.EqualTo(new[] { true }));
        }

        // 0x060007f8 @0x1AE8EB0 with closure 0x060007db..0x060007dd: the same filtering as
        // fetch, but into the separate shared resolve list, which the caller receives and
        // the next matching Started clears; the params overload drives it the same way.
        [Test]
        public void ResolveKeepsOnlyMatchingEventsInTheSharedResolveList()
        {
            GameCenterSaveGame saveGame = CreateStubService(out StubGameCenterSaveGameListener listener);
            SavedGame a = Parse("A|Mac|2024-01-02T03:04:05Z");
            SavedGame b = Parse("B|Mac|2024-01-02T03:04:05Z");
            SavedGame c = Parse("C|Mac|2024-01-02T03:04:05Z");
            int hash = 0;
            bool emit = true;
            bool foreignStarted = false;
            listener.OnResolveConflictingSavedGamesStarted += started =>
            {
                if (foreignStarted) return;
                hash = started;
                int own = started;
                listener.OnResolveConflictingSavedGamesCompleted += (completed, success) =>
                {
                    if (completed == own) listener.TriggerResolveConflictingSavedGamesCompletedEvent(own + 1, false);
                };
            };
            listener.OnResolveConflictingSavedGamesCompleted += (completed, success) =>
            {
                if (completed != hash || !emit) return;
                emit = false;
                listener.TriggerResolveConflictingSavedGameEvent(hash, b);
                listener.TriggerResolveConflictingSavedGameEvent(hash + 1, a);
                listener.TriggerResolveConflictingSavedGameEvent(hash, c);
                // 0x060007db clears only for the request hash; a foreign Started
                // before completion must keep b and c.
                foreignStarted = true;
                listener.TriggerResolveConflictingSavedGamesStartedEvent(hash + 1);
                foreignStarted = false;
            };

            var results = new List<(bool success, IReadOnlyList<SavedGame> savedGames)>();
            IReadOnlyList<SavedGame> conflicts = new[] { a, b };
            Assert.That(saveGame.ResolveConflictingSavedGames("data", (success, savedGames) => results.Add((success, savedGames)), conflicts).MoveNext(), Is.False);
            Assert.That(results.Count, Is.EqualTo(1));
            Assert.That(results[0].success, Is.True);
            Assert.That(Ids(results[0].savedGames), Is.EqualTo(new[] { Id(b), Id(c) }));
            listener.TriggerResolveConflictingSavedGameEvent(hash, a);
            Assert.That(Ids(results[0].savedGames), Is.EqualTo(new[] { Id(b), Id(c) }));

            IReadOnlyList<SavedGame> fetched = null;
            saveGame.FetchSavedGames((success, savedGames) => fetched = savedGames).MoveNext();
            Assert.That(fetched, Is.Not.SameAs(results[0].savedGames));

            IEnumerator resolve = saveGame.ResolveConflictingSavedGames("data", (success, savedGames) => results.Add((success, savedGames)), a, b);
            Assert.That(resolve.MoveNext(), Is.True);
            Assert.That(((IEnumerator)resolve.Current).MoveNext(), Is.False);
            Assert.That(resolve.MoveNext(), Is.False);
            Assert.That(results.Count, Is.EqualTo(2));
            Assert.That(results[1].success, Is.True);
            Assert.That(results[1].savedGames, Is.SameAs(results[0].savedGames));
            Assert.That(results[1].savedGames, Is.Empty);
        }

        private GameCenterSaveGame CreateStubService(out StubGameCenterSaveGameListener listener)
        {
            var saveGame = new GameCenterSaveGame(true);
            m_ownedSystems.Add(saveGame);
            listener = (StubGameCenterSaveGameListener)typeof(GameCenterSaveGame)
                .GetField("m_gameCenterSaveGameListener", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(saveGame);
            return saveGame;
        }

        private static SavedGame Parse(string join)
        {
            Assert.That(SavedGame.TryParseProperties(join, out SavedGame savedGame), Is.True, join);
            return savedGame;
        }

        private static string Id(SavedGame savedGame) => ((ISavedGameIdentifier)savedGame).PropertiesJoinToIdentifySavedGame;

        private static List<string> Ids(IReadOnlyList<SavedGame> savedGames)
        {
            var ids = new List<string>();
            foreach (SavedGame savedGame in savedGames) ids.Add(Id(savedGame));
            return ids;
        }

        private sealed class RecordingHandler : IGameCenterSaveGameListenerCallbacksHandler
        {
            public readonly List<string> Calls = new List<string>();
            public void TriggerFetchSavedGamesStartedEvent(int hashCode) => Calls.Add("FetchStarted:" + hashCode);
            public void TriggerFetchSavedGameEvent(int hashCode, SavedGame savedGame) => Calls.Add("Fetch:" + hashCode);
            public void TriggerFetchSavedGamesCompletedEvent(int hashCode, bool success) => Calls.Add("FetchCompleted:" + hashCode + ":" + success);
            public void TriggerSaveGameDataEvent(int hashCode, SavedGame savedGame) => Calls.Add("Save:" + hashCode);
            public void TriggerSaveGameDataCompletedEvent(int hashCode, bool success) => Calls.Add("SaveCompleted:" + hashCode + ":" + success);
            public void TriggerLoadGameDataEvent(int hashCode, string saveGameData) => Calls.Add("Load:" + hashCode);
            public void TriggerLoadGameDataCompletedEvent(int hashCode, bool success) => Calls.Add("LoadCompleted:" + hashCode + ":" + success);
            public void TriggerDeleteSavedGamesWithNameCompletedEvent(int hashCode, bool success) => Calls.Add("DeleteCompleted:" + hashCode + ":" + success);
            public void TriggerResolveConflictingSavedGamesStartedEvent(int hashCode) => Calls.Add("ResolveStarted:" + hashCode);
            public void TriggerResolveConflictingSavedGameEvent(int hashCode, SavedGame savedGame) => Calls.Add("Resolve:" + hashCode);
            public void TriggerResolveConflictingSavedGamesCompletedEvent(int hashCode, bool success) => Calls.Add("ResolveCompleted:" + hashCode + ":" + success);
            public void TriggerDidModifySavedGameEvent(SavedGame savedGame) => Calls.Add("DidModify");
            public void TriggerHasConflictingSavedGamesStartedEvent() => Calls.Add("ConflictsStarted");
            public void TriggerHasConflictingSavedGameEvent(SavedGame savedGame) => Calls.Add("Conflict");
            public void TriggerHasConflictingSavedGamesCompletedEvent() => Calls.Add("ConflictsCompleted");
        }
    }
}
