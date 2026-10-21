using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Hardlight;
using Hardlight.Utils;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.TestTools;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // Restored Game.Runtime PersistentLevelObjectType`1 (0x02000a18, read from the <bool>
    // instantiation 0x18CAAE0..0x18CB110) and PersistentLevelObjectBoolean (0x02000a17,
    // 0x6B3080..0x6B3160). Each component's constructor caches the MissionManager reference, so
    // that row is leased first; Awake and the level callbacks are called directly. The first
    // cases leave the MissionManager row empty; WithMissions registers a real one, with a
    // SaveManager holding an open save and a refusing coroutine host.
    public sealed class OriginalPersistentLevelObjectTests
    {
        private const PersistentObjectIdentifierType Identifier = PersistentObjectIdentifierType.FTUE_GateKey_1;

        private static IDictionary Registry => (IDictionary)typeof(ProcessManager)
            .GetField("s_systemDictionary", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);

        // The base event logs "event", or "event:" plus what state() reads at that moment.
        private static PersistentLevelObjectBoolean Persistent(Owned owned, List<string> log, Func<string> state = null)
        {
            var persistent = owned.Host("Lucid persistent object", false).AddComponent<PersistentLevelObjectBoolean>();
            persistent.OnStateChangedEvent += () => log.Add(state == null ? "event" : "event:" + state());
            var onTrue = new UnityEvent();
            onTrue.AddListener(() => log.Add("true"));
            var onFalse = new UnityEvent();
            onFalse.AddListener(() => log.Add("false"));
            Set(persistent, "m_onStateTrue", onTrue);
            Set(persistent, "m_onStateFalse", onFalse);
            return persistent;
        }

        private static bool Current(PersistentLevelObjectBoolean persistent) => Get<bool>(persistent, "m_currentValue");

        // An unawakened LevelManager; its three constructor rows are leased with MissionManager's.
        private static LevelManager Level(Owned owned, bool currentLevelValid)
        {
            owned.LeaseRow<AudioManager>();
            owned.LeaseRow<SaveManager>();
            var levelManager = owned.Host("Lucid level manager", false).AddComponent<LevelManager>();
            typeof(LevelManager).GetProperty("CurrentLevelValid").SetValue(levelManager, currentLevelValid);
            return owned.Register(levelManager);
        }

        private static Delegate[] LevelActivated(LevelManager levelManager) =>
            Get<Action<LevelManagerLevel>>(levelManager, "m_actionOnLevelActivated")?.GetInvocationList() ?? new Delegate[0];

        // TypeDefs 0x02000a18 (abstract, one unconstrained T) and 0x02000a17 in metadata order.
        // The <bool> constructor 0x18CB110 stores GetSystemRef<MissionManager>(null, true) and a
        // new FastAction; the Boolean constructor 0x6B3160 first stores true at +0x60.
        [Test]
        public void DeclarationsAndConstructorDefaults()
        {
            Type generic = typeof(PersistentLevelObjectType<>);
            AssertMembers(generic, 0x00100081,
                new[] { ("m_id", 0x0001), ("m_defaultValue", 0x0001), ("m_saveData", 0x0001), ("m_currentValue", 0x0004),
                        ("m_levelManager", 0x0001), ("m_missionManagerRef", 0x0021), ("OnStateChangedEvent", 0x0006) },
                new[] { ("Awake", 0x0081), ("OnDestroy", 0x0081), ("OnLevelManagerValid", 0x0081), ("UpdateCurrentValueFromSave", 0x0081),
                        ("OnLevelActivated", 0x0081), ("Action_SetPersistentState", 0x0086), ("OnStateChanged", 0x01c4),
                        ("ChallengeModeActive", 0x0081), ("GetPersistentObjectFromSaveDataLevel", 0x05c4), ("GetSavedValue", 0x05c4),
                        ("SetSavedValue", 0x05c4), ("GetCachedValue", 0x05c6), ("GetDefaultDuringChallenge", 0x05c4), (".ctor", 0x1884) });
            AssertIl2CppOptions(generic, 1, 2);
            Type parameter = generic.GetGenericArguments().Single();
            Assert.That((int)parameter.GenericParameterAttributes, Is.Zero);
            Assert.That(parameter.GetGenericParameterConstraints(), Is.Empty);
            Assert.That(generic.BaseType, Is.EqualTo(typeof(PersistentLevelObject)));

            AssertMembers(typeof(PersistentLevelObjectBoolean), 0x00100001,
                new[] { ("m_onStateTrue", 0x0001), ("m_onStateFalse", 0x0001), ("m_stateWhenChallenge", 0x0001) },
                new[] { ("GetPersistentObjectFromSaveDataLevel", 0x00c4), ("GetSavedValue", 0x00c4), ("GetCachedValue", 0x00c6),
                        ("GetDefaultDuringChallenge", 0x00c4), ("SetSavedValue", 0x00c4), ("OnStateChanged", 0x00c4), (".ctor", 0x1886) });
            AssertIl2CppOptions(typeof(PersistentLevelObjectBoolean), 2, 1);
            Assert.That(typeof(PersistentLevelObjectBoolean).BaseType, Is.EqualTo(typeof(PersistentLevelObjectType<bool>)));
            Assert.That(Tooltip(Field(typeof(PersistentLevelObjectBoolean), "m_stateWhenChallenge")), Is.EqualTo(
                "This is the state that will be set if Challenges are active, regardless of the saved state. State changes are not saved during Challenges."));

            Owned.Run(owned =>
            {
                owned.LeaseRow<MissionManager>();
                var first = owned.Host("Lucid persistent first", false).AddComponent<PersistentLevelObjectBoolean>();
                var second = owned.Host("Lucid persistent second", false).AddComponent<PersistentLevelObjectBoolean>();
                Assert.That(first.OnStateChangedEvent, Is.Not.Null.And.Not.SameAs(second.OnStateChangedEvent));
                Assert.That(Get<SystemRef<MissionManager>>(first, "m_missionManagerRef"), Is.SameAs(ProcessManager.GetSystemRef<MissionManager>()));
                Assert.That(Get<bool>(first, "m_stateWhenChallenge"), Is.True);
                Assert.That(Get<object>(first, "m_saveData"), Is.Null);
                Assert.That(Get<object>(first, "m_levelManager"), Is.Null);
            });
        }

        // Awake 0x18CAAE0 copies m_defaultValue (+0x24) to m_currentValue (+0x30). An unset id
        // (cmp +0x20, 0) calls OnStateChanged (slot 4) with it and never asks for the level
        // manager. The Boolean override 0x6B30F0 runs the base event (FastAction at +0x48) first,
        // then m_onStateTrue or m_onStateFalse; a null UnityEvent is skipped.
        [Test]
        public void AnUnsetIdAnnouncesTheDefaultWithoutTheLevelManager()
        {
            Owned.Run(owned =>
            {
                owned.LeaseRow<MissionManager>();
                owned.LeaseRow<LevelManager>();
                string levelName = ProcessManager.GetDefaultName<LevelManager>();
                bool levelRowExisted = Registry.Contains(levelName);
                foreach (bool defaultValue in new[] { true, false })
                {
                    var log = new List<string>();
                    PersistentLevelObjectBoolean persistent = Persistent(owned, log);
                    Set(persistent, "m_defaultValue", defaultValue);
                    Call(persistent, "Awake");
                    Assert.That(Current(persistent), Is.EqualTo(defaultValue));
                    Assert.That(persistent.GetCachedValue(), Is.EqualTo(defaultValue), "0x6B30B0 returns +0x30");
                    Assert.That(log, Is.EqualTo(new[] { "event", defaultValue ? "true" : "false" }));
                }
                Assert.That(Registry.Contains(levelName), Is.EqualTo(levelRowExisted), "no level manager lookup");

                var silent = owned.Host("Lucid persistent silent", false).AddComponent<PersistentLevelObjectBoolean>();
                Set(silent, "m_onStateTrue", null);
                Set(silent, "m_onStateFalse", null);
                Assert.DoesNotThrow(() => Call(silent, "OnStateChanged", true));
                Assert.DoesNotThrow(() => Call(silent, "OnStateChanged", false));
            });
        }

        // With an id, Awake defers OnLevelManagerValid on GetSystemRef<LevelManager> (0x18CAB67)
        // and announces nothing. Once valid, 0x18CACA0 stores the manager, handles a current
        // level directly and then subscribes OnLevelActivated; OnDestroy 0x18CABE0 removes it.
        [Test]
        public void AnIdWaitsForTheLevelManagerAndSubscribesToLevelActivation()
        {
            Owned.Run(owned =>
            {
                owned.LeaseRow<MissionManager>();
                owned.Register<LevelManager>(null);
                var log = new List<string>();
                PersistentLevelObjectBoolean persistent = Persistent(owned, log);
                Set(persistent, "m_id", Identifier);
                Set(persistent, "m_defaultValue", true);
                Call(persistent, "Awake");
                Assert.That(Current(persistent), Is.True);
                Assert.That(log, Is.Empty);
                var pending = Get<FastAction<LevelManager>>(ProcessManager.GetSystemRef<LevelManager>(), "m_actionOnSystemValid");
                Assert.That(pending.GetInvocationList().Select(action => (action.Target, action.Method.Name)),
                    Is.EqualTo(new[] { ((object)persistent, "OnLevelManagerValid") }));
            });

            Owned.Run(owned =>
            {
                owned.LeaseRow<MissionManager>();
                LevelManager levelManager = Level(owned, false);
                var log = new List<string>();
                PersistentLevelObjectBoolean persistent = Persistent(owned, log);
                Set(persistent, "m_id", Identifier);
                Assert.DoesNotThrow(() => Call(persistent, "OnDestroy"), "no level manager is stored yet");
                Call(persistent, "Awake");
                Assert.That(Get<LevelManager>(persistent, "m_levelManager"), Is.SameAs(levelManager));
                Assert.That(LevelActivated(levelManager).Select(action => (action.Target, action.Method.Name)),
                    Is.EqualTo(new[] { ((object)persistent, "OnLevelActivated") }));
                Assert.That(log, Is.Empty, "without a current level nothing is announced");

                Call(persistent, "OnDestroy");
                Assert.That(LevelActivated(levelManager), Is.Empty);
            });
        }

        // With a current level, OnLevelManagerValid calls OnLevelActivated before subscribing,
        // and OnLevelActivated 0x18CAE40 tests ChallengeModeActive (SystemRef.Get at 0x18cae83)
        // before reading the save. With no MissionManager, Get faults there: the manager is
        // stored, nothing is subscribed and no save record is read.
        [Test]
        public void ACurrentLevelIsHandledBeforeSubscribingAndChecksTheChallengeFirst()
        {
            Owned.Run(owned =>
            {
                owned.LeaseRow<MissionManager>();
                LevelManager levelManager = Level(owned, true);
                var log = new List<string>();
                PersistentLevelObjectBoolean persistent = Persistent(owned, log);
                Set(persistent, "m_id", Identifier);
                var missing = Assert.Throws<NullReferenceException>(() => Call(persistent, "Awake"));
                Assert.That(missing.Message, Is.EqualTo("System '" + ProcessManager.GetDefaultName<MissionManager>() + "' is null"));
                Assert.That(Get<LevelManager>(persistent, "m_levelManager"), Is.SameAs(levelManager));
                Assert.That(LevelActivated(levelManager), Is.Empty, "the direct call comes before the subscription");
                Assert.That(Get<object>(persistent, "m_saveData"), Is.Null);
                Assert.That(log, Is.Empty);
            });
        }

        // Action_SetPersistentState 0x18CAF10 returns at once for an unset id. Otherwise it
        // stores the value (0x18caf4f) before the inlined ChallengeModeActive, so without a
        // MissionManager the value is kept and neither the save record nor the event is reached.
        [Test]
        public void SetPersistentStateStoresTheValueBeforeTheChallengeTest()
        {
            Owned.Run(owned =>
            {
                owned.LeaseRow<MissionManager>();
                var log = new List<string>();
                PersistentLevelObjectBoolean persistent = Persistent(owned, log);
                persistent.Action_SetPersistentState(true);
                Assert.That(Current(persistent), Is.False, "an unset id ignores the call");
                Assert.That(log, Is.Empty);

                Set(persistent, "m_id", Identifier);
                var missing = Assert.Throws<NullReferenceException>(() => persistent.Action_SetPersistentState(true));
                Assert.That(missing.Message, Is.EqualTo("System '" + ProcessManager.GetDefaultName<MissionManager>() + "' is null"));
                Assert.That(Current(persistent), Is.True);
                Assert.That(log, Is.Empty);
            });
        }

        // The Boolean save overrides tail-call GetOrCreatePersistentObjectData (0x6B3080) and
        // Set (0x6B30D0) and read the record's byte at +0x1c (0x6B30A0). A created record takes
        // the default; an existing one keeps its value. 0x6B30C0 returns m_stateWhenChallenge.
        [Test]
        public void BooleanOverridesUseTheLevelSaveRecord()
        {
            Owned.Run(owned =>
            {
                owned.LeaseRow<MissionManager>();
                var persistent = owned.Host("Lucid persistent save", false).AddComponent<PersistentLevelObjectBoolean>();
                var level = new SaveDataLevel("lucid-level");
                var record = (SaveDataLevelPersistentObject)Call(persistent, "GetPersistentObjectFromSaveDataLevel", level, Identifier, true);
                Assert.That(record.ID, Is.EqualTo(Identifier));
                Assert.That(Call(persistent, "GetSavedValue", record), Is.EqualTo(true), "a created record takes the default");
                Assert.That(Call(persistent, "GetPersistentObjectFromSaveDataLevel", level, Identifier, false), Is.SameAs(record));
                Assert.That(record.GetBooleanValue(), Is.True, "an existing record keeps its value");

                Call(persistent, "SetSavedValue", record, false);
                Assert.That(record.GetBooleanValue(), Is.False);
                Assert.That(Call(persistent, "GetSavedValue", record), Is.EqualTo(false));

                Assert.That(Call(persistent, "GetDefaultDuringChallenge"), Is.EqualTo(true));
                Set(persistent, "m_stateWhenChallenge", false);
                Assert.That(Call(persistent, "GetDefaultDuringChallenge"), Is.EqualTo(false));
            });
        }

        private const string LevelGuid = "lucid-persistent-level";
        private static readonly Regex RefusedCoroutine = new Regex("Coroutine couldn't be started");

        internal sealed class Missions
        {
            public MissionManager Manager;
            public SaveManager Save;
            public LevelManager Level;
            public LevelManagerLevel CurrentLevel;
            public Dictionary<string, SaveDataLevel> SavedLevels;
            public List<string> Log;
        }

        // A real MissionManager, SaveManager and LevelManager in the shared rows. SaveManager is
        // registered before the LevelManager caches its reference; the MissionManager constructor
        // looks the LevelManager up, so it comes last, in the row the LevelManager's reference
        // created. That row is admitted absent or idle by LeaseRow and the manager is registered
        // into it directly, then unregistered by identity, because RegistryRow needs an absent row.
        // The open save holds only the level dictionary GetOrCreateLevelData reads. RequestSave's
        // RunCoroutine reaches an inactive CoroutineUtils host, which refuses it with an error
        // that is appended to the case log in order.
        internal static void WithMissions(bool currentLevelValid, Action<Owned, Missions> body) => Owned.Run(owned =>
        {
            owned.LeaseActions(SystemAction.Initialise, SystemAction.Shutdown);
            owned.LeaseRow<MissionManager>();
            owned.LeaseRow<AudioManager>();
            var missions = new Missions { Log = new List<string>(), SavedLevels = new Dictionary<string, SaveDataLevel>() };
            missions.Save = owned.Register(new SaveManager());
            var game = new SaveDataGame();
            Set(game, "m_levelsByGuid", missions.SavedLevels);
            Set(missions.Save, "m_currentSave", game);
            missions.Level = owned.Register(owned.Host("Lucid level manager", false).AddComponent<LevelManager>());
            var definition = owned.Track(ScriptableObject.CreateInstance<GameplayLevelDefinition>());
            Set(definition, "m_guid", LevelGuid);
            missions.CurrentLevel = new LevelManagerLevel(definition, definition);
            Set(missions.Level, "m_currentLevel", missions.CurrentLevel);
            typeof(LevelManager).GetProperty("CurrentLevelValid").SetValue(missions.Level, currentLevelValid);
            owned.LeaseRow<GameplayIslandManager>();
            owned.LeaseRow<CharacterManager>();

            FieldInfo coroutineHost = typeof(CoroutineUtils).GetField("s_instance", BindingFlags.Static | BindingFlags.NonPublic);
            object priorHost = coroutineHost.GetValue(null);
            Application.LogCallback capture = (condition, stackTrace, type) =>
            {
                if (RefusedCoroutine.IsMatch(condition)) missions.Log.Add("save requested");
            };
            VerificationFixtureOwnership.RunThenCleanUp(() =>
                {
                    coroutineHost.SetValue(null, owned.Host("Lucid refusing coroutine host", false).AddComponent<CoroutineUtils>());
                    Application.logMessageReceived += capture;
                    missions.Manager = new MissionManager();
                    ProcessManager.RegisterSystem(missions.Manager);
                    body(owned, missions);
                },
                () =>
                {
                    if (missions.Manager == null) return;
                    ProcessManager.UnregisterSystem(missions.Manager);
                    ProcessManager.UnsubscribeFromAllActions(missions.Manager);
                },
                () => Application.logMessageReceived -= capture,
                () => coroutineHost.SetValue(null, priorHost));
        });

        private const string MissionGuid = "lucid-persistent-mission";

        // ChallengeModeActive tests only the context's runtime type (isinst at 0x18cb0d2..0x18cb103),
        // but the context is a shipped one, built by its constructor and published through
        // MissionManager.SetActiveMissionContext, which loads its mission: the level's save record
        // and the mission state are the real ones. A regular context reads the open save for its best
        // time; a challenge context formats its description through an owned string table.
        private static void SetContext(Owned owned, Missions missions, bool challenge)
        {
            owned.LeaseRow<TimeTrialLeaderboardManager>(); // MissionDefinition's field initialiser
            owned.LeaseRow<UIManager>(); // MissionState's field initialiser
            var mission = owned.Track(ScriptableObject.CreateInstance<MissionDefinition>());
            Set(mission, "m_guid", MissionGuid);
            Set(mission, "m_subMissions", new List<MissionDefinition>());
            GameplayLevelDefinition level = missions.CurrentLevel.LevelDefinition;
            if (!challenge)
            {
                owned.LeaseRow<ChallengeManager>(); // MissionContextRegular's field initialiser
                missions.Manager.SetActiveMissionContext(new MissionContextRegular(mission, level, false));
                return;
            }
            var definition = owned.Track(ScriptableObject.CreateInstance<ChallengeDefinition>());
            Set(definition, "m_mission", mission);
            Set(definition, "m_level", level);
            Set(definition, "m_characterSelection", new CharacterId[0]);
            Set(definition, "m_startPositions", new LevelStartPositionDefinition[0]);
            var state = new ChallengeState(0, definition, 0, null, 0);
            OwnedStringTable.Run(new Dictionary<Hardlight.Enums.Strings, string> { { (Hardlight.Enums.Strings)0, "Lucid challenge" } },
                () => missions.Manager.SetActiveMissionContext(new MissionContextChallenge(state, false)));
        }

        // Outside a challenge Action_SetPersistentState 0x18CAF10 writes the record (SetSavedValue,
        // slot 7 at 0x18cafde), announces the state (slot 4 at 0x18caff4) and then tail-calls
        // GetSystem<SaveManager>().RequestSave (0x18cb031). The event already sees the written
        // record, and the save request is the last step.
        [Test]
        public void OutsideAChallengeTheRecordIsWrittenBeforeTheStateChangeAndTheSaveIsRequestedLast()
        {
            WithMissions(false, (owned, missions) =>
            {
                SaveDataLevelPersistentObject record = new SaveDataLevel("lucid-record-level").GetOrCreatePersistentObjectData(Identifier, false);
                PersistentLevelObjectBoolean persistent = Persistent(owned, missions.Log, () => record.GetBooleanValue().ToString());
                Set(persistent, "m_id", Identifier);
                Set(persistent, "m_saveData", record);
                Assert.That(missions.Manager.ActiveMissionContext, Is.Null);

                LogAssert.Expect(LogType.Error, RefusedCoroutine);
                persistent.Action_SetPersistentState(true);
                LogAssert.NoUnexpectedReceived();
                Assert.That(missions.Log, Is.EqualTo(new[] { "event:True", "true", "save requested" }));
                Assert.That(record.GetBooleanValue(), Is.True);
                Assert.That(Current(persistent), Is.True);
            });
        }

        // In a challenge OnLevelActivated 0x18CAE40 takes GetDefaultDuringChallenge (slot 9 at
        // 0x18caefd) without reading the save, and OnLevelManagerValid subscribes with
        // triggerImmediately = true (edx = 1 at 0x18cad4d), so a current level is announced
        // twice. Action_SetPersistentState then only announces the state (0x18cb03b): the record
        // keeps its value and no save is requested.
        [Test]
        public void AChallengeUsesItsOwnStateAndNeverReadsOrWritesTheSave()
        {
            WithMissions(true, (owned, missions) =>
            {
                SetContext(owned, missions, true);
                Assert.That(missions.Log, Is.Empty, "publishing the context requests no save");
                string[] loadedLevels = missions.SavedLevels.Keys.ToArray();
                PersistentLevelObjectBoolean persistent = Persistent(owned, missions.Log);
                Set(persistent, "m_id", Identifier);
                Set(persistent, "m_defaultValue", true);
                Set(persistent, "m_stateWhenChallenge", false);

                Call(persistent, "Awake");
                Assert.That(missions.Log, Is.EqualTo(new[] { "event", "false", "event", "false" }));
                Assert.That(Current(persistent), Is.False);
                Assert.That(Get<object>(persistent, "m_saveData"), Is.Null);
                Assert.That(missions.SavedLevels.Keys, Is.EqualTo(loadedLevels), "Awake creates no level save data");
                Assert.That(LevelActivated(missions.Level).Select(action => (action.Target, action.Method.Name)),
                    Is.EqualTo(new[] { ((object)persistent, "OnLevelActivated") }));

                missions.Log.Clear();
                SaveDataLevelPersistentObject record = new SaveDataLevel("lucid-record-level").GetOrCreatePersistentObjectData(Identifier, false);
                Set(persistent, "m_saveData", record);
                persistent.Action_SetPersistentState(true);
                LogAssert.NoUnexpectedReceived();
                Assert.That(missions.Log, Is.EqualTo(new[] { "event", "true" }));
                Assert.That(record.GetBooleanValue(), Is.False, "the record is not written");
                Assert.That(Current(persistent), Is.True);
                Call(persistent, "OnDestroy");
            });
        }

        // Any other active context is not a challenge (the isinst test, not a null test), so the
        // same Awake reads the level's save record twice and takes its value.
        [Test]
        public void ANonChallengeContextReadsTheSave()
        {
            WithMissions(true, (owned, missions) =>
            {
                SetContext(owned, missions, false);
                Assert.That(missions.Log, Is.Empty, "publishing the context requests no save");
                Assert.That(missions.Manager.ActiveMissionContext, Is.Not.Null);
                PersistentLevelObjectBoolean persistent = Persistent(owned, missions.Log);
                Set(persistent, "m_id", Identifier);
                Set(persistent, "m_defaultValue", true);
                Set(persistent, "m_stateWhenChallenge", false);

                Call(persistent, "Awake");
                Assert.That(missions.Log, Is.EqualTo(new[] { "event", "true", "event", "true" }));
                Assert.That(missions.SavedLevels.Keys, Is.EqualTo(new[] { LevelGuid }));
                var record = Get<SaveDataLevelPersistentObject>(persistent, "m_saveData");
                Assert.That(record.ID, Is.EqualTo(Identifier));
                Assert.That(record.GetBooleanValue(), Is.True);
                Call(persistent, "OnDestroy");
            });
        }

        // UpdateCurrentValueFromSave 0x18CAD70 asks the current save for the level's data by the
        // level definition's GUID, then for the record with m_id (+0x20) and m_defaultValue
        // (+0x24, 0x18cadec), and takes the record's value. A new record holds the default even
        // when the current value differs; an existing record keeps its own value.
        [Test]
        public void TheSaveReadCreatesTheRecordWithTheDefaultValue()
        {
            WithMissions(false, (owned, missions) =>
            {
                PersistentLevelObjectBoolean persistent = Persistent(owned, missions.Log);
                Set(persistent, "m_id", Identifier);
                Set(persistent, "m_defaultValue", true);
                Set(persistent, "m_currentValue", false);

                Call(persistent, "OnLevelActivated", missions.CurrentLevel);
                Assert.That(missions.Log, Is.EqualTo(new[] { "event", "true" }));
                Assert.That(missions.SavedLevels.Keys, Is.EqualTo(new[] { LevelGuid }));
                var record = Get<SaveDataLevelPersistentObject>(persistent, "m_saveData");
                Assert.That(record.ID, Is.EqualTo(Identifier));
                Assert.That(record.GetBooleanValue(), Is.True, "a created record takes m_defaultValue");
                Assert.That(Current(persistent), Is.True);

                record.Set(false);
                missions.Log.Clear();
                Call(persistent, "OnLevelActivated", missions.CurrentLevel);
                Assert.That(Get<SaveDataLevelPersistentObject>(persistent, "m_saveData"), Is.SameAs(record));
                Assert.That(Current(persistent), Is.False, "an existing record keeps its value");
                Assert.That(missions.Log, Is.EqualTo(new[] { "event", "false" }));
            });
        }
    }
}
