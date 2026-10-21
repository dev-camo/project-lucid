using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Hardlight;
using Hardlight.Enums;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static ProjectLucid.Tests.ChallengeRewardTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // The two debug level pickers (Game.Runtime 020008b6/020008b7 UIContainerFastLevelSelect and 020008b8/020008b9 UIContainerFastLoad
    // with their closures; x86-64 0x62FFB0..0x630A8A and 0x630A90..0x63171E) over the level requirement world: a registered
    // LevelManager with an authored GameLevels, a real App (whose storage is a plain FSM storage) and a real MissionManager. The
    // buttons are clones of a real UIWidgetButton on an owned inactive host. ClearList destroys the old buttons with Destroy, which
    // Edit Mode refuses (one logged error each, expected below) and the objects stay under the owned list root.
    // Not covered: the hidden-group argument of MissionList.GetMissions(true) needs a hidden-requirement group. Pressing a level
    // button and FastLoad.Continue (with and without a mission) are observed through the app's plain FSM storage and the
    // mission manager, without running the queued application states.
    public sealed class FastLevelSelectAndFastLoadContainerTests
    {
        private static readonly Regex DefinitionAwakeFault = new Regex("^NullReferenceException: Object reference not set to an instance of an object$");
        private static readonly Regex RefusedCoroutine = new Regex("Coroutine couldn't be started");
        private static readonly Regex RefusedDestroy = new Regex("^Destroy may not be called from edit mode");
        private static void ExpectRefusedDestroys(int count)
        {
            for (int i = 0; i < count; i++) LogAssert.Expect(LogType.Error, RefusedDestroy);
        }

        private static string[] Labels(IEnumerable<UIWidgetButton> buttons) => buttons.Select(button => button.Label.Value).ToArray();

        private static List<UIWidgetButton> Buttons(UIContainerFastLevelSelect container) =>
            Get<List<UIWidgetButton>>(container, "m_buttons");

        private static Stack<GameplayLevelCategory> Stack(UIContainerFastLevelSelect container) =>
            Get<Stack<GameplayLevelCategory>>(container, "m_categoryStack");

        private sealed class Picker
        {
            public UIContainerFastLevelSelect Container;
            public GameplayLevelDefinition[] Levels;
            public GameplayLevelCategory Zone;
            public GameplayLevelDefinition OtherLevel;
            public GameObject Root;
        }

        // Three levels in one zone: the first and last are shown in level select (display names "First level" and "Third level"),
        // the one between is hidden. With a second zone, that zone holds one more shown level ("Other zone level").
        private static void WithPicker(Action<Owned, Picker> body, bool secondZone = false) => LevelRequirementWorld.Run((owned, world) =>
            OwnedStringTable.Run(new Dictionary<Strings, string>
            {
                { Strings.CULTURE_NUMBER_MILLIONS, "First level" },
                { Strings.CULTURE_NUMBER_BILLIONS, "Third level" },
                { Strings.CULTURE_NUMBER_THOUSANDS, "Other zone level" },
            }, () =>
            {
                var picker = new Picker { Levels = new GameplayLevelDefinition[3] };
                for (int i = 0; i < 3; i++)
                {
                    picker.Levels[i] = world.NewLevel("lucid-fast-level-" + i, false, (MissionGroup[])null);
                    bool shown = i != 1;
                    // Reflection through the engine serializer: authored level data.
                    JsonUtility.FromJsonOverwrite("{\"m_showInLevelSelect\":" + (shown ? "true" : "false") + ",\"m_displayName\":" +
                        (int)(i == 0 ? Strings.CULTURE_NUMBER_MILLIONS : Strings.CULTURE_NUMBER_BILLIONS) + ",\"m_exclusiveCharacters\":[]}", picker.Levels[i]);
                }
                picker.Zone = world.AddZone("lucid-fast-zone", picker.Levels);
                if (secondZone)
                {
                    picker.OtherLevel = world.NewLevel("lucid-fast-level-other", false, (MissionGroup[])null);
                    JsonUtility.FromJsonOverwrite("{\"m_showInLevelSelect\":true,\"m_displayName\":" + (int)Strings.CULTURE_NUMBER_THOUSANDS + "}", picker.OtherLevel);
                    world.AddZone("lucid-fast-zone-2", picker.OtherLevel);
                }
                picker.Root = owned.Host("Lucid fast level select list", false);
                picker.Container = owned.Host("Lucid fast level select", false).AddComponent<UIContainerFastLevelSelect>();
                Set(picker.Container, "m_listRoot", picker.Root); // Reflection: serialized references
                Set(picker.Container, "m_levelSelectPrefab", owned.Host("Lucid fast level select button", false).AddComponent<UIWidgetButton>());
                body(owned, picker);
            }));

        // dump.cs TypeDefIndex 2230 and 2229 and the DummyDll rows (fields 0x0400229f..0x040022a5).
        [Test]
        public void TheLevelSelectDeclarationsMatchTheOriginalMetadata()
        {
            AssertMembers(typeof(UIContainerFastLevelSelect), 0x00100101,
                new (string, int)[]
                {
                    ("m_listRoot", 0x0001), ("m_levelSelectPrefab", 0x0001), ("m_categoryStack", 0x0021), ("m_buttons", 0x0021),
                    ("m_levelManagerRef", 0x0021),
                },
                new (string, int)[]
                {
                    ("Start", 0x0081), ("ClearList", 0x0081), ("ShowSelectableLevels", 0x0081), ("AddBackButton", 0x0081),
                    ("OnUISelection", 0x0081), ("OnBackButton", 0x0081), (".ctor", 0x1886),
                });
            AssertIl2CppOptions(typeof(UIContainerFastLevelSelect), 1, 2);
            Assert.That(typeof(UIContainerFastLevelSelect).BaseType, Is.EqualTo(typeof(UIContainer)));
            Type closure = typeof(UIContainerFastLevelSelect).GetNestedType("<>c__DisplayClass7_0", BindingFlags.NonPublic);
            Assert.That(closure, Is.Not.Null, "the closure keeps the original compiler name");
            AssertMembers(closure, 0x00100103,
                new (string, int)[] { ("levelDefinition", 0x0006), ("<>4__this", 0x0006) },
                new (string, int)[] { (".ctor", 0x1886), ("<ShowSelectableLevels>b__0", 0x0083) });
        }

        // dump.cs TypeDefIndex 2232 and 2231 and the DummyDll rows (fields 0x040022a6..0x040022ae); Continue's second parameter is an
        // optional null.
        [Test]
        public void TheFastLoadDeclarationsMatchTheOriginalMetadata()
        {
            AssertMembers(typeof(UIContainerFastLoad), 0x00100101,
                new (string, int)[]
                {
                    ("m_rootTransform", 0x0001), ("m_buttonPrefab", 0x0001), ("m_continueEvent", 0x0001), ("m_zone5", 0x0001),
                    ("m_appRef", 0x0021), ("m_missionManagerRef", 0x0021),
                },
                new (string, int)[]
                {
                    ("Start", 0x0081), ("FindLevelDefinition", 0x0081), ("PopulateMissions", 0x0081), ("CreateButton", 0x0081),
                    ("Continue", 0x0081), (".ctor", 0x1886),
                });
            AssertIl2CppOptions(typeof(UIContainerFastLoad), 2, 1);
            Assert.That(typeof(UIContainerFastLoad).BaseType, Is.EqualTo(typeof(UIContainer)));
            ParameterInfo mission = typeof(UIContainerFastLoad).GetMethod("Continue", BindingFlags.Instance | BindingFlags.NonPublic).GetParameters()[1];
            Assert.That(mission.IsOptional, Is.True);
            Assert.That(mission.DefaultValue, Is.Null);
            Type closure = typeof(UIContainerFastLoad).GetNestedType("<>c__DisplayClass9_0", BindingFlags.NonPublic);
            Assert.That(closure, Is.Not.Null, "the closure keeps the original compiler name");
            AssertMembers(closure, 0x00100103,
                new (string, int)[] { ("<>4__this", 0x0006), ("levelDefinition", 0x0006), ("missionDefinition", 0x0006) },
                new (string, int)[] { (".ctor", 0x1886), ("<CreateButton>b__0", 0x0083) });
        }

        // 0x62FFB0 (Start tail-calls ShowSelectableLevels with a null category) and 0x62FFC0: the list is every level of the game
        // (the parameterless GetLevels, 0x797F50), one button per level that is shown in level select (+0x2C), labelled with the
        // string-table text of its display name (+0x20). The hidden level sits between two shown ones, so a loop that stops at it
        // loses the last button. No Back button on the first list, and the (null) category is pushed.
        [Test]
        public void StartListsTheLevelsShownInLevelSelect()
        {
            WithPicker((owned, picker) =>
            {
                Call(picker.Container, "Start");

                Assert.That(Labels(Buttons(picker.Container)), Is.EqualTo(new[] { "First level", "Third level" }));
                Assert.That(picker.Root.GetComponentsInChildren<UIWidgetButton>(true).Length, Is.EqualTo(2), "the buttons are under the list root");
                Assert.That(Stack(picker.Container).ToArray(), Is.EqualTo(new GameplayLevelCategory[] { null }));
            });
        }

        // 0x6300B7..0x6300C0 calls the parameterless GetLevels (0x06001812), not GetLevels(category) (0x06001813): selecting the
        // first zone still lists the level of the second zone, which GetLevels(zone) would drop.
        [Test]
        public void ShowingAZoneStillListsEveryLevelOfTheGame()
        {
            WithPicker((owned, picker) =>
            {
                Call(picker.Container, "Start");
                Assert.That(Labels(Buttons(picker.Container)), Is.EqualTo(new[] { "First level", "Third level", "Other zone level" }));
                ExpectRefusedDestroys(3);

                Call(picker.Container, "OnUISelection", picker.Zone);

                Assert.That(Labels(Buttons(picker.Container)), Is.EqualTo(new[] { "First level", "Third level", "Other zone level", "Back" }));
            }, secondZone: true);
        }

        // 0x630855..0x6308AF: a level runs App.FastLoad(level) then LevelManager.LoadGame(). FastLoad stores the level load
        // parameters, the current character (Sonic: no exclusive character) and IsFastLoading in the app storage; LoadGame adds the
        // load-game event to the state events once. Each button keeps its own level (the closure of 0x6301E3 is per iteration), and
        // the list and category stack are untouched.
        [Test]
        public void PressingALevelButtonFastLoadsThatLevelAndQueuesTheLoadGameEvent()
        {
            WithPicker((owned, picker) =>
            {
                App app = ProcessManager.GetSystem<App>();
                var events = new List<ApplicationStateEvent>();
                app.Storage.SetValue(AppFSMKeys.StateEvents, events);
                var loadGame = owned.Track(ScriptableObject.CreateInstance<ApplicationStateEvent>());
                Set(ProcessManager.GetSystem<LevelManager>(), "m_loadGameStartEvent", loadGame); // Reflection: serialized reference
                // LevelManager.Initialise takes the App through its SystemRef but also parents a DontDestroyOnLoad object that Edit Mode
                // cannot own, so the App it would take is assigned directly.
                Set(ProcessManager.GetSystem<LevelManager>(), "m_app", app); // Reflection: private field Initialise assigns
                Call(picker.Container, "Start");
                UIWidgetButton[] buttons = Buttons(picker.Container).ToArray();

                buttons[0].Action_OnButtonPress();
                Assert.That(app.Storage.GetValueOnly<LevelManagerLoadLevelParameters>(AppFSMKeys.LevelLoadParameters).LevelDefinition, Is.SameAs(picker.Levels[0]));

                buttons[1].Action_OnButtonPress();

                Assert.That(app.Storage.GetValueOnly<LevelManagerLoadLevelParameters>(AppFSMKeys.LevelLoadParameters).LevelDefinition, Is.SameAs(picker.Levels[2]));
                Assert.That(app.Storage.GetValueOnly<bool>(AppFSMKeys.IsFastLoading), Is.True);
                Assert.That(app.Storage.GetValueOnly<CharacterId>(AppFSMKeys.CurrentCharacterId), Is.EqualTo(CharacterId.Sonic));
                Assert.That(events, Is.EqualTo(new[] { loadGame }), "added once by both presses");
                Assert.That(Buttons(picker.Container).ToArray(), Is.EqualTo(buttons));
                Assert.That(Stack(picker.Container).ToArray(), Is.EqualTo(new GameplayLevelCategory[] { null }));
            });
        }

        // 0x62FFC0 pushes the category it was given after the list is built, whatever the list holds: a category only becomes
        // the stack entry. The second list gets a Back button because the stack is no longer empty (0x6305F0 tests its count).
        [Test]
        public void ShowingACategoryAddsABackButtonAndPushesIt()
        {
            WithPicker((owned, picker) =>
            {
                Call(picker.Container, "Start");
                ExpectRefusedDestroys(2);

                Call(picker.Container, "OnUISelection", picker.Zone);

                Assert.That(Labels(Buttons(picker.Container)), Is.EqualTo(new[] { "First level", "Third level", "Back" }));
                Assert.That(Stack(picker.Container).ToArray(), Is.EqualTo(new[] { picker.Zone, null }), "newest on top");
            });
        }

        // 0x6308D0: the first pop removes the list on show, the second the one before it, which is shown again (and pushed again).
        // From the zone list the back button returns to the null-category list, which has no Back button of its own.
        [Test]
        public void TheBackButtonShowsThePreviousList()
        {
            WithPicker((owned, picker) =>
            {
                Call(picker.Container, "Start");
                ExpectRefusedDestroys(2);
                Call(picker.Container, "OnUISelection", picker.Zone);
                UIWidgetButton back = Buttons(picker.Container).Last();
                Assert.That(back.Label.Value, Is.EqualTo("Back"));
                ExpectRefusedDestroys(3);

                back.Action_OnButtonPress();

                Assert.That(Labels(Buttons(picker.Container)), Is.EqualTo(new[] { "First level", "Third level" }));
                Assert.That(Stack(picker.Container).ToArray(), Is.EqualTo(new GameplayLevelCategory[] { null }));
            });
        }

        // 0x6308D0: a stack that holds only the current list pops it, finds nothing under it and shows the list for the null
        // category again; an empty stack does nothing.
        [Test]
        public void TheBackButtonOnTheFirstListShowsItAgainAndOnAnEmptyStackDoesNothing()
        {
            WithPicker((owned, picker) =>
            {
                Call(picker.Container, "OnBackButton");
                Assert.That(Buttons(picker.Container), Is.Empty);
                Assert.That(Stack(picker.Container), Is.Empty);

                Call(picker.Container, "Start");
                ExpectRefusedDestroys(2);
                Call(picker.Container, "OnBackButton");

                Assert.That(Labels(Buttons(picker.Container)), Is.EqualTo(new[] { "First level", "Third level" }));
                Assert.That(Stack(picker.Container).ToArray(), Is.EqualTo(new GameplayLevelCategory[] { null }));
            });
        }

        // 0x6307A0: a selection that is neither a level nor a category (or none) does nothing.
        [Test]
        public void ASelectionThatIsNotALevelOrACategoryIsIgnored()
        {
            WithPicker((owned, picker) =>
            {
                Call(picker.Container, "Start");
                UIWidgetButton[] before = Buttons(picker.Container).ToArray();

                Call(picker.Container, "OnUISelection", (object)null);
                Call(picker.Container, "OnUISelection", "not a level");
                Call(picker.Container, "OnUISelection", 7);

                Assert.That(Buttons(picker.Container).ToArray(), Is.EqualTo(before));
                Assert.That(Stack(picker.Container).Count, Is.EqualTo(1));
            });
        }

        // ---- UIContainerFastLoad ----

        private sealed class Loader
        {
            public UIContainerFastLoad Container;
            public RectTransform Root;
            public GameplayLevelDefinition Level;
            public MissionDefinition[] Missions;
            public GameplayLevelCategory Zone5;
            public GameplayLevelCategory OtherZone;
        }

        // CreateInstance sends Awake, whose ActorDefinition.UpdateCachedValues (0x7A8060) calls Settings.UpdateCachedValues on the
        // unassigned Settings with no null test: one logged fault per definition.
        private static CharacterDefinition NewCharacter(Owned owned, CharacterId id)
        {
            LogAssert.Expect(LogType.Exception, DefinitionAwakeFault);
            return Create<CharacterDefinition>(owned, "{\"m_id\":" + (int)id + "}");
        }

        // A level with two missions named "Mission one" and "Mission two" in one group, as the app's load parameters; without
        // missions the level has no mission list. The data manager lists Sonic then Amy, the order of the first allowed character.
        private static void WithLoader(Action<Owned, Loader> body, bool withMissions = true) => LevelRequirementWorld.Run((owned, world) =>
        {
            var loader = new Loader { Missions = new MissionDefinition[2] };
            for (int i = 0; i < 2; i++)
            {
                loader.Missions[i] = world.NewMission("lucid-fast-load-mission-" + i);
                loader.Missions[i].name = i == 0 ? "Mission one" : "Mission two";
            }
            loader.Level = withMissions
                ? world.NewLevel("lucid-fast-load-level", false, world.NewGroup("lucid-fast-load-group", loader.Missions))
                : world.NewLevel("lucid-fast-load-level", false, (MissionGroup[])null);
            var characters = ProcessManager.GetSystem<DataManager>().Characters;
            characters[CharacterId.Sonic] = NewCharacter(owned, CharacterId.Sonic);
            characters[CharacterId.Amy] = NewCharacter(owned, CharacterId.Amy);
            loader.Zone5 = world.AddZone("lucid-fast-load-zone5", loader.Level);
            loader.OtherZone = world.AddZone("lucid-fast-load-other-zone");
            GameObject root = owned.Track(new GameObject("Lucid fast load list", typeof(RectTransform)));
            root.SetActive(false);
            loader.Root = (RectTransform)root.transform;
            loader.Container = owned.Host("Lucid fast load", false).AddComponent<UIContainerFastLoad>();
            Set(loader.Container, "m_rootTransform", loader.Root); // Reflection: serialized references
            Set(loader.Container, "m_buttonPrefab", owned.Host("Lucid fast load button", false).AddComponent<UIWidgetButton>());
            Set(loader.Container, "m_continueEvent", owned.Track(ScriptableObject.CreateInstance<ApplicationStateEvent>()));
            Set(loader.Container, "m_zone5", loader.Zone5);
            body(owned, loader);
        });

        // The constructor takes the App and MissionManager references.
        [Test]
        public void ConstructionTakesTheAppAndMissionManagerReferences()
        {
            WithLoader((owned, loader) =>
            {
                Assert.That(Get<SystemRef<App>>(loader.Container, "m_appRef").Get(), Is.SameAs(ProcessManager.GetSystem<App>()));
                Assert.That(Get<SystemRef<MissionManager>>(loader.Container, "m_missionManagerRef").Get(), Is.SameAs(ProcessManager.GetSystem<MissionManager>()));
            });
        }

        // 0x630AB0: the level is the one in the app storage's level load parameters.
        [Test]
        public void TheLevelComesFromTheAppsLevelLoadParameters()
        {
            WithLoader((owned, loader) =>
            {
                ProcessManager.GetSystem<App>().Storage.SetValue(AppFSMKeys.LevelLoadParameters, new LevelManagerLoadLevelParameters(loader.Level));

                Assert.That(Call(loader.Container, "FindLevelDefinition"), Is.SameAs(loader.Level));
            });
        }

        // 0x630BD0 and 0x6314C0 (with the closure): a button per mission of the level's list, in order, labelled with the mission
        // asset's name, under the root transform. Start takes the level from the load parameters first.
        [Test]
        public void StartAddsAButtonPerMissionOfTheLoadingLevel()
        {
            WithLoader((owned, loader) =>
            {
                ProcessManager.GetSystem<App>().Storage.SetValue(AppFSMKeys.LevelLoadParameters, new LevelManagerLoadLevelParameters(loader.Level));

                Call(loader.Container, "Start");

                UIWidgetButton[] buttons = loader.Root.GetComponentsInChildren<UIWidgetButton>(true);
                Assert.That(Labels(buttons), Is.EqualTo(new[] { "Mission one", "Mission two" }));
            });
        }

        // 0x630EF0 with no mission list (PopulateMissions 0x630C4F..0x630C75 continues at once, without buttons): the meta game state
        // holds the level and no mission (key stored at 0x630F0..), LoadMission(null) loads nothing and the null context leaves no
        // active mission, the first character of the data manager (Sonic) is stored as both current and mission character, the
        // level of the zone-5 category sets BonusZoneActive (0x6312F0..0x6313CB), and the continue event is added once (AddUnique, 0x631491)
        // when Start runs twice.
        [Test]
        public void AMissionlessLevelContinuesAtOnceWithTheFirstCharacterAndTheBonusZone()
        {
            WithLoader((owned, loader) =>
            {
                App app = ProcessManager.GetSystem<App>();
                var events = new List<ApplicationStateEvent>();
                app.Storage.SetValue(AppFSMKeys.StateEvents, events);
                app.Storage.SetValue(AppFSMKeys.LevelLoadParameters, new LevelManagerLoadLevelParameters(loader.Level));

                Call(loader.Container, "Start");
                Call(loader.Container, "Start");

                Assert.That(loader.Root.GetComponentsInChildren<UIWidgetButton>(true), Is.Empty);
                var state = app.Storage.GetValueOnly<MetaGameState>(AppFSMKeys.MetaGameState);
                Assert.That(state.LevelDefinition, Is.SameAs(loader.Level));
                Assert.That(state.MissionDefinition, Is.Null);
                Assert.That(ProcessManager.GetSystem<MissionManager>().ActiveMissionContext, Is.Null);
                Assert.That(app.Storage.GetValueOnly<CharacterId>(AppFSMKeys.CurrentCharacterId), Is.EqualTo(CharacterId.Sonic));
                Assert.That(app.Storage.GetValueOnly<CharacterId>(AppFSMKeys.MissionCharacterId), Is.EqualTo(CharacterId.Sonic));
                Assert.That(app.Storage.GetValueOnly<bool>(AppFSMKeys.BonusZoneActive), Is.True);
                Assert.That(events, Is.EqualTo(new[] { Get<ApplicationStateEvent>(loader.Container, "m_continueEvent") }));
            }, withMissions: false);
        }

        // 0x6312F0..0x6313CB: only a level of the zone-5 category sets BonusZoneActive; with another category there, the key is
        // never written.
        [Test]
        public void ALevelOutsideTheZoneFiveCategoryLeavesTheBonusZoneUnset()
        {
            WithLoader((owned, loader) =>
            {
                App app = ProcessManager.GetSystem<App>();
                app.Storage.SetValue(AppFSMKeys.StateEvents, new List<ApplicationStateEvent>());
                app.Storage.SetValue(AppFSMKeys.LevelLoadParameters, new LevelManagerLoadLevelParameters(loader.Level));
                Set(loader.Container, "m_zone5", loader.OtherZone); // Reflection: serialized reference

                Call(loader.Container, "Start");

                Assert.That(app.Storage.TryGetValue(AppFSMKeys.BonusZoneActive, out bool _), Is.False);
                Assert.That(app.Storage.GetValueOnly<CharacterId>(AppFSMKeys.MissionCharacterId), Is.EqualTo(CharacterId.Sonic), "the rest still ran");
            }, withMissions: false);
        }

        // 0x6314C0 closure and 0x630EF0: a mission button continues with its own mission (not the last of the loop): the meta game
        // state holds it and the active context is a regular context for it. Fast loading is set, as App.FastLoad does, so the
        // archetype lock filter allows the characters.
        [Test]
        public void PressingAMissionButtonContinuesWithThatMission()
        {
            WithLoader((owned, loader) =>
            {
                owned.LeaseRow<ChallengeManager>(); // MissionContextRegular's field initialiser
                // The archetype lock filter of GetCharactersForActiveMission (0x5BA3A0) is the real CharacterManager's. The world
                // leased an idle CharacterManager row; the manager is built by its real constructor, whose RequestSave and
                // LoadStates coroutines the world's inactive coroutine host refuses, each with one logged engine error.
                LogAssert.Expect(LogType.Error, RefusedCoroutine);
                LogAssert.Expect(LogType.Error, RefusedCoroutine);
                var characterManager = new CharacterManager(() => { });
                owned.Lease(new CharacterTriggerTestSupport.ReleaseAction(() => ProcessManager.UnsubscribeFromAllActions(characterManager)));
                owned.Register(characterManager);
                App app = ProcessManager.GetSystem<App>();
                app.Storage.SetValue(AppFSMKeys.StateEvents, new List<ApplicationStateEvent>());
                app.Storage.SetValue(AppFSMKeys.IsFastLoading, true);
                app.Storage.SetValue(AppFSMKeys.LevelLoadParameters, new LevelManagerLoadLevelParameters(loader.Level));
                Call(loader.Container, "Start");
                UIWidgetButton[] buttons = loader.Root.GetComponentsInChildren<UIWidgetButton>(true);

                buttons[0].Action_OnButtonPress();

                var state = app.Storage.GetValueOnly<MetaGameState>(AppFSMKeys.MetaGameState);
                Assert.That(state.LevelDefinition, Is.SameAs(loader.Level));
                Assert.That(state.MissionDefinition, Is.SameAs(loader.Missions[0]));
                var context = ProcessManager.GetSystem<MissionManager>().ActiveMissionContext;
                Assert.That(context, Is.TypeOf<MissionContextRegular>());
                Assert.That(context.MissionDefinition, Is.SameAs(loader.Missions[0]));
                Assert.That(context.LevelDefinition, Is.SameAs(loader.Level));
            });
        }
    }
}
