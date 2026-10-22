using System.Collections.Generic;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.CharacterSelectScene;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // UIContainerCharacterSelect (Game.Runtime 0200088d, x86-64 0x620D10..0x621320) as a real component on an owned inactive host
    // over the character select scene. The cases other than Start assign the registry references Start would take directly. Start
    // runs in its own cases over a real UINavigationManager (NavigationManagerTestSupport, active or inactive), whose probe
    // bridge records the widget it selects. The swap to a character other than the current one (CharacterManager.SwapCharacter)
    // is not covered.
    public sealed class UIContainerCharacterSelectTests
    {
        private static UIContainerCharacterSelect NewContainer(Scene scene, out UIModernEvent continueEvent)
        {
            var container = scene.Owned.Host("Lucid character select", false).AddComponent<UIContainerCharacterSelect>();
            continueEvent = scene.Owned.Track(ScriptableObject.CreateInstance<UIModernEvent>());
            Set(container, "m_app", scene.App); // Reflection: Start takes these from the registry
            Set(container, "m_characterManager", scene.Characters);
            Set(container, "m_continueEvent", continueEvent); // Reflection: authored serialized field
            return container;
        }

        private static GameObject Entry(Scene scene, UIContainerCharacterSelect container, CharacterId id, bool active)
        {
            GameObject entry = scene.Owned.Host("Lucid character " + id, active);
            Get<SerializableDictionary<CharacterId, GameObject>>(container, "m_characterIdToGameObject")[id] = entry;
            return entry;
        }

        private static UIContainerCharacterSelect NewBareContainer(Scene scene) =>
            scene.Owned.Host("Lucid character select", false).AddComponent<UIContainerCharacterSelect>();

        // A character entry with the navigation component the container's navigable widgets list holds.
        private static UIWidgetNavigation EntryWidget(Scene scene, UIContainerCharacterSelect container, CharacterId id, out GameObject entry)
        {
            entry = Entry(scene, container, id, true);
            return entry.AddComponent<UIWidgetNavigation>();
        }

        private static void ListWidgets(Scene scene, UIContainerCharacterSelect container, params UIWidgetNavigation[] widgets)
        {
            var navigation = scene.Owned.Host("Lucid character navigation", false).AddComponent<UIContainerNavigation>();
            Set(navigation, "m_navigableWidgets", widgets); // Reflection: serialized list the editor assigns
            Set(container, "m_uiContainerNavigation", navigation); // Reflection: serialized reference the editor assigns
        }

        // Start 0x620D10 takes the App (GetSystem 0x620DAD), the CharacterManager (0x620DD3) and the navigation manager (0x620DFC) from the
        // registry, takes gameplay control away (SetHasGameplayControl(false) 0x620E0E), updates the characters (0x620E16) and, with
        // navigation active (IsActive tested at 0x620E1F) and a current character (TryGetCurrentCharacter 0x620E33), selects the
        // navigable widget on that character's entry (get_Item 0x620E58, the object compare at 0x620EBE, Select 0x620ED0).
        [Test]
        public void StartTakesGameplayControlShowsTheCharactersAndSelectsTheCurrentCharactersWidget()
        {
            Run(scene =>
            {
                NavigationManagerTestSupport.Navigation navigation = NavigationManagerTestSupport.Add(scene.Owned);
                UIContainerCharacterSelect container = NewBareContainer(scene);
                AddCharacter(scene, CharacterId.Sonic);
                AddCharacter(scene, CharacterId.Tails);
                MakeCurrent(scene, CharacterId.Sonic);
                UIWidgetNavigation tailsWidget = EntryWidget(scene, container, CharacterId.Tails, out _);
                UIWidgetNavigation sonicWidget = EntryWidget(scene, container, CharacterId.Sonic, out GameObject sonic);
                GameObject amy = Entry(scene, container, CharacterId.Amy, true);
                ListWidgets(scene, container, tailsWidget, sonicWidget);
                scene.App.SetHasGameplayControl(true, true);

                Call(container, "Start"); // Reflection: Unity sends Start; Edit Mode does not

                Assert.That(scene.App.HasGameplayControl(), Is.False);
                Assert.That(Get<App>(container, "m_app"), Is.SameAs(scene.App));
                Assert.That(Get<CharacterManager>(container, "m_characterManager"), Is.SameAs(scene.Characters));
                Assert.That(amy.activeSelf, Is.False, "the characters were updated");
                Assert.That(navigation.Bridge.Selected, Is.EqualTo(new[] { sonic }));
                Assert.That(sonicWidget.IsSelected, Is.True);
                Assert.That(tailsWidget.IsSelected, Is.False);
            });
        }

        // The navigation selection needs active navigation (0x620E1F); without it gameplay control is still taken and the
        // characters still updated, and no widget is selected.
        [Test]
        public void StartSelectsNoWidgetWhileNavigationIsInactive()
        {
            Run(scene =>
            {
                NavigationManagerTestSupport.Navigation navigation = NavigationManagerTestSupport.Add(scene.Owned, false);
                UIContainerCharacterSelect container = NewBareContainer(scene);
                AddCharacter(scene, CharacterId.Sonic);
                MakeCurrent(scene, CharacterId.Sonic);
                UIWidgetNavigation sonicWidget = EntryWidget(scene, container, CharacterId.Sonic, out _);
                GameObject amy = Entry(scene, container, CharacterId.Amy, true);
                ListWidgets(scene, container, sonicWidget);
                scene.App.SetHasGameplayControl(true, true);

                Call(container, "Start"); // Reflection: Unity sends Start; Edit Mode does not

                Assert.That(scene.App.HasGameplayControl(), Is.False);
                Assert.That(amy.activeSelf, Is.False);
                Assert.That(navigation.Bridge.Selected, Is.Empty);
                Assert.That(sonicWidget.IsSelected, Is.False);
            });
        }

        // With no current character (TryGetCurrentCharacter false at 0x620E3A) nothing is looked up or selected.
        [Test]
        public void StartSelectsNoWidgetWithoutACurrentCharacter()
        {
            Run(scene =>
            {
                NavigationManagerTestSupport.Navigation navigation = NavigationManagerTestSupport.Add(scene.Owned);
                UIContainerCharacterSelect container = NewBareContainer(scene);
                AddCharacter(scene, CharacterId.Sonic);
                UIWidgetNavigation sonicWidget = EntryWidget(scene, container, CharacterId.Sonic, out _);
                ListWidgets(scene, container, sonicWidget);
                Assert.That(scene.Characters.TryGetCurrentCharacter(out _), Is.False);

                Call(container, "Start"); // Reflection: Unity sends Start; Edit Mode does not

                Assert.That(navigation.Bridge.Selected, Is.Empty);
                Assert.That(sonicWidget.IsSelected, Is.False);
            });
        }

        // UpdateCharacters 0x620EF0 fills the id list from the active mission (0x620FC4: with no mission, every listed
        // character) and sets each entry active exactly when its id is in that list (0x62111A..0x621127).
        [Test]
        public void OnlyTheMissionsCharactersAreShown()
        {
            Run(scene =>
            {
                UIContainerCharacterSelect container = NewContainer(scene, out _);
                AddCharacter(scene, CharacterId.Sonic);
                AddCharacter(scene, CharacterId.Tails);
                GameObject sonic = Entry(scene, container, CharacterId.Sonic, false);
                GameObject tails = Entry(scene, container, CharacterId.Tails, false);
                GameObject amy = Entry(scene, container, CharacterId.Amy, true);

                Call(container, "UpdateCharacters"); // Reflection: private UIContainerCharacterSelect.UpdateCharacters

                Assert.That(Get<List<CharacterId>>(container, "m_characterIds"), Is.EquivalentTo(new[] { CharacterId.Sonic, CharacterId.Tails }));
                Assert.That(sonic.activeSelf, Is.True);
                Assert.That(tails.activeSelf, Is.True);
                Assert.That(amy.activeSelf, Is.False, "an entry whose character the mission does not list is hidden");
            });
        }

        // SetCharacter 0x6212B0: the current character is kept (0x6212E0) and the continue event is added once (0x621303),
        // however often it is chosen.
        [Test]
        public void ChoosingTheCurrentCharacterOnlyPublishesTheContinueEventOnce()
        {
            Run(scene =>
            {
                UIContainerCharacterSelect container = NewContainer(scene, out UIModernEvent continueEvent);
                MakeCurrent(scene, CharacterId.Sonic);

                Call(container, "SetCharacter", CharacterId.Sonic); // Reflection: private UIContainerCharacterSelect.SetCharacter
                Call(container, "SetCharacter", CharacterId.Sonic); // Reflection: private UIContainerCharacterSelect.SetCharacter

                Assert.That(scene.App.GetUIModernEvents(), Is.EqualTo(new[] { continueEvent }));
                Assert.That(scene.Characters.GetCurrentCharacterUnsafe(), Is.SameAs(scene.Character));
            });
        }

        // Action_OnCharacterSelected 0x621240 reads the definition's id (+0x58) and runs the same body.
        [Test]
        public void SelectingADefinitionChoosesItsCharacter()
        {
            Run(scene =>
            {
                UIContainerCharacterSelect container = NewContainer(scene, out UIModernEvent continueEvent);
                CharacterDefinition sonic = AddCharacter(scene, CharacterId.Sonic);
                MakeCurrent(scene, CharacterId.Sonic);

                container.Action_OnCharacterSelected(sonic);

                Assert.That(scene.App.GetUIModernEvents(), Is.EqualTo(new[] { continueEvent }));
            });
        }

        // OnDestroy 0x621220 gives gameplay control back (SetHasGameplayControl(true) at 0x621228).
        [Test]
        public void DestroyingTheScreenGivesGameplayControlBack()
        {
            Run(scene =>
            {
                UIContainerCharacterSelect container = NewContainer(scene, out _);
                scene.App.SetHasGameplayControl(false);
                Assert.That(scene.App.HasGameplayControl(), Is.False);

                Call(container, "OnDestroy"); // Reflection: Unity sends OnDestroy; Edit Mode does not

                Assert.That(scene.App.HasGameplayControl(), Is.True);
            });
        }
    }
}
