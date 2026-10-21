using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hardlight;
using Hardlight.Enums;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.ChallengeRewardTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // ChallengeManagerBinders (x86-64 0x5A8C40..0x5A99C6) over a real ChallengeManager built by its constructor
    // (the manager and the binders sit on inactive or edit-mode hosts, so no Unity message runs). The manager's
    // feature state is authored through its auto-property backing field and its challenge list through the live
    // list GetChallengeStates returns; no path reaches either without a started manager.
    //
    // Not covered: the ready branch of Update (the formatted time remaining). It needs ChallengeManager.Ready,
    // whose flag is set only by a started leaderboard integration (OnIntegrationInitialised defers into the
    // SaveManager and MissionManager rows), and a loaded StringTable for the formats; neither exists in Edit Mode.
    public sealed class ChallengeManagerBindersTests
    {
        // ChallengeManager's field initialisers read these six rows. The manager is added through the owned
        // time-scaled helper, which installs an empty category lookup for the editor's Reset and OnValidate.
        private static ChallengeManager NewManager(Owned owned)
        {
            owned.LeaseRow<DataManager>();
            owned.LeaseRow<VisualQualityManager_SDT>();
            owned.LeaseRow<CutsceneManager>();
            owned.LeaseRow<MissionManager>();
            owned.LeaseRow<SaveManager>();
            owned.LeaseRow<OrnamentManager>();
            return owned.Lease(new OwnedTimeScaledObjects("lucid-challenge-binders")).Add<ChallengeManager>("manager");
        }

        private static void WithBinder(Action<Owned, ChallengeManagerBinders, ChallengeManager> body) => Owned.Run(owned =>
        {
            ChallengeManager manager = NewManager(owned);
            var binder = owned.Host("Lucid challenge binders", false).AddComponent<ChallengeManagerBinders>();
            // The binder takes its reference from the process registry on enable; a standalone reference over the
            // manager keeps the registry untouched.
            Set(binder, "m_challengeManagerRef", new SystemRef<ChallengeManager>("lucid-challenge-manager", manager));
            body(owned, binder, manager);
        });

        private static void SetFeatureState(ChallengeManager manager, ChallengeManager.ChallengeFeatureState state) =>
            // The state is written through the auto-property setter's backing field: the setter is private and
            // the public routes need an open save and the App.
            Set(manager, "<FeatureState>k__BackingField", state);

        private static void Publish(ChallengeManagerBinders binder, params ChallengeState[] states) =>
            Call(binder, "OnChallengesUpdated", new List<ChallengeState>(states));

        private static string ShowIfTarget(FieldInfo field) =>
            (string)field.GetCustomAttributesData().Single(data => data.AttributeType == typeof(ShowIfAttribute)).ConstructorArguments[0].Value;

        // dump.cs TypeDefIndex 1778 and the DummyDll rows (fields 0x0400191c..0x04001929).
        [Test]
        public void DeclarationsMatchTheOriginalMetadata()
        {
            AssertMembers(typeof(ChallengeManagerBinders), 0x00100001,
                new (string, int)[]
                {
                    ("ChallengeFeatureUnlocked", 0x0026),
                    ("ChallengeFeatureIsNew", 0x0026),
                    ("NewChallengeCount", 0x0026),
                    ("TimeRemainingText", 0x0026),
                    ("m_updateTimeRemaining", 0x0001),
                    ("m_useShortFormats", 0x0001),
                    ("m_timeRemainingFormat", 0x0001),
                    ("m_timeRemainingFormatDaysHours", 0x0001),
                    ("m_timeRemainingFormatHoursMinutes", 0x0001),
                    ("m_timeRemainingFormatMinutesSeconds", 0x0001),
                    ("m_alertBadge", 0x0001),
                    ("m_challengeManagerRef", 0x0001),
                    ("m_lastTimeRemaining", 0x0001),
                    ("m_stringParams", 0x0021),
                },
                new (string, int)[]
                {
                    ("OnEnable", 0x0081),
                    ("OnChallengeManagerValid", 0x0081),
                    ("OnDisable", 0x0081),
                    ("OnChallengesUpdated", 0x0081),
                    ("Update", 0x0081),
                    (".ctor", 0x1886),
                });
            AssertIl2CppOptions(typeof(ChallengeManagerBinders), 2, 1);
            Type type = typeof(ChallengeManagerBinders);
            const BindingFlags all = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            // Attribute order per field is the DummyDll order.
            Type tooltip = typeof(TooltipAttribute), serialized = typeof(SerializeField), showIf = typeof(ShowIfAttribute);
            Assert.That(AttributeTypes(type.GetField("m_updateTimeRemaining", all)), Is.EqualTo(new[] { tooltip, serialized }));
            Assert.That(AttributeTypes(type.GetField("m_useShortFormats", all)), Is.EqualTo(new[] { serialized, showIf }));
            Assert.That(AttributeTypes(type.GetField("m_timeRemainingFormat", all)), Is.EqualTo(new[] { tooltip, serialized, showIf }));
            Assert.That(AttributeTypes(type.GetField("m_timeRemainingFormatDaysHours", all)), Is.EqualTo(new[] { tooltip, serialized, showIf }));
            Assert.That(AttributeTypes(type.GetField("m_timeRemainingFormatHoursMinutes", all)), Is.EqualTo(new[] { showIf, serialized, tooltip }));
            Assert.That(AttributeTypes(type.GetField("m_timeRemainingFormatMinutesSeconds", all)), Is.EqualTo(new[] { showIf, tooltip, serialized }));
            Assert.That(AttributeTypes(type.GetField("m_alertBadge", all)), Is.EqualTo(new[] { serialized, tooltip }));
            Assert.That(ShowIfTarget(type.GetField("m_useShortFormats", all)), Is.EqualTo("m_updateTimeRemaining"));
            Assert.That(ShowIfTarget(type.GetField("m_timeRemainingFormat", all)), Is.EqualTo("m_updateTimeRemaining"));
            Assert.That(ShowIfTarget(type.GetField("m_timeRemainingFormatDaysHours", all)), Is.EqualTo("m_useShortFormats"));
            Assert.That(ShowIfTarget(type.GetField("m_timeRemainingFormatHoursMinutes", all)), Is.EqualTo("m_useShortFormats"));
            Assert.That(ShowIfTarget(type.GetField("m_timeRemainingFormatMinutesSeconds", all)), Is.EqualTo("m_useShortFormats"));
            Assert.That(Tooltip(type.GetField("m_timeRemainingFormatDaysHours", all)),
                Is.EqualTo("String format for the time remaining with only days and hours. Parameters: {0} days, {1} hours."));
        }

        // The constructor 0x5A9850 allocates the four bindables, stores 1 at +0x40 (the update flag), copies the 16
        // bytes at 0x29a79e0 over the four format fields (+0x44..+0x53) and allocates a four-slot parameter array.
        // The short-format flag (+0x41) is not written.
        [Test]
        public void ConstructorSetsTheLiteralFormatsAndFlags()
        {
            Owned.Run(owned =>
            {
                var binder = owned.Host("Lucid challenge binders", false).AddComponent<ChallengeManagerBinders>();
                Assert.That(Get<bool>(binder, "m_updateTimeRemaining"), Is.True);
                Assert.That(Get<bool>(binder, "m_useShortFormats"), Is.False);
                Assert.That((int)Get<Strings>(binder, "m_timeRemainingFormat"), Is.EqualTo(895986815), "0x3567ac7f");
                Assert.That((int)Get<Strings>(binder, "m_timeRemainingFormatDaysHours"), Is.EqualTo(1845892065), "0x6e0613e1");
                Assert.That((int)Get<Strings>(binder, "m_timeRemainingFormatHoursMinutes"), Is.EqualTo(-2107480886), "0x826264ca");
                Assert.That((int)Get<Strings>(binder, "m_timeRemainingFormatMinutesSeconds"), Is.EqualTo(1277909823), "0x4c2b5b3f");
                Assert.That(Get<object[]>(binder, "m_stringParams"), Has.Length.EqualTo(4));
                Assert.That(binder.ChallengeFeatureUnlocked.Value, Is.False);
                Assert.That(binder.ChallengeFeatureIsNew.Value, Is.False);
                Assert.That(binder.NewChallengeCount.Value, Is.EqualTo(0));
                Assert.That(binder.TimeRemainingText.Value, Is.Null);
                Assert.That(Get<TimeSpan>(binder, "m_lastTimeRemaining"), Is.EqualTo(TimeSpan.Zero));
                Assert.That(Get<object>(binder, "m_challengeManagerRef"), Is.Null, "created on enable, not at construction");
            });
        }

        // OnChallengesUpdated 0x5A8E30 writes ChallengeFeatureUnlocked from FeatureState (+0xE8) != 0 and
        // ChallengeFeatureIsNew from (uint)(FeatureState - 1) < 2 (0x5A8ee1..0x5A8ee8). Truth table over every
        // state and two out-of-range values: (state, unlocked, isNew).
        [Test]
        public void FeatureStateDrivesTheUnlockedAndNewBindables()
        {
            var table = new[]
            {
                (state: 0, unlocked: false, isNew: false),
                (state: 1, unlocked: true, isNew: true),
                (state: 2, unlocked: true, isNew: true),
                (state: 3, unlocked: true, isNew: false),
                (state: 4, unlocked: true, isNew: false),
                (state: -1, unlocked: true, isNew: false),
            };
            WithBinder((owned, binder, manager) =>
            {
                foreach (var row in table)
                {
                    SetFeatureState(manager, (ChallengeManager.ChallengeFeatureState)row.state);
                    // Start from the opposite values so a missing write is seen.
                    binder.ChallengeFeatureUnlocked.Value = !row.unlocked;
                    binder.ChallengeFeatureIsNew.Value = !row.isNew;
                    Publish(binder);
                    Assert.That(binder.ChallengeFeatureUnlocked.Value, Is.EqualTo(row.unlocked), "unlocked, state " + row.state);
                    Assert.That(binder.ChallengeFeatureIsNew.Value, Is.EqualTo(row.isNew), "new, state " + row.state);
                }
            });
        }

        // 0x5A8fa0..0x5A9089 count the challenges with ucomiss(BestTimeSeconds, 0) not above and BestScore <= 0:
        // a challenge is new while it has neither a positive time nor a positive score; a NaN time is not positive.
        // Truth table over (best time, best score).
        [Test]
        public void NewChallengeCountCountsTheChallengesWithoutAPositiveTimeOrScore()
        {
            var table = new[]
            {
                (time: 0f, score: 0, isNew: true),
                (time: 12.5f, score: 0, isNew: false),
                (time: 0f, score: 40, isNew: false),
                (time: 12.5f, score: 40, isNew: false),
                (time: -1f, score: -1, isNew: true),
                (time: -1f, score: 3, isNew: false),
                (time: float.NaN, score: 0, isNew: true),
                (time: float.NaN, score: 3, isNew: false),
                (time: float.Epsilon, score: 0, isNew: false),
            };
            WithBinder((owned, binder, manager) =>
            {
                var states = new List<ChallengeState>();
                int expected = 0;
                foreach (var row in table)
                {
                    ChallengeState state = NewState(owned, row.score, row.time);
                    states.Add(state);
                    if (row.isNew) expected++;
                    binder.NewChallengeCount.Value = 99;
                    Publish(binder, state);
                    Assert.That(binder.NewChallengeCount.Value, Is.EqualTo(row.isNew ? 1 : 0), "time " + row.time + ", score " + row.score);
                }
                binder.NewChallengeCount.Value = 99;
                Publish(binder, states.ToArray());
                Assert.That(binder.NewChallengeCount.Value, Is.EqualTo(expected), "the whole list");
                Assert.That(expected, Is.EqualTo(3));
                binder.NewChallengeCount.Value = 99;
                Publish(binder);
                Assert.That(binder.NewChallengeCount.Value, Is.EqualTo(0), "an empty list");
            });
        }

        // A challenge built by its constructor over a real definition and mission; UpdateScoreInfo(forceSet) then
        // stores the exact best score and time.
        private static ChallengeState NewState(Owned owned, int score, float time)
        {
            owned.LeaseRow<TimeTrialLeaderboardManager>(); // MissionDefinition's field initialiser
            var mission = Create<MissionDefinition>(owned);
            var definition = Create<ChallengeDefinition>(owned);
            Set(definition, "m_mission", mission);
            Set(definition, "m_characterSelection", new CharacterId[0]);
            Set(definition, "m_startPositions", new LevelStartPositionDefinition[0]);
            var state = new ChallengeState(0, definition, 0, null, 0);
            state.OnAwardXP = (challenge, first, ringXp) => { };
            state.OnUpdateScore = challenge => { };
            state.UpdateScoreInfo(score, time, 0, true);
            return state;
        }

        // 0x5A9135..0x5A91bc: with a badge, SetAlert(isNew || BonusXPUnacknowledged > 0, count). The badge object is
        // deactivated for no alert and no count (0x69f5d0 first branch); otherwise it is activated and then the badge
        // writes its text, which faults here because the badge has no text component.
        [Test]
        public void AlertBadgeShowsForANewFeatureOrUnacknowledgedBonusXp()
        {
            WithBinder((owned, binder, manager) =>
            {
                GameObject shown = owned.Host("Lucid badge shown", true);
                var badge = owned.Host("Lucid badge", false).AddComponent<UIWidgetSDTAlertBadge>();
                Set(badge, "m_enableObjectWhileActive", shown);
                Set(binder, "m_alertBadge", badge);

                // Locked, no bonus, no new challenge: no alert, no number.
                SetFeatureState(manager, ChallengeManager.ChallengeFeatureState.Locked);
                Publish(binder);
                Assert.That(shown.activeSelf, Is.False);

                // A new feature alerts.
                shown.SetActive(false);
                SetFeatureState(manager, ChallengeManager.ChallengeFeatureState.UnlockedShowInterstitial);
                Assert.Catch<Exception>(() => Publish(binder));
                Assert.That(shown.activeSelf, Is.True);

                // A seen feature without bonus XP does not, even with the feature unlocked.
                shown.SetActive(true);
                SetFeatureState(manager, ChallengeManager.ChallengeFeatureState.UnlockedSeenFeature);
                Publish(binder);
                Assert.That(shown.activeSelf, Is.False);

                // Unacknowledged bonus XP alerts a seen feature.
                Set(manager, "m_saveDataChallenges", new SaveDataChallenges { BonusXPUnacknowledged = 7 });
                Assert.Catch<Exception>(() => Publish(binder));
                Assert.That(shown.activeSelf, Is.True);

                // A new challenge alone shows a number.
                shown.SetActive(false);
                Set(manager, "m_saveDataChallenges", new SaveDataChallenges { BonusXPUnacknowledged = 0 });
                Assert.Catch<Exception>(() => Publish(binder, NewState(owned, 0, 0f)));
                Assert.That(shown.activeSelf, Is.True);
            });
        }

        // The badge test is Unity's op_Equality (0x5A9158): an unassigned badge and a destroyed one are skipped.
        [Test]
        public void AMissingOrDestroyedBadgeIsSkipped()
        {
            WithBinder((owned, binder, manager) =>
            {
                SetFeatureState(manager, ChallengeManager.ChallengeFeatureState.UnlockedShowInterstitial);
                Assert.DoesNotThrow(() => Publish(binder), "no badge assigned");
                var badge = owned.Host("Lucid destroyed badge", false).AddComponent<UIWidgetSDTAlertBadge>();
                Set(binder, "m_alertBadge", badge);
                UnityEngine.Object.DestroyImmediate(badge);
                Assert.DoesNotThrow(() => Publish(binder), "a destroyed badge is Unity-null");
                Assert.That(binder.ChallengeFeatureIsNew.Value, Is.True, "the bindables are still published");
            });
        }

        // OnEnable 0x5A8C40 creates the reference only when the field is null, then InvokeOnValid(OnChallengeManagerValid)
        // 0x5A8D30: with the manager registered the callback runs at once, subscribes OnChallengesUpdated (+0xD8)
        // by delegate combine and publishes the manager's live list (+0xB0). A second enable subscribes again.
        [Test]
        public void EnableTakesTheManagerFromTheRegistryOnceAndSubscribesEachTime()
        {
            Owned.Run(owned =>
            {
                ChallengeManager manager = NewManager(owned);
                SetFeatureState(manager, ChallengeManager.ChallengeFeatureState.UnlockedSeenFeature);
                owned.Register(manager);
                var binder = owned.Host("Lucid challenge binders", false).AddComponent<ChallengeManagerBinders>();
                Assert.That(manager.OnChallengesUpdated, Is.Null);
                binder.ChallengeFeatureUnlocked.Value = false;

                Call(binder, "OnEnable");
                var reference = Get<SystemRef<ChallengeManager>>(binder, "m_challengeManagerRef");
                Assert.That(reference.Get(), Is.SameAs(manager), "taken from the process registry");
                Assert.That(manager.OnChallengesUpdated.GetInvocationList().Select(handler => handler.Target), Is.EqualTo(new object[] { binder }));
                Assert.That(binder.ChallengeFeatureUnlocked.Value, Is.True, "the current state is published on subscription");

                Call(binder, "OnEnable");
                Assert.That(Get<SystemRef<ChallengeManager>>(binder, "m_challengeManagerRef"), Is.SameAs(reference), "the reference is kept");
                Assert.That(manager.OnChallengesUpdated.GetInvocationList().Length, Is.EqualTo(2), "each enable adds its own subscription");

                // OnDisable 0x5A9230 removes one subscription per call while the manager exists.
                Call(binder, "OnDisable");
                Assert.That(manager.OnChallengesUpdated.GetInvocationList().Length, Is.EqualTo(1));
                Call(binder, "OnDisable");
                Assert.That(manager.OnChallengesUpdated, Is.Null);
                Call(binder, "OnDisable");
                Assert.That(manager.OnChallengesUpdated, Is.Null, "removing an absent handler changes nothing");
            });
        }

        // OnDisable returns at the IsNull test (0x5A9296) when the reference holds no manager, and the unguarded
        // IsNull call on a never-created reference faults.
        [Test]
        public void DisableTouchesNothingWithoutAManagerAndFaultsWithoutAReference()
        {
            Owned.Run(owned =>
            {
                ChallengeManager manager = NewManager(owned);
                var binder = owned.Host("Lucid challenge binders", false).AddComponent<ChallengeManagerBinders>();
                Assert.Throws<NullReferenceException>(() => Call(binder, "OnDisable"), "no reference yet");
                Set(binder, "m_challengeManagerRef", new SystemRef<ChallengeManager>("lucid-empty-reference", null));
                Assert.DoesNotThrow(() => Call(binder, "OnDisable"));
                Assert.That(manager.OnChallengesUpdated, Is.Null);
            });
        }

        // Update 0x5A9370 returns at once when the update flag (+0x40) is clear, when the reference holds no manager
        // (IsNull 0x5A93f0) and when the manager is not Ready (0x5A942f..0x5A9448); the sentinel text and the stored
        // last time are untouched.
        [Test]
        public void UpdatePublishesNothingWithoutAReadyManager()
        {
            WithBinder((owned, binder, manager) =>
            {
                binder.TimeRemainingText.Value = "lucid-sentinel";
                Assert.That(manager.Ready, Is.False);
                Call(binder, "Update");
                Assert.That(binder.TimeRemainingText.Value, Is.EqualTo("lucid-sentinel"), "the manager is not ready");

                Set(binder, "m_updateTimeRemaining", false);
                Call(binder, "Update");
                Assert.That(binder.TimeRemainingText.Value, Is.EqualTo("lucid-sentinel"), "the update flag is clear");

                Set(binder, "m_updateTimeRemaining", true);
                Set(binder, "m_challengeManagerRef", new SystemRef<ChallengeManager>("lucid-empty-reference", null));
                Call(binder, "Update");
                Assert.That(binder.TimeRemainingText.Value, Is.EqualTo("lucid-sentinel"), "the reference holds no manager");
                Assert.That(Get<TimeSpan>(binder, "m_lastTimeRemaining"), Is.EqualTo(TimeSpan.Zero));
            });
        }
    }
}
