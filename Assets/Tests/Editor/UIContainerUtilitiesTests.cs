using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Hardlight;
using Hardlight.UI.Binding;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AddressableAssets;
using static ProjectLucid.Tests.CharacterTriggerTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // UIContainerUtilities (x86-64 0x681C70 TryGetSelectedLevelAndZone, 0x681DF0 SetBackgroundGradientBinders,
    // 0x681FD0 GetZoneAccent) over the genuine App storage, LevelManager and MissionManager. The class keeps its three
    // typed system references in static readonly fields built by the type initialiser, so each case points them at the
    // case's own systems and puts them back afterwards. Definitions are tested for null with the authored
    // ScriptableObjectWithGuid operator (0x681eb4, 0x682031, 0x682041, so a destroyed definition counts as missing), but
    // the "??" fallbacks (0x682077, 0x682080) and the "?." tests (0x681f69, 0x68216d) are plain reference tests.
    public sealed class UIContainerUtilitiesTests
    {
        private const BindingFlags Statics = BindingFlags.Static | BindingFlags.NonPublic;

        private sealed class Fixture
        {
            public App App;
            public OriginalPersistentLevelObjectTests.Missions Missions;
            public GameplayLevelDefinition Level;
            public GameplayLevelCategory Zone;
            public GameplayLevelDefinition OtherLevel;
            public GameplayLevelCategory OtherZone;
        }

        private static GameplayLevelDefinition NewLevel(Owned owned, string guid)
        {
            var level = owned.Track(ScriptableObject.CreateInstance<GameplayLevelDefinition>());
            Set(level, "m_guid", guid); // serialized authored data
            Set(level, "m_sceneName", guid); // serialized authored data
            return level;
        }

        private static GameplayLevelCategory NewZone(Owned owned, string guid, Color start, Color end, params GameplayLevelDefinition[] levels)
        {
            var zone = owned.Track(ScriptableObject.CreateInstance<GameplayLevelCategory>());
            Set(zone, "m_guid", guid); // serialized authored data
            Set(zone, "m_gameplayEntries", new List<GameplayLevelUIEntry>(levels)); // serialized authored data
            Set(zone, "m_backgroundStartColour", start); // serialized authored data
            Set(zone, "m_backgroundEndColour", end); // serialized authored data
            return zone;
        }

        // A theme override with its own accent wrapper (made by OnEnable) and, optionally, a gradient.
        private static ZoneThemeOverride NewTheme(Owned owned, bool gradient, Color start, Color end, bool accent = false)
        {
            var theme = owned.Track(ScriptableObject.CreateInstance<ZoneThemeOverride>());
            Set(theme, "m_zoneGradientOverride", gradient); // serialized authored data
            Set(theme, "m_backgroundStartColour", start); // serialized authored data
            Set(theme, "m_backgroundEndColour", end); // serialized authored data
            if (accent) Set(theme, "m_zoneAccentOverride", new AssetReferenceAtlasedSprite("lucid-accent-guid")); // serialized authored data
            return theme;
        }

        // The type initialiser builds its typed references from the rows it finds, so it runs once here against empty
        // rows that are released at once (as InitialiseTriggerConditions does) and not while a case has systems registered.
        private static void InitialiseUtilities() => Owned.Run(owned =>
        {
            owned.LeaseRow<App>();
            owned.LeaseRow<LevelManager>();
            owned.LeaseRow<MissionManager>();
            RuntimeHelpers.RunClassConstructor(typeof(UIContainerUtilities).TypeHandle);
        });

        private static void PointAt<T>(Owned owned, string field, T system) where T : class, ISystem
        {
            FieldInfo target = typeof(UIContainerUtilities).GetField(field, Statics);
            object prior = target.GetValue(null);
            target.SetValue(null, new SystemRef<T>(ProcessManager.GetDefaultName<T>(), system));
            owned.Lease(new ReleaseAction(() => target.SetValue(null, prior)));
        }

        // A real MissionManager, LevelManager and SaveManager (OriginalPersistentLevelObjectTests.WithMissions) and a
        // genuine App built by its own initialisers on an inactive host. The LevelManager holds a GameLevels of one zone
        // with one level; the meta game selects that level when asked.
        private static void WithUtilities(bool selected, Action<Owned, Fixture> body)
        {
            InitialiseUtilities();
            OriginalPersistentLevelObjectTests.WithMissions(true, (owned, missions) =>
            {
                owned.LeaseRow<TimeTrialLeaderboardManager>(); // MissionDefinition's field initialiser
                owned.LeaseRow<UIManager>(); // MissionState's field initialiser
                owned.LeaseRow<ChallengeManager>(); // MissionContextRegular's field initialiser
                owned.LeaseRow<UIScreenTransitionManager>(); // App's field initialiser
                var fixture = new Fixture { Missions = missions };
                fixture.App = owned.Register(owned.Host("Lucid app", false).AddComponent<App>());
                fixture.Level = NewLevel(owned, "lucid-selected-level");
                fixture.OtherLevel = NewLevel(owned, "lucid-other-level");
                fixture.Zone = NewZone(owned, "lucid-selected-zone", Color.red, Color.blue, fixture.Level);
                fixture.OtherZone = NewZone(owned, "lucid-other-zone", Color.black, Color.white, fixture.OtherLevel);
                var levels = owned.Track(ScriptableObject.CreateInstance<GameLevels>());
                var entries = (List<GameplayLevelUIEntry>)typeof(GameLevels).GetField("m_gameplayEntries", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(levels);
                entries.Add(fixture.Zone);
                entries.Add(fixture.OtherZone);
                typeof(LevelManager).GetProperty("GameLevels").SetValue(missions.Level, levels);
                if (selected) fixture.App.Storage.SetValue(AppFSMKeys.MetaGameState, new MetaGameState(fixture.Level, null));
                PointAt(owned, "s_appRef", fixture.App);
                PointAt(owned, "s_levelManagerRef", missions.Level);
                PointAt(owned, "s_missionManagerRef", missions.Manager);
                body(owned, fixture);
            });
        }

        // Without a MetaGameState in the App storage (0x681cbd) there is no selection: both outs stay null and the callers
        // return before touching their bindables. A passed level and zone need no selection at all, but one missing sends
        // the lookup that then fails (the "||" at 0x682031/0x682041).
        [Test]
        public void WithoutASelectedLevelThereIsNoZoneAndTheBindersAreLeftAlone()
        {
            WithUtilities(false, (owned, fixture) =>
            {
                Assert.That(UIContainerUtilities.TryGetSelectedLevelAndZone(out GameplayLevelDefinition level, out GameplayLevelCategory zone), Is.False);
                Assert.That(level == null, Is.True);
                Assert.That(zone == null, Is.True);

                var start = new Bindable<Color>(Color.green);
                var end = new Bindable<Color>(Color.yellow);
                UIContainerUtilities.SetBackgroundGradientBinders(start, end);
                Assert.That(start.Value, Is.EqualTo(Color.green));
                Assert.That(end.Value, Is.EqualTo(Color.yellow));

                Assert.That(UIContainerUtilities.GetZoneAccent(), Is.Null);
                Assert.That(UIContainerUtilities.GetZoneAccent(null, fixture.OtherZone, null), Is.Null, "a missing level needs the selection");
                Assert.That(UIContainerUtilities.GetZoneAccent(null, null, fixture.OtherLevel), Is.Null, "a missing zone needs the selection");
                Assert.That(UIContainerUtilities.GetZoneAccent(null, fixture.OtherZone, fixture.OtherLevel), Is.SameAs(fixture.OtherZone.ZoneAccent),
                    "a passed level and zone need no selection");
            });
        }

        // TryGetSelectedLevelAndZone reads the MetaGameState level and asks GameLevels for its zone (0x681d0a..0x681d60); a
        // selected level that no zone holds has no zone and fails.
        [Test]
        public void TheSelectedLevelAndItsZoneComeFromTheMetaGameState()
        {
            WithUtilities(true, (owned, fixture) =>
            {
                Assert.That(UIContainerUtilities.TryGetSelectedLevelAndZone(out GameplayLevelDefinition level, out GameplayLevelCategory zone), Is.True);
                Assert.That(level, Is.SameAs(fixture.Level));
                Assert.That(zone, Is.SameAs(fixture.Zone));

                fixture.App.Storage.SetValue(AppFSMKeys.MetaGameState, new MetaGameState(NewLevel(owned, "lucid-unlisted-level"), null));
                Assert.That(UIContainerUtilities.TryGetSelectedLevelAndZone(out _, out GameplayLevelCategory missingZone), Is.False);
                Assert.That(missingZone == null, Is.True);
            });
        }

        // A passed zone is kept when only the level is missing, a passed level when only the zone is missing, and a destroyed
        // level is looked up again but kept by the plain "??" (0x682077), so its own theme override still applies.
        [Test]
        public void OnlyTheMissingLevelOrZoneIsTakenFromTheSelection()
        {
            WithUtilities(true, (owned, fixture) =>
            {
                Assert.That(fixture.Zone.ZoneAccent, Is.Not.SameAs(fixture.OtherZone.ZoneAccent));
                Assert.That(UIContainerUtilities.GetZoneAccent(), Is.SameAs(fixture.Zone.ZoneAccent), "both from the selection");
                Assert.That(UIContainerUtilities.GetZoneAccent(null, fixture.OtherZone, null), Is.SameAs(fixture.OtherZone.ZoneAccent),
                    "the passed zone is kept, not replaced by the selected one");
                Assert.That(UIContainerUtilities.GetZoneAccent(null, null, fixture.OtherLevel), Is.SameAs(fixture.Zone.ZoneAccent),
                    "the passed level is kept; the zone comes from the selection");

                ZoneThemeOverride theme = NewTheme(owned, false, Color.clear, Color.clear, true);
                var destroyed = ScriptableObject.CreateInstance<GameplayLevelDefinition>();
                Set(destroyed, "m_zoneThemeOverride", theme); // serialized authored data
                Object.DestroyImmediate(destroyed);
                Assert.That(UIContainerUtilities.GetZoneAccent(null, fixture.OtherZone, destroyed), Is.SameAs(theme.ZoneAccentOverride),
                    "the destroyed level is looked up again but kept");
            });
        }

        // The level override replaces the zone's gradient and accent (0x681f69, 0x68216d), then the mission's. The mission is
        // the passed one, or the active context's when none is passed or the passed one is destroyed.
        [Test]
        public void TheGradientIsLayeredFromTheZoneThenTheLevelThenTheMission()
        {
            WithUtilities(true, (owned, fixture) =>
            {
                var start = new Bindable<Color>();
                var end = new Bindable<Color>();
                UIContainerUtilities.SetBackgroundGradientBinders(start, end);
                Assert.That((start.Value, end.Value), Is.EqualTo((Color.red, Color.blue)), "the zone's colours");

                Set(fixture.Level, "m_zoneThemeOverride", NewTheme(owned, true, Color.green, Color.yellow)); // serialized authored data
                UIContainerUtilities.SetBackgroundGradientBinders(start, end);
                Assert.That((start.Value, end.Value), Is.EqualTo((Color.green, Color.yellow)), "the level's override");

                var passed = owned.Track(ScriptableObject.CreateInstance<MissionDefinition>());
                Set(passed, "m_zoneThemeOverride", NewTheme(owned, true, Color.cyan, Color.magenta)); // serialized authored data
                UIContainerUtilities.SetBackgroundGradientBinders(start, end, passed);
                Assert.That((start.Value, end.Value), Is.EqualTo((Color.cyan, Color.magenta)), "the passed mission's override");

                var plain = owned.Track(ScriptableObject.CreateInstance<MissionDefinition>());
                Set(plain, "m_zoneThemeOverride", NewTheme(owned, false, Color.black, Color.white)); // serialized authored data
                UIContainerUtilities.SetBackgroundGradientBinders(start, end, plain);
                Assert.That((start.Value, end.Value), Is.EqualTo((Color.green, Color.yellow)), "an override without a gradient changes nothing");

                var active = owned.Track(ScriptableObject.CreateInstance<MissionDefinition>());
                Set(active, "m_guid", "lucid-utilities-mission"); // serialized authored data
                Set(active, "m_subMissions", new List<MissionDefinition>());
                Set(active, "m_zoneThemeOverride", NewTheme(owned, true, Color.gray, Color.cyan)); // serialized authored data
                fixture.Missions.Manager.SetActiveMissionContext(new MissionContextRegular(active, fixture.Missions.CurrentLevel.LevelDefinition, false));
                UIContainerUtilities.SetBackgroundGradientBinders(start, end);
                Assert.That((start.Value, end.Value), Is.EqualTo((Color.gray, Color.cyan)), "no mission passed: the active mission's override");

                var destroyed = ScriptableObject.CreateInstance<MissionDefinition>();
                Set(destroyed, "m_zoneThemeOverride", NewTheme(owned, true, Color.black, Color.white)); // serialized authored data
                Object.DestroyImmediate(destroyed);
                UIContainerUtilities.SetBackgroundGradientBinders(start, end, destroyed);
                Assert.That((start.Value, end.Value), Is.EqualTo((Color.gray, Color.cyan)), "a destroyed mission counts as missing");
            });
        }
    }
}
