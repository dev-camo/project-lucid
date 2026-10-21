using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.TestTools;
using UnityEngine.UI;
using static ProjectLucid.Tests.ChallengeRewardTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // UIContainerCutsceneTheatre (Game.Runtime 0x020008a6, x86-64 0x62A8A0..0x62B6F0, closures 0x020008a7 and 0x020008a8) as a real
    // component on an inactive owned host. Its collaborators are real: the App, DataManager and SaveManager (with an open save) of
    // OwnedCharacterManagers, a UIManager whose only container manager is the scripted one of the UI publishing fixtures (so the
    // fixture decides whether the theatre's container is the only one open), five cutscene definitions, two page buttons
    // (UIWidgetTogglePagination) and two cutscene widgets (UIWidgetCutscene). A page holds two cutscenes: the valid ones in the
    // dictionary's order are A, B, C, D and E. A and D are seen on the save, C on a level, B and E not at all. The serialized lists,
    // definitions and the Unity message Start are driven directly. Playing a cutscene constructs a CutsceneTheatre, so those cases also
    // own a real CutsceneManager as the CutsceneTheatre fixture does; the save request they make is refused by the owned coroutine host
    // and logs once. Not covered: the image load of an unlocked cutscene (a valid theatre image key needs Addressables).
    public sealed class UIContainerCutsceneTheatreTests
    {
        private static readonly Regex RefusedCoroutine = new Regex("Coroutine couldn't be started");

        private sealed class Rig
        {
            public Owned Owned;
            public UIContainerCutsceneTheatre Theatre;
            public UIWidgetTogglePagination[] Pages;
            public UIWidgetCutscene[] Widgets;
            public Button[] Buttons;
            public CutsceneDefinition[] Definitions; // A, B, C, D, E and the credits (not valid in the theatre) last
            public SaveManager Save;
            public App App;
            public ScriptedExclusivityContainerManager Containers;
            public UIContainerIdentifier Identifier;
            public int Selected;
        }

        private static void SetSerialized(Object target, string field, Action<SerializedProperty> assign)
        {
            var serialized = new SerializedObject(target);
            assign(serialized.FindProperty(field));
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static CutsceneDefinition Define(Rig rig, DataManager data, HLCutsceneIdentifier identifier, string name, bool valid)
        {
            var definition = rig.Owned.Track(ScriptableObject.CreateInstance<CutsceneDefinition>());
            definition.name = name;
            SetSerialized(definition, "m_guid", property => property.stringValue = "guid-" + name);
            SetSerialized(definition, "m_metadataFromCharacterPrefabs.AllComicCutscene", property => property.boolValue = valid);
            Set(definition, "m_theatreImage", new AssetReferenceT<Texture>("")); // Reflection: serialized reference with no image key
            data.CutsceneDefinitions[identifier] = definition;
            return definition;
        }

        private static UIWidgetTogglePagination NewPage(Owned owned)
        {
            GameObject host = owned.Host("Lucid theatre page", false);
            var widget = host.AddComponent<UIWidgetTogglePagination>();
            var toggle = host.GetComponent<UIToggle>();
            var element = host.AddComponent<UIToggleGroupElement>();
            var transition = host.AddComponent<UITransitionAnimation>();
            Set(transition, "m_settings", new List<UITransitionAnimationSetting>()); // Reflection: authored (empty) transition settings
            Set(toggle, "m_transition", transition); // Reflection: serialized reference
            Set(widget, "m_toggle", toggle); // Reflection: the editor Reset would fill this serialized reference
            Set(widget, "m_toggleGroupElement", element); // Reflection: serialized reference
            return widget;
        }

        private static UIWidgetCutscene NewCutsceneWidget(Owned owned, out Button button)
        {
            GameObject host = owned.Host("Lucid theatre cutscene button", false);
            button = host.AddComponent<Button>();
            var widget = host.AddComponent<UIWidgetCutscene>();
            Set(widget, "m_uiButton", button); // Reflection: serialized reference
            Set(widget, "m_loadingImage", NewSprite(owned)); // Reflection: serialized reference
            return widget;
        }

        // The CutsceneTheatre fixture's manager: its constructor registers itself and subscribes both actions, and its field
        // initialisers create these three rows.
        private static void AddCutsceneManager(Owned owned)
        {
            owned.LeaseActions(SystemAction.Initialise, SystemAction.Shutdown);
            owned.LeaseRow<FullscreenShaderManager>();
            owned.LeaseRow<VisualQualityManager_SDT>();
            owned.LeaseRow<CinemachineCameraManager>();
            var row = owned.Lease(new VerificationFixtureOwnership.RegistryRow(ProcessManager.GetDefaultName<CutsceneManager>()));
            row.Register(null);
            var manager = new CutsceneManager();
            owned.Lease(new CharacterTriggerTestSupport.ReleaseAction(() => ProcessManager.UnsubscribeFromAllActions(manager)));
            row.AdoptCurrent();
        }

        private static void Run(Action<Rig> body, bool withCutsceneManager = false) => Owned.Run(owned =>
        {
            // Taken first, so it is released last: it owns the App, DataManager and SaveManager rows.
            owned.Lease(new OwnedCharacterManagers());
            if (withCutsceneManager) AddCutsceneManager(owned);
            owned.LeaseRow<AudioManager>(); // CutsceneTheatre's field initialiser
            var rig = new Rig { Owned = owned, App = ProcessManager.GetSystem<App>(), Save = ProcessManager.GetSystem<SaveManager>() };
            DataManager data = ProcessManager.GetSystem<DataManager>();
            UiPublishingFixtures.NewUiManager(owned, out rig.Containers);
            rig.Definitions = new[]
            {
                Define(rig, data, HLCutsceneIdentifier.CS_Intro_A_2D, "A", true),
                Define(rig, data, HLCutsceneIdentifier.CS_Intro_B_2D, "B", true),
                Define(rig, data, HLCutsceneIdentifier.CS_Intro_C_2D, "C", true),
                Define(rig, data, HLCutsceneIdentifier.CS_Outro_A_2D, "D", true),
                Define(rig, data, HLCutsceneIdentifier.CS_Outro_B_2D, "E", true),
                Define(rig, data, HLCutsceneIdentifier.CS_End_Credits, "Credits", false),
            };
            SaveDataGame save = rig.Save.CurrentSave;
            save.AddSeenCutsceneGuid("guid-A");
            save.AddSeenCutsceneGuid("guid-D");
            save.AddSeenCutsceneGuid("guid-Credits");
            SaveDataLevel level = save.GetOrCreateLevelData("lucid-theatre-level");
            level.SeenCutsceneOnLevelGuids.Add("guid-C");
            Get<List<SaveDataLevel>>(save, "m_levels").Add(level); // the list the save serialises its levels from; the lookup alone does not fill it

            rig.Identifier = UiPublishingFixtures.NewIdentifier(owned, "theatre");
            rig.Theatre = owned.Host("Lucid cutscene theatre", false).AddComponent<UIContainerCutsceneTheatre>();
            Set(rig.Theatre, "m_identifier", rig.Identifier); // Reflection: serialized in the original
            rig.Pages = new[] { NewPage(owned), NewPage(owned), NewPage(owned) };
            rig.Buttons = new Button[2];
            rig.Widgets = new[] { NewCutsceneWidget(owned, out rig.Buttons[0]), NewCutsceneWidget(owned, out rig.Buttons[1]) };
            Set(rig.Theatre, "m_paginationWidgets", rig.Pages.ToList()); // Reflection: serialized list
            Set(rig.Theatre, "m_cutsceneWidgets", rig.Widgets.ToList()); // Reflection: serialized list
            body(rig);
        });

        private static void SelectPage(Rig rig, int page, bool selected = true) => rig.Pages[page].Action_OnValueChanged(selected);

        private static string[] Labels(Rig rig) => rig.Widgets.Select(widget => widget.Label.Value).ToArray();

        private static string[] Names(IEnumerable<CutsceneDefinition> definitions) => definitions.Select(definition => definition.name).ToArray();

        private static IReadOnlyList<CutsceneDefinition> PageContents(Rig rig, int startFrom) =>
            (IReadOnlyList<CutsceneDefinition>)Call(rig.Theatre, "GetCutscenesStartingFrom", startFrom);

        // dump.cs TypeDefIndex 2215/2213/2214 and the DummyDll rows (fields 0x0400224a..0x04002255, methods 0x060031ed..0x060031fa).
        [Test]
        public void DeclarationsMatchTheOriginalMetadata()
        {
            AssertMembers(typeof(UIContainerCutsceneTheatre), 0x00100101,
                new (string, int)[]
                {
                    ("m_paginationWidgets", 0x0001), ("m_cutsceneWidgets", 0x0001), ("m_dataManager", 0x0001), ("m_saveManager", 0x0001),
                    ("m_uiManager", 0x0001), ("m_cutsceneDefinitions", 0x0021), ("m_seenCutsceneGUIDs", 0x0021),
                },
                new (string, int)[]
                {
                    ("Start", 0x0081), ("CacheDefinitions", 0x0081), ("SetupPagination", 0x0081), ("SelectPage", 0x0081), ("LoadPage", 0x0081),
                    ("GetCutscenesStartingFrom", 0x0081), ("SetupWidgets", 0x0081), ("PlayCutscene", 0x0081), ("Action_PlayAll", 0x0086),
                    (".ctor", 0x1886),
                });
            AssertIl2CppOptions(typeof(UIContainerCutsceneTheatre), 2, 1);
            Assert.That(typeof(UIContainerCutsceneTheatre).BaseType, Is.EqualTo(typeof(UIContainer)));
            foreach (string field in new[] { "m_paginationWidgets", "m_cutsceneWidgets" })
                Assert.That(AttributeTypes(typeof(UIContainerCutsceneTheatre).GetField(field, Declared)), Is.EqualTo(new[] { typeof(SerializeField) }), field);

            Type pagination = typeof(UIContainerCutsceneTheatre).GetNestedType("<>c__DisplayClass9_0", BindingFlags.NonPublic);
            AssertMembers(pagination, 0x00100103,
                new (string, int)[] { ("localIndex", 0x0006), ("<>4__this", 0x0006) },
                new (string, int)[] { (".ctor", 0x1886), ("<SetupPagination>b__0", 0x0083) });
            Type widgets = typeof(UIContainerCutsceneTheatre).GetNestedType("<>c__DisplayClass13_0", BindingFlags.NonPublic);
            AssertMembers(widgets, 0x00100103,
                new (string, int)[] { ("widgetCutscene", 0x0006), ("cutsceneDefinition", 0x0006), ("<>4__this", 0x0006) },
                new (string, int)[] { (".ctor", 0x1886), ("<SetupWidgets>b__0", 0x0083) });
        }

        // Start 0x62A8A0 takes the three managers, caches the definitions and sets the page buttons up: every page button is visible,
        // not new and unlocked (UIWidgetTogglePaginationParameters constructor arguments 0x62AF4F..0x62AF5F: isNew 0, locked 0,
        // unlockSeen 1, isVisible 1).
        [Test]
        public void StartTakesTheManagersAndSetsUpEveryPageButton()
        {
            Run(rig =>
            {
                Call(rig.Theatre, "Start");

                Assert.That(Get<DataManager>(rig.Theatre, "m_dataManager"), Is.SameAs(ProcessManager.GetSystem<DataManager>()));
                Assert.That(Get<SaveManager>(rig.Theatre, "m_saveManager"), Is.SameAs(rig.Save));
                Assert.That(Get<UIManager>(rig.Theatre, "m_uiManager"), Is.SameAs(ProcessManager.GetSystem<UIManager>()));
                Assert.That(rig.Pages.Select(page => page.IsVisible.Value), Is.All.True);
                Assert.That(rig.Pages.Select(page => page.IsNew.Value), Is.All.False);
                foreach (UIWidgetTogglePagination page in rig.Pages)
                {
                    Call(page, "Refresh");
                    Assert.That(page.ToggleGroupElement.enabled, Is.True, "an unlocked page");
                }
            });
        }

        // The pages hold as many cutscenes as there are cutscene widgets (two): page n starts at the valid definition 2n, and the
        // credits (not valid in the theatre, 0x79F440 is its test) are never among them. Pure function of the cached definitions.
        [TestCase(0, "A,B", TestName = "TheFirstPageHoldsTheFirstTwoValidCutscenes")]
        [TestCase(1, "C,D", TestName = "TheSecondPageHoldsTheNextTwo")]
        [TestCase(2, "E", TestName = "TheLastPageHoldsTheRemainder")]
        [TestCase(3, "", TestName = "APagePastTheEndIsEmpty")]
        public void ThePagesSplitTheValidCutscenesByTheWidgetCount(int page, string expected)
        {
            Run(rig =>
            {
                Call(rig.Theatre, "Start");

                Assert.That(string.Join(",", Names(PageContents(rig, page))), Is.EqualTo(expected));
            });
        }

        // SetupWidgets 0x62B150 for the first page: a cutscene is locked until it is seen on the save (globally or on a level) and
        // is new until it has been played from the theatre; a locked widget is disabled and not new.
        [Test]
        public void SelectingThePageSetsUpItsWidgetsFromTheSave()
        {
            Run(rig =>
            {
                Call(rig.Theatre, "Start");

                SelectPage(rig, 0);

                Assert.That(Labels(rig), Is.EqualTo(new[] { "A", "B" }));
                Assert.That(rig.Buttons.Select(button => button.interactable), Is.EqualTo(new[] { true, false }), "B was never seen");
                Assert.That(rig.Widgets.Select(widget => widget.IsNew.Value), Is.EqualTo(new[] { true, false }), "A is unseen by the theatre; B is locked");
            });
        }

        [Test]
        public void ACutsceneSeenOnALevelIsUnlocked()
        {
            Run(rig =>
            {
                Call(rig.Theatre, "Start");

                SelectPage(rig, 1);

                Assert.That(Labels(rig), Is.EqualTo(new[] { "C", "D" }));
                Assert.That(rig.Buttons.Select(button => button.interactable), Is.EqualTo(new[] { true, true }));
            });
        }

        // A cutscene already played from the theatre (in the save's is-new list) is unlocked but not new.
        [Test]
        public void APlayedCutsceneIsNotNew()
        {
            Run(rig =>
            {
                rig.Save.CurrentSave.AddIsNewCutsceneGuid("guid-A");
                Call(rig.Theatre, "Start");

                SelectPage(rig, 0);

                Assert.That(rig.Buttons[0].interactable, Is.True);
                Assert.That(rig.Widgets[0].IsNew.Value, Is.False);
            });
        }

        // A page with fewer cutscenes than widgets leaves the remaining widgets as they were (the loop skips them, 0x62B3EE).
        [Test]
        public void AShortPageLeavesTheOtherWidgetsAsTheyWere()
        {
            Run(rig =>
            {
                Call(rig.Theatre, "Start");
                SelectPage(rig, 1);

                SelectPage(rig, 2);

                Assert.That(Labels(rig), Is.EqualTo(new[] { "E", "D" }));
                Assert.That(rig.Buttons[0].interactable, Is.False, "E was never seen");
                Assert.That(rig.Buttons[1].interactable, Is.True, "D keeps its state");
            });
        }

        // The page button's callback sets the page up only for a selection (test sil at 0x62B6BF).
        [Test]
        public void DeselectingAPageChangesNothing()
        {
            Run(rig =>
            {
                Call(rig.Theatre, "Start");
                SelectPage(rig, 0);

                SelectPage(rig, 1, selected: false);

                Assert.That(Labels(rig), Is.EqualTo(new[] { "A", "B" }));
            });
        }

        // LoadPage 0x62AFE0 sets the page up without a selection.
        [Test]
        public void LoadingAPageSetsItUp()
        {
            Run(rig =>
            {
                Call(rig.Theatre, "Start");

                Call(rig.Theatre, "LoadPage", 1);

                Assert.That(Labels(rig), Is.EqualTo(new[] { "C", "D" }));
            });
        }

        // PlayCutscene 0x62B4A0, from the cutscene widget's button, acts only while the theatre is the one open container
        // (UIManager.IsOnlyContainerOpen 0x62B4D9). Then the cutscene counts as played (AddIsNewCutsceneGuid 0x62B4F3), the save is
        // requested, the widget stops being new and a CutsceneTheatre queues the cutscene.
        [Test]
        public void PressingACutsceneWhileTheTheatreIsOpenQueuesItAndMarksItPlayed()
        {
            Run(rig =>
            {
                Call(rig.Theatre, "Start");
                SelectPage(rig, 0);
                rig.Containers.Open.Add(rig.Identifier);
                LogAssert.Expect(LogType.Error, RefusedCoroutine);

                rig.Widgets[0].Action_OnButtonPress();

                Assert.That(rig.Save.CurrentSave.CutsceneIsNewGuids, Is.EqualTo(new[] { "guid-A" }));
                Assert.That(rig.Widgets[0].IsNew.Value, Is.False);
                Assert.That(rig.App.Storage.TryGetValue(AppFSMKeys.CutsceneTheatre, out CutsceneTheatre theatre), Is.True);
                Assert.That(theatre.Peek(), Is.SameAs(rig.Definitions[0]));
            }, withCutsceneManager: true);
        }

        [Test]
        public void PressingACutsceneWhileAnotherContainerIsOpenDoesNothing()
        {
            Run(rig =>
            {
                Call(rig.Theatre, "Start");
                SelectPage(rig, 0);

                rig.Widgets[0].Action_OnButtonPress();

                Assert.That(rig.Save.CurrentSave.CutsceneIsNewGuids, Is.Empty);
                Assert.That(rig.Widgets[0].IsNew.Value, Is.True);
                Assert.That(rig.App.Storage.HasValue(AppFSMKeys.CutsceneTheatre), Is.False);
            });
        }

        // Action_PlayAll 0x62B550 likewise needs the theatre to be the only open container; then a CutsceneTheatre queues every seen
        // cutscene that is valid in the theatre, starting with A.
        [Test]
        public void PlayAllQueuesTheSeenCutscenesWhileTheTheatreIsOpen()
        {
            Run(rig =>
            {
                rig.Containers.Open.Add(rig.Identifier);

                rig.Theatre.Action_PlayAll();

                Assert.That(rig.App.Storage.TryGetValue(AppFSMKeys.CutsceneTheatre, out CutsceneTheatre theatre), Is.True);
                Assert.That(theatre.Peek(), Is.SameAs(rig.Definitions[0]));
                theatre.DequeueNext();
                Assert.That(theatre.Peek(), Is.SameAs(rig.Definitions[2]), "B was never seen and E neither");
                theatre.DequeueNext();
                Assert.That(theatre.Peek(), Is.SameAs(rig.Definitions[3]));
                theatre.DequeueNext();
                Assert.That(theatre.IsEmpty(), Is.True, "the credits are not valid in the theatre");
            }, withCutsceneManager: true);
        }

        [Test]
        public void PlayAllDoesNothingWhileAnotherContainerIsOpen()
        {
            Run(rig =>
            {
                rig.Theatre.Action_PlayAll();

                Assert.That(rig.App.Storage.HasValue(AppFSMKeys.CutsceneTheatre), Is.False);
            });
        }
    }
}
