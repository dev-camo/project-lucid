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
    // over the character select scene. Start is not run: it needs a live UINavigationManager, whose constructor reads the
    // navigation configuration, so the references it takes from the registry are assigned directly. The swap to a character
    // other than the current one (CharacterManager.SwapCharacter) is not covered. Start's navigation selection is not covered.
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

                Call(container, "UpdateCharacters");

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

                Call(container, "SetCharacter", CharacterId.Sonic);
                Call(container, "SetCharacter", CharacterId.Sonic);

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

                Call(container, "OnDestroy");

                Assert.That(scene.App.HasGameplayControl(), Is.True);
            });
        }
    }
}
