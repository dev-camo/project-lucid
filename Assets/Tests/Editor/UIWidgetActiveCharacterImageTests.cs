using System;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.CharacterSelectScene;
using static ProjectLucid.Tests.ChallengeRewardTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // UIWidgetActiveCharacterImage (Game.Runtime 02000999, x86-64 0x699180..0x699550) as a real component on an owned inactive host
    // over the character select scene: a registered CharacterManager whose current character is a real Character, and a DataManager
    // listing the character definitions. Each definition's portrait wrapper already holds its sprite, so no Addressables call is
    // made.
    public sealed class UIWidgetActiveCharacterImageTests
    {
        private sealed class Rig
        {
            public UIWidgetActiveCharacterImage Widget;
            public CharacterDefinition Sonic, Tails;
            public Sprite SonicImage, TailsImage;
        }

        private static CharacterDefinition WithPortrait(Scene scene, CharacterId id, out Sprite portrait)
        {
            CharacterDefinition definition = AddCharacter(scene, id);
            portrait = NewSprite(scene.Owned);
            definition.Image = new ManagedAddressableAsset<Sprite>(null);
            Set(definition.Image, "m_loadedAsset", portrait); // Reflection: the sprite an earlier Addressables load would hold
            return definition;
        }

        private static void Run(Action<Scene, Rig> body) => CharacterSelectScene.Run(scene =>
        {
            var rig = new Rig();
            rig.Sonic = WithPortrait(scene, CharacterId.Sonic, out rig.SonicImage);
            rig.Tails = WithPortrait(scene, CharacterId.Tails, out rig.TailsImage);
            rig.Widget = scene.Owned.Host("Lucid active character image", false).AddComponent<UIWidgetActiveCharacterImage>();
            body(scene, rig);
        });

        private static bool Listening(Scene scene, UIWidgetActiveCharacterImage widget)
        {
            var handlers = Get<Action<Character>>(scene.Characters, "m_onCharacterChange");
            if (handlers == null) return false;
            foreach (Delegate handler in handlers.GetInvocationList())
                if (ReferenceEquals(handler.Target, widget)) return true;
            return false;
        }

        // OnEnable 0x699190 waits for the character manager and OnCharacterManagerValid 0x6993B0 registers the widget's change callback with an
        // immediate call (0x699417 passes true), so the portrait of the current character is shown at once and retained.
        [Test]
        public void EnablingShowsTheCurrentCharactersPortrait()
        {
            Run((scene, rig) =>
            {
                MakeCurrent(scene, CharacterId.Sonic);

                Call(rig.Widget, "OnEnable"); // Reflection: Unity sends OnEnable; Edit Mode does not

                Assert.That(Listening(scene, rig.Widget), Is.True);
                Assert.That(rig.Widget.CharacterSprite.Value, Is.SameAs(rig.SonicImage));
                Assert.That(Get<CharacterDefinition>(rig.Widget, "m_characterDefinition"), Is.SameAs(rig.Sonic));
                Assert.That(Get<int>(rig.Sonic.Image, "m_refCount"), Is.EqualTo(1));
            });
        }

        // OnCharacterChange 0x699430 releases the previous portrait (0x6994BF), looks the new character up in the DataManager's
        // definitions (0x6994F2) and loads its portrait (0x699517).
        [Test]
        public void ANewCharacterReleasesTheOldPortraitAndShowsItsOwn()
        {
            Run((scene, rig) =>
            {
                MakeCurrent(scene, CharacterId.Sonic);
                Call(rig.Widget, "OnEnable"); // Reflection: Unity sends OnEnable; Edit Mode does not

                MakeCurrent(scene, CharacterId.Tails);
                Call(rig.Widget, "OnCharacterChange", scene.Character); // Reflection: private UIWidgetActiveCharacterImage.OnCharacterChange

                Assert.That(rig.Widget.CharacterSprite.Value, Is.SameAs(rig.TailsImage));
                Assert.That(Get<int>(rig.Sonic.Image, "m_refCount"), Is.Zero);
                Assert.That(Get<int>(rig.Tails.Image, "m_refCount"), Is.EqualTo(1));
                Assert.That(Get<CharacterDefinition>(rig.Widget, "m_characterDefinition"), Is.SameAs(rig.Tails));
            });
        }

        // A character the DataManager does not list leaves the shown sprite (0x6994F9 skips the load) but the lookup has already
        // overwritten the stored definition with null (TryGetValue's out argument is the field), so the old portrait is released
        // only once and the widget then holds nothing.
        [Test]
        public void AnUnlistedCharacterKeepsTheSpriteAndForgetsTheDefinition()
        {
            Run((scene, rig) =>
            {
                MakeCurrent(scene, CharacterId.Sonic);
                Call(rig.Widget, "OnEnable"); // Reflection: Unity sends OnEnable; Edit Mode does not

                MakeCurrent(scene, CharacterId.Rouge);
                Call(rig.Widget, "OnCharacterChange", scene.Character); // Reflection: private UIWidgetActiveCharacterImage.OnCharacterChange

                Assert.That(rig.Widget.CharacterSprite.Value, Is.SameAs(rig.SonicImage));
                Assert.That(Get<CharacterDefinition>(rig.Widget, "m_characterDefinition") == null, Is.True);
                Assert.That(Get<int>(rig.Sonic.Image, "m_refCount"), Is.Zero);
            });
        }

        // OnDisable 0x699220 releases the change callback (0x6992D5) and the shown portrait (0x699322).
        [Test]
        public void DisablingRemovesTheCallbackAndReleasesThePortrait()
        {
            Run((scene, rig) =>
            {
                MakeCurrent(scene, CharacterId.Tails);
                Call(rig.Widget, "OnEnable"); // Reflection: Unity sends OnEnable; Edit Mode does not
                Assert.That(Listening(scene, rig.Widget), Is.True);

                Call(rig.Widget, "OnDisable"); // Reflection: Unity sends OnDisable; Edit Mode does not

                Assert.That(Listening(scene, rig.Widget), Is.False);
                Assert.That(Get<int>(rig.Tails.Image, "m_refCount"), Is.Zero);
            });
        }

        // Without a character definition nothing is released (0x699301); UnloadImage 0x699340 releases the held portrait.
        [Test]
        public void UnloadImageReleasesTheHeldPortraitOnly()
        {
            Run((scene, rig) =>
            {
                Assert.DoesNotThrow(() => Call(rig.Widget, "UnloadImage"), "no definition yet"); // Reflection: private UIWidgetActiveCharacterImage.UnloadImage
                MakeCurrent(scene, CharacterId.Sonic);
                Call(rig.Widget, "OnEnable"); // Reflection: Unity sends OnEnable; Edit Mode does not

                Call(rig.Widget, "UnloadImage"); // Reflection: private UIWidgetActiveCharacterImage.UnloadImage

                Assert.That(Get<int>(rig.Sonic.Image, "m_refCount"), Is.Zero);
            });
        }

        // Start 0x699180 is an empty override of the abstract base.
        [Test]
        public void StartDoesNothing()
        {
            Run((scene, rig) =>
            {
                Call(rig.Widget, "Start"); // Reflection: Unity sends Start; Edit Mode does not

                Assert.That(rig.Widget.CharacterSprite.Value, Is.Null);
                Assert.That(Listening(scene, rig.Widget), Is.False);
            });
        }
    }
}
