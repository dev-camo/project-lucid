using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AddressableAssets;
using static ProjectLucid.Tests.ChallengeRewardTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // UIWidgetButtonImageTexture (Game.Runtime 0x020009a1, x86-64 0x69AEB0..0x69B250) as a real component on an inactive owned host.
    // Its static AddressableManager reference is built by the type initializer, which may run during a case, so the AddressableManager
    // row is leased empty for every case. Start waits on that static reference and loads the sprite through the manager's Addressables
    // calls; it is not covered here. HLOutput errors are read through OwnedCoreErrorLog. The loading sprite is serialized authored data.
    public sealed class UIWidgetButtonImageTextureTests
    {
        private static UIWidgetButtonImageTexture NewWidget(Owned owned, out Sprite loadingImage)
        {
            owned.LeaseRow<AddressableManager>();
            var widget = owned.Host("Lucid texture image button", false).AddComponent<UIWidgetButtonImageTexture>();
            loadingImage = NewSprite(owned);
            Set(widget, "m_loadingImage", loadingImage);
            return widget;
        }

        // dump.cs TypeDefIndex 2464 and the DummyDll rows (fields 0x0400285b, 0x0400285c).
        [Test]
        public void DeclarationsMatchTheOriginalMetadata()
        {
            AssertMembers(typeof(UIWidgetButtonImageTexture), 0x00100101,
                new (string, int)[] { ("m_atlasedSpriteToLoad", 0x0001), ("s_addressableManagerRef", 0x0031) },
                new (string, int)[]
                {
                    ("Setup", 0x00c6), ("Start", 0x00c4), ("OnImageLoaded", 0x0081), (".ctor", 0x1886), (".cctor", 0x1891),
                    ("<Start>b__3_0", 0x0081),
                });
            AssertIl2CppOptions(typeof(UIWidgetButtonImageTexture), 2, 1);
            Assert.That(typeof(UIWidgetButtonImageTexture).BaseType, Is.EqualTo(typeof(UIWidgetButtonImageBase)));
            Assert.That(AttributeTypes(typeof(UIWidgetButtonImageTexture).GetField("m_atlasedSpriteToLoad", Declared)),
                Is.EqualTo(new[] { typeof(SerializeField) }));
        }

        // Setup 0x69AEB0: the base setup first (label and loading image), then the atlased sprite reference is taken from the parameters.
        [Test]
        public void SetupTakesTheAtlasedSpriteReference()
        {
            Owned.Run(owned =>
            {
                UIWidgetButtonImageTexture widget = NewWidget(owned, out Sprite loadingImage);
                var reference = new AssetReferenceAtlasedSprite("");

                widget.Setup(new UIWidgetButtonImageTextureParameters("Lucid label", null, reference));

                Assert.That(Get<AssetReferenceAtlasedSprite>(widget, "m_atlasedSpriteToLoad"), Is.SameAs(reference));
                Assert.That(widget.Label.Value, Is.EqualTo("Lucid label"));
                Assert.That(widget.Image.Value, Is.SameAs(loadingImage));
            });
        }

        // 0x69AF01..0x69AF55: parameters of another type fail the type test after the base setup (which reports its own two errors for
        // them): the widget logs and takes no reference.
        [Test]
        public void SetupWithTheWrongParametersOnlyLogs()
        {
            Owned.Run(owned =>
            {
                using (var log = new OwnedCoreErrorLog())
                {
                    UIWidgetButtonImageTexture widget = NewWidget(owned, out _);

                    widget.Setup(new UIWidgetImageArchetypeParameters(null, null));

                    Assert.That(log.Errors, Is.EqualTo(new[]
                    {
                        "Could not set up UIWidgetButton with given data.:0",
                        "Could not set up UIWidgetButtonImageBase with given data.:0",
                        "Could not set up UIWidgetButtonImageTexture with given data.:0",
                    }));
                    Assert.That(Get<AssetReferenceAtlasedSprite>(widget, "m_atlasedSpriteToLoad"), Is.Null);
                }
            });
        }

        // OnImageLoaded 0x69B060 publishes the loaded sprite through the Image bindable.
        [Test]
        public void ALoadedImageIsPublished()
        {
            Owned.Run(owned =>
            {
                UIWidgetButtonImageTexture widget = NewWidget(owned, out _);
                Sprite loaded = NewSprite(owned);

                Call(widget, "OnImageLoaded", loaded);

                Assert.That(widget.Image.Value, Is.SameAs(loaded));
            });
        }
    }
}
