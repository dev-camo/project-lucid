using System;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using static ProjectLucid.Tests.CharacterTriggerTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // UISettingsBackgroundImage (Game.Runtime 0x0200084b, Reset 0x60B9B0, Awake 0x60BA00, constructor 0x60BAB0) as a real
    // component on an owned inactive host (it requires, and so adds, an Image). Awake asks the CharacterManager whether
    // a character is in gameplay, which reads the App's metagame flag, so the services are the real ones of
    // CharacterTriggerTestSupport.WithManager.
    public sealed class UISettingsBackgroundImageTests
    {
        // The tint a fresh component holds: the constructor copies one 16-byte constant (0x29A7A80) into the colour,
        // whose floats read 0x3ECACACA, 0x3E58D8D8, 0x3F56D6D7 and 0x3F75F5F6.
        [Test]
        public void AFreshComponentHoldsTheAuthoredMainMenuTint()
        {
            Owned.Run(owned =>
            {
                var background = owned.Host("Lucid settings background", false).AddComponent<UISettingsBackgroundImage>();

                Color tint = Get<Color>(background, "m_backgroundImageColourOnMainMenu");

                Assert.That(BitConverter.SingleToInt32Bits(tint.r), Is.EqualTo(0x3ECACACA));
                Assert.That(BitConverter.SingleToInt32Bits(tint.g), Is.EqualTo(0x3E58D8D8));
                Assert.That(BitConverter.SingleToInt32Bits(tint.b), Is.EqualTo(0x3F56D6D7));
                Assert.That(BitConverter.SingleToInt32Bits(tint.a), Is.EqualTo(0x3F75F5F6));
            });
        }

        // Reset 0x60B9B0 stores the component's own Image (GetComponent<Image>).
        [Test]
        public void ResetFindsTheImageOnTheSameObject()
        {
            Owned.Run(owned =>
            {
                var background = owned.Host("Lucid settings background", false).AddComponent<UISettingsBackgroundImage>();
                Set(background, "m_backgroundImage", null); // Reflection: serialized m_backgroundImage the editor assigns

                Call(background, "Reset"); // Reflection: the editor's Reset is private and Edit Mode does not send it

                Assert.That(Get<Image>(background, "m_backgroundImage"), Is.SameAs(background.GetComponent<Image>()));
                Assert.That(Get<Image>(background, "m_backgroundImage"), Is.Not.Null);
            });
        }

        private static (UISettingsBackgroundImage background, Image image, Material material) NewBackground(Owned owned)
        {
            var background = owned.Host("Lucid settings background", false).AddComponent<UISettingsBackgroundImage>();
            Image image = background.GetComponent<Image>();
            Set(background, "m_backgroundImage", image); // serialized in the original
            Set(background, "m_backgroundImageColourOnMainMenu", new Color(0.25f, 0.5f, 0.75f, 0.125f)); // serialized in the original
            var material = owned.Track(new Material(Shader.Find("UI/Default")));
            image.material = material;
            image.color = Color.white;
            return (background, image, material);
        }

        // Awake 0x60BA00 with no character in gameplay: the image loses its own material (set_material(null), slot 33) and
        // takes the main menu tint (set_color, slot 23). With the metagame active IsCharacterGameplayActive is false
        // before it asks for a character.
        [Test]
        public void AwakeTintsTheBackgroundInTheMetaGame()
        {
            WithManager((owned, manager, character) =>
            {
                (UISettingsBackgroundImage background, Image image, Material material) = NewBackground(owned);
                ProcessManager.GetSystem<App>().Storage.SetValue(AppFSMKeys.MetaGameActive, true);
                SetCurrent(manager, character, true);

                Call(background, "Awake");

                Assert.That(image.material, Is.Not.SameAs(material));
                Assert.That(image.color, Is.EqualTo(new Color(0.25f, 0.5f, 0.75f, 0.125f)));
            });
        }

        [Test]
        public void AwakeTintsTheBackgroundWhenNoCharacterIsInGameplay()
        {
            WithManager((owned, manager, character) =>
            {
                (UISettingsBackgroundImage background, Image image, Material material) = NewBackground(owned);
                SetCurrent(manager, character, false);

                Call(background, "Awake");

                Assert.That(image.material, Is.Not.SameAs(material));
                Assert.That(image.color, Is.EqualTo(new Color(0.25f, 0.5f, 0.75f, 0.125f)));
            });
        }

        // With a valid character outside the metagame IsCharacterGameplayActive is true and Awake returns (0x60ba1a) before
        // reading the image.
        [Test]
        public void AwakeLeavesTheBackgroundAloneDuringGameplay()
        {
            WithManager((owned, manager, character) =>
            {
                (UISettingsBackgroundImage background, Image image, Material material) = NewBackground(owned);
                SetCurrent(manager, character, true);

                Call(background, "Awake");

                Assert.That(image.material, Is.SameAs(material));
                Assert.That(image.color, Is.EqualTo(Color.white));
            });
        }

        // A destroyed or missing image ends Awake (0x60ba61 compares it with null) without a fault.
        [Test]
        public void AwakeWithoutAnImageDoesNothing()
        {
            WithManager((owned, manager, character) =>
            {
                var background = owned.Host("Lucid settings background", false).AddComponent<UISettingsBackgroundImage>();
                Set(background, "m_backgroundImage", null); // Reflection: serialized m_backgroundImage the editor assigns
                SetCurrent(manager, character, false);

                Assert.DoesNotThrow(() => Call(background, "Awake"));
            });
        }
    }
}
