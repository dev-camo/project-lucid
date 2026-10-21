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
    // Not covered: pressing a level button (it runs App.FastLoad and LevelManager.LoadGame, which queue application states), and
    // everything FastLoad.Continue does (it loads the mission through the mission manager's tracker prefabs and sets up the
    // meta game state); the hidden-group argument of MissionList.GetMissions(true) needs a hidden-requirement group.
    public sealed class FastLevelSelectAndFastLoadContainerTests
    {
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
            public GameObject Root;
        }

        // Three levels in one zone: the first and last are shown in level select (display names "First level" and "Third level"),
        // the one between is hidden.
        private static void WithPicker(Action<Owned, Picker> body) => LevelRequirementWorld.Run((owned, world) =>
            OwnedStringTable.Run(new Dictionary<Strings, string>
            {
                { Strings.CULTURE_NUMBER_MILLIONS, "First level" },
                { Strings.CULTURE_NUMBER_BILLIONS, "Third level" },
            }, () =>
            {
                var picker = new Picker { Levels = new GameplayLevelDefinition[3] };
                for (int i = 0; i < 3; i++)
                {
                    picker.Levels[i] = world.NewLevel("lucid-fast-level-" + i, false, (MissionGroup[])null);
                    bool shown = i != 1;
                    // Reflection through the engine serializer: authored level data.
                    JsonUtility.FromJsonOverwrite("{\"m_showInLevelSelect\":" + (shown ? "true" : "false") + ",\"m_displayName\":" +
                        (int)(i == 0 ? Strings.CULTURE_NUMBER_MILLIONS : Strings.CULTURE_NUMBER_BILLIONS) + "}", picker.Levels[i]);
                }
                picker.Zone = world.AddZone("lucid-fast-zone", picker.Levels);
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
        }

        // A level with two missions named "Mission one" and "Mission two" in one group, as the app's load parameters.
        private static void WithLoader(Action<Owned, Loader> body) => LevelRequirementWorld.Run((owned, world) =>
        {
            var loader = new Loader { Missions = new MissionDefinition[2] };
            for (int i = 0; i < 2; i++)
            {
                loader.Missions[i] = world.NewMission("lucid-fast-load-mission-" + i);
                loader.Missions[i].name = i == 0 ? "Mission one" : "Mission two";
            }
            loader.Level = world.NewLevel("lucid-fast-load-level", false, world.NewGroup("lucid-fast-load-group", loader.Missions));
            GameObject root = owned.Track(new GameObject("Lucid fast load list", typeof(RectTransform)));
            root.SetActive(false);
            loader.Root = (RectTransform)root.transform;
            loader.Container = owned.Host("Lucid fast load", false).AddComponent<UIContainerFastLoad>();
            Set(loader.Container, "m_rootTransform", loader.Root); // Reflection: serialized references
            Set(loader.Container, "m_buttonPrefab", owned.Host("Lucid fast load button", false).AddComponent<UIWidgetButton>());
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
    }
}
