using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.ChallengeRewardTestSupport;
using static ProjectLucid.Tests.UiWidgetImageFixtures;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // UIWidgetButtonImageCharacter (Game.Runtime 0x0200099f, x86-64 0x69AAA0..0x69AE40) as a real component on an inactive owned host over
    // an owned character definition whose Image wrapper already holds its sprite. Start and OnDestroy are called directly. The
    // serialized character and swap icon are authored data. HLOutput errors are read through OwnedCoreErrorLog.
    public sealed class UIWidgetButtonImageCharacterTests
    {
        private static UIWidgetButtonImageCharacter NewWidget(Owned owned, out GameObject swapIcon)
        {
            var widget = owned.Host("Lucid character image button", false).AddComponent<UIWidgetButtonImageCharacter>();
            swapIcon = owned.Host("Lucid swap icon", true);
            Set(widget, "m_swapIcon", swapIcon);
            return widget;
        }

        // dump.cs TypeDefIndex 2462 and the DummyDll rows (fields 0x04002856..0x04002858).
        [Test]
        public void DeclarationsMatchTheOriginalMetadata()
        {
            AssertMembers(typeof(UIWidgetButtonImageCharacter), 0x00100101,
                new (string, int)[] { ("m_character", 0x0001), ("m_swapIcon", 0x0001), ("<CharacterId>k__BackingField", 0x0001) },
                new (string, int)[]
                {
                    ("get_CharacterId", 0x0886), ("set_CharacterId", 0x0881), ("Setup", 0x00c6), ("Start", 0x00c4),
                    ("SetCharacterImage", 0x0081), ("OnDestroy", 0x0081), (".ctor", 0x1886),
                });
            AssertIl2CppOptions(typeof(UIWidgetButtonImageCharacter), 1, 2);
            Assert.That(typeof(UIWidgetButtonImageCharacter).BaseType, Is.EqualTo(typeof(UIWidgetButtonImageBase)));
            foreach (string field in new[] { "m_character", "m_swapIcon" })
                Assert.That(AttributeTypes(typeof(UIWidgetButtonImageCharacter).GetField(field, Declared)), Is.EqualTo(new[] { typeof(SerializeField) }), field);
        }

        // Setup 0x69AAC0: the base setup first, then the character, its id, the swap icon's visibility (from the parameters, only when
        // an icon is assigned) and the character's Image loaded into the Image bindable.
        [TestCase(true, TestName = "SetupShowsTheSwapIconWhenAsked")]
        [TestCase(false, TestName = "SetupHidesTheSwapIconWhenNotAsked")]
        public void SetupTakesTheCharacterAndItsImage(bool showSwapIcon)
        {
            Owned.Run(owned =>
            {
                UIWidgetButtonImageCharacter widget = NewWidget(owned, out GameObject swapIcon);
                swapIcon.SetActive(!showSwapIcon);
                CharacterDefinition character = NewCharacterDefinition(owned, CharacterId.Sonic, out Sprite image, out _);

                widget.Setup(new UIWidgetButtonCharacterImageParameters(character, showSwapIcon, null));

                Assert.That(Get<CharacterDefinition>(widget, "m_character"), Is.SameAs(character));
                Assert.That(widget.CharacterId, Is.EqualTo(CharacterId.Sonic));
                Assert.That(swapIcon.activeSelf, Is.EqualTo(showSwapIcon));
                Assert.That(widget.Image.Value, Is.SameAs(image));
                Assert.That(Count(character.Image), Is.EqualTo(1));
                Assert.That(widget.Label.Value, Is.EqualTo(string.Empty), "the base setup ran with the parameters' empty label");
            });
        }

        // 0x69ABB6..0x69ABC5: the swap icon is only touched when assigned.
        [Test]
        public void SetupWithoutASwapIconStillTakesTheCharacter()
        {
            Owned.Run(owned =>
            {
                UIWidgetButtonImageCharacter widget = NewWidget(owned, out _);
                Set(widget, "m_swapIcon", null);
                CharacterDefinition character = NewCharacterDefinition(owned, CharacterId.Amy, out Sprite image, out _);

                Assert.DoesNotThrow(() => widget.Setup(new UIWidgetButtonCharacterImageParameters(character, true, null)));

                Assert.That(widget.CharacterId, Is.EqualTo(CharacterId.Amy));
                Assert.That(widget.Image.Value, Is.SameAs(image));
            });
        }

        // 0x69AB1C..0x69AB79: parameters of another type fail the type test after the base setup (which reports its own two errors for
        // them): the widget logs and takes nothing.
        [Test]
        public void SetupWithTheWrongParametersOnlyLogs()
        {
            Owned.Run(owned =>
            {
                using (var log = new OwnedCoreErrorLog())
                {
                    UIWidgetButtonImageCharacter widget = NewWidget(owned, out GameObject swapIcon);

                    widget.Setup(new UIWidgetImageArchetypeParameters(null, null));

                    Assert.That(log.Errors, Is.EqualTo(new[]
                    {
                        "Could not set up UIWidgetButton with given data.:0",
                        "Could not set up UIWidgetButtonImageBase with given data.:0",
                        "Could not set up UIWidgetButtonImageCharacter with given data.:0",
                    }));
                    Assert.That(Get<CharacterDefinition>(widget, "m_character"), Is.Null);
                    Assert.That(widget.CharacterId, Is.EqualTo(CharacterId.None));
                    Assert.That(swapIcon.activeSelf, Is.True);
                    Assert.That(widget.Image.Value, Is.Null);
                }
            });
        }

        // Start 0x69ACE0 shows the character's Image (loading it once more); without a character it does nothing.
        [Test]
        public void StartLoadsTheAuthoredCharactersImage()
        {
            Owned.Run(owned =>
            {
                UIWidgetButtonImageCharacter widget = NewWidget(owned, out _);
                Assert.DoesNotThrow(() => Call(widget, "Start"), "no character yet");
                Assert.That(widget.Image.Value, Is.Null);

                CharacterDefinition character = NewCharacterDefinition(owned, CharacterId.Cream, out Sprite image, out _);
                Set(widget, "m_character", character);
                Call(widget, "Start");

                Assert.That(widget.Image.Value, Is.SameAs(image));
                Assert.That(Count(character.Image), Is.EqualTo(1));
            });
        }

        // OnDestroy 0x69AD60 unloads the character's Image (the matching release of the load); without a character it does nothing.
        [Test]
        public void DestroyUnloadsTheCharactersImage()
        {
            Owned.Run(owned =>
            {
                UIWidgetButtonImageCharacter widget = NewWidget(owned, out _);
                Assert.DoesNotThrow(() => Call(widget, "OnDestroy"), "no character");

                CharacterDefinition character = NewCharacterDefinition(owned, CharacterId.Knuckles, out _, out _);
                widget.Setup(new UIWidgetButtonCharacterImageParameters(character, false, null));
                Assert.That(Count(character.Image), Is.EqualTo(1));

                Call(widget, "OnDestroy");

                Assert.That(Count(character.Image), Is.EqualTo(0));
                Assert.That(IsCached(character.Image), Is.False);
            });
        }
    }
}
