using System;
using System.Linq;
using System.Reflection;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.ChallengeRewardTestSupport;
using static ProjectLucid.Tests.UiWidgetImageFixtures;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // UIWidgetImageArchetype (Game.Runtime 0x020009b4, x86-64 0x69DF10..0x69E220) as a real component on an inactive owned host over
    // owned archetype and character definitions whose image wrappers already hold their sprites. Setup and OnDestroy are called
    // directly (Edit Mode sends no OnDestroy to an inactive object). The serialized image type is authored data.
    public sealed class UIWidgetImageArchetypeTests
    {
        private static readonly Type ImageType = typeof(UIWidgetImageArchetype).GetNestedType("ImageType", BindingFlags.NonPublic);

        private static UIWidgetImageArchetype NewWidget(Owned owned, int imageType)
        {
            var widget = owned.Host("Lucid archetype image", false).AddComponent<UIWidgetImageArchetype>();
            Set(widget, "m_imageType", Enum.ToObject(ImageType, imageType));
            return widget;
        }

        // dump.cs TypeDefIndex 2484 / 2483 and the DummyDll rows (fields 0x0400289f..0x040028a3, 0x040028a4..0x040028a7).
        [Test]
        public void DeclarationsMatchTheOriginalMetadata()
        {
            AssertMembers(typeof(UIWidgetImageArchetype), 0x00100001,
                new (string, int)[]
                {
                    ("m_imageType", 0x0001), ("ArchetypeSprite", 0x0026), ("CharacterSprite", 0x0026),
                    ("<Archetype>k__BackingField", 0x0001), ("m_parameters", 0x0001),
                },
                new (string, int)[]
                {
                    ("get_Archetype", 0x0886), ("set_Archetype", 0x0881), ("Setup", 0x00c6), ("GetUIIcon", 0x0081),
                    ("OnDestroy", 0x0081), (".ctor", 0x1886),
                });
            AssertIl2CppOptions(typeof(UIWidgetImageArchetype), 2, 1);
            Assert.That(typeof(UIWidgetImageArchetype).BaseType, Is.EqualTo(typeof(UIWidget)));
            Assert.That(AttributeTypes(typeof(UIWidgetImageArchetype).GetField("m_imageType", Declared)), Is.EqualTo(new[] { typeof(SerializeField) }));

            Assert.That((int)ImageType.Attributes, Is.EqualTo(0x00000103), "NestedPrivate, Sealed");
            Assert.That(Enum.GetNames(ImageType), Is.EqualTo(new[] { "Normal", "Alternate", "Silhouette" }));
            Assert.That(Enum.GetValues(ImageType).Cast<int>().ToArray(), Is.EqualTo(new[] { 0, 1, 2 }));
            Assert.That(Enum.GetUnderlyingType(ImageType), Is.EqualTo(typeof(int)));
        }

        // The constructor 0x69E170 builds the two bindables (+0x28, +0x30) before the UIWidget constructor.
        [Test]
        public void ConstructorBuildsTwoEmptyIndependentBindables()
        {
            Owned.Run(owned =>
            {
                UIWidgetImageArchetype widget = NewWidget(owned, 0);

                Assert.That(widget.ArchetypeSprite, Is.Not.Null.And.Not.SameAs(widget.CharacterSprite));
                Assert.That(widget.CharacterSprite, Is.Not.Null);
                Assert.That(widget.ArchetypeSprite.Value, Is.Null);
                Assert.That(widget.CharacterSprite.Value, Is.Null);
                Assert.That(widget.Archetype, Is.EqualTo(CharacterArchetype.None));
            });
        }

        // 0x69DFBA..0x69E05E and GetUIIcon 0x69E070: the image type picks one of the archetype definition's three icon wrappers
        // (Normal +0x58, Alternate +0x60, Silhouette +0x68); the chosen wrapper is loaded once and the others are untouched. An image
        // type outside the enum loads nothing, yet the archetype is still recorded (+0x38).
        [TestCase(0, TestName = "TheNormalImageTypeLoadsTheNormalIcon")]
        [TestCase(1, TestName = "TheAlternateImageTypeLoadsTheAlternateIcon")]
        [TestCase(2, TestName = "TheSilhouetteImageTypeLoadsTheSilhouetteIcon")]
        [TestCase(3, TestName = "AnUndefinedImageTypeLoadsNoIcon")]
        public void TheImageTypeChoosesTheIconThatIsLoaded(int imageType)
        {
            Owned.Run(owned =>
            {
                UIWidgetImageArchetype widget = NewWidget(owned, imageType);
                CharacterArchetypeDefinition definition = NewArchetypeDefinition(owned, CharacterArchetype.Climbing,
                    out Sprite normal, out Sprite alternate, out Sprite silhouette);
                var wrappers = new[] { definition.UIImage, definition.AlternateImage, definition.SilhouetteImage };
                var sprites = new[] { normal, alternate, silhouette };

                widget.Setup(new UIWidgetImageArchetypeParameters(definition, null));

                Assert.That(widget.Archetype, Is.EqualTo(CharacterArchetype.Climbing));
                if (imageType < 3)
                    Assert.That(widget.ArchetypeSprite.Value, Is.SameAs(sprites[imageType]));
                else
                    Assert.That(widget.ArchetypeSprite.Value, Is.Null);
                Assert.That(wrappers.Select(wrapper => Count(wrapper)).ToArray(),
                    Is.EqualTo(Enumerable.Range(0, 3).Select(i => i == imageType ? 1 : 0).ToArray()));
                Assert.That(widget.CharacterSprite.Value, Is.Null);
            });
        }

        // 0x69DFBA / 0x69DFE8: an archetype definition takes precedence over a character definition (which is then never loaded);
        // with only a character definition its Render wrapper (+0x90) feeds CharacterSprite and the archetype stays None.
        [Test]
        public void AnArchetypeDefinitionWinsOverACharacterDefinition()
        {
            Owned.Run(owned =>
            {
                UIWidgetImageArchetype widget = NewWidget(owned, 0);
                CharacterArchetypeDefinition archetype = NewArchetypeDefinition(owned, CharacterArchetype.Dashing, out Sprite normal, out _, out _);
                CharacterDefinition character = NewCharacterDefinition(owned, CharacterId.Sonic, out _, out Sprite render);

                widget.Setup(new UIWidgetImageArchetypeParameters(archetype, character));
                Assert.That(widget.ArchetypeSprite.Value, Is.SameAs(normal));
                Assert.That(widget.CharacterSprite.Value, Is.Null);
                Assert.That(Count(character.Render), Is.EqualTo(0));

                widget.Setup(new UIWidgetImageArchetypeParameters(null, character));
                Assert.That(widget.Archetype, Is.EqualTo(CharacterArchetype.None), "Setup starts by clearing the archetype");
                Assert.That(widget.CharacterSprite.Value, Is.SameAs(render));
                Assert.That(Count(character.Render), Is.EqualTo(1));
            });
        }

        // With neither definition nothing is loaded and nothing is published.
        [Test]
        public void SetupWithNoDefinitionLoadsNothing()
        {
            Owned.Run(owned =>
            {
                UIWidgetImageArchetype widget = NewWidget(owned, 0);

                widget.Setup(new UIWidgetImageArchetypeParameters(null, null));

                Assert.That(widget.Archetype, Is.EqualTo(CharacterArchetype.None));
                Assert.That(widget.ArchetypeSprite.Value, Is.Null);
                Assert.That(widget.CharacterSprite.Value, Is.Null);
            });
        }

        // OnDestroy 0x69E0B0 releases what Setup loaded: the chosen archetype icon, or the character's Render wrapper when only a
        // character definition was given. Before any Setup (no parameters) it does nothing.
        [Test]
        public void DestroyUnloadsWhatSetupLoaded()
        {
            Owned.Run(owned =>
            {
                Assert.DoesNotThrow(() => Call(NewWidget(owned, 0), "OnDestroy"), "no Setup yet");

                UIWidgetImageArchetype widget = NewWidget(owned, 1);
                CharacterArchetypeDefinition archetype = NewArchetypeDefinition(owned, CharacterArchetype.Flying, out _, out _, out _);
                widget.Setup(new UIWidgetImageArchetypeParameters(archetype, null));
                Assert.That(Count(archetype.AlternateImage), Is.EqualTo(1));
                Call(widget, "OnDestroy");
                Assert.That(Count(archetype.AlternateImage), Is.EqualTo(0));
                Assert.That(IsCached(archetype.AlternateImage), Is.False, "the last release drops the cached asset");

                UIWidgetImageArchetype characterOnly = NewWidget(owned, 0);
                CharacterDefinition character = NewCharacterDefinition(owned, CharacterId.Amy, out _, out _);
                characterOnly.Setup(new UIWidgetImageArchetypeParameters(null, character));
                Assert.That(Count(character.Render), Is.EqualTo(1));
                Call(characterOnly, "OnDestroy");
                Assert.That(Count(character.Render), Is.EqualTo(0));
                Assert.That(IsCached(character.Render), Is.False);
            });
        }

        // With both definitions only the archetype icon was loaded, so only it is released; the character's wrapper is not touched
        // (a release of an unloaded wrapper would take its count below zero before clamping).
        [Test]
        public void DestroyWithBothDefinitionsReleasesOnlyTheArchetypeIcon()
        {
            Owned.Run(owned =>
            {
                UIWidgetImageArchetype widget = NewWidget(owned, 0);
                CharacterArchetypeDefinition archetype = NewArchetypeDefinition(owned, CharacterArchetype.Shadow, out _, out _, out _);
                CharacterDefinition character = NewCharacterDefinition(owned, CharacterId.Shadow, out _, out _);
                widget.Setup(new UIWidgetImageArchetypeParameters(archetype, character));

                Call(widget, "OnDestroy");

                Assert.That(Count(archetype.UIImage), Is.EqualTo(0));
                Assert.That(Count(character.Render), Is.EqualTo(0));
                Assert.That(IsCached(character.Render), Is.True, "the character's wrapper was never unloaded");
            });
        }
    }
}
