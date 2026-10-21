using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using static ProjectLucid.Tests.ChallengeRewardTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // UIWidgetButtonImageTexture (Game.Runtime 0x020009a1, x86-64 0x69AEB0..0x69B250) as a real component on an inactive owned host.
    // Its static AddressableManager reference is built by the type initializer, which may run during a case, so the AddressableManager
    // row is leased empty for every case. Start hands its load to that static reference only once the manager is registered; a case
    // cannot register into a row the static reference would still follow after the case releases it, so the queued load is reached
    // through its callback <Start>b__3_0 over a manager that answers from its preloaded asset table (PreloadedAddressableSprites), and
    // Start itself is covered only for the key gate. A load that is not preloaded needs Addressables and is not covered. HLOutput errors are read through OwnedCoreErrorLog. The loading sprite is serialized authored data.
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

        // <Start>b__3_0 (0x69B190, LoadAssetImmediate call at 0x69B220) loads the atlased sprite through the manager it is handed (LoadAssetImmediate answers from the
        // preloaded table), OnImageLoaded publishes it and the returned handle is stored in m_assetLoadHandle for the base to release.
        [Test]
        public void TheStartCallbackLoadsThePreloadedSpriteAndKeepsTheHandle()
        {
            PreloadedAddressableSprites.Run((owned, sprites) =>
            {
                UIWidgetButtonImageTexture widget = NewWidget(owned, out Sprite loadingImage);
                AssetReferenceT<Sprite> reference = sprites.Preload(owned, out Sprite sprite);
                Set(widget, "m_atlasedSpriteToLoad", new AssetReferenceAtlasedSprite(reference.AssetGUID));
                widget.Image.Value = loadingImage;

                Call(widget, "<Start>b__3_0", sprites.Manager);

                Assert.That(widget.Image.Value, Is.SameAs(sprite));
                var handle = Get<AsyncOperationHandle<Sprite>>(widget, "m_assetLoadHandle");
                Assert.That(handle.IsValid(), Is.True);
                Assert.That(handle.Result, Is.SameAs(sprite));
            }, registerManager: false);
        }

        // Start 0x69AF80 tests the reference's key (RuntimeKeyIsValid) before it touches the static manager reference: with an empty
        // key no callback is queued on it (a missing gate would queue one on the empty row) and the loading image stays.
        [Test]
        public void StartWithoutAValidKeyNeverQueuesALoad()
        {
            Owned.Run(owned =>
            {
                UIWidgetButtonImageTexture widget = NewWidget(owned, out Sprite loadingImage);
                Set(widget, "m_atlasedSpriteToLoad", new AssetReferenceAtlasedSprite(""));
                widget.Image.Value = loadingImage;

                Call(widget, "Start");

                object staticReference = Field(typeof(UIWidgetButtonImageTexture), "s_addressableManagerRef").GetValue(null);
                Assert.That(Get<object>(staticReference, "m_actionOnSystemValid"), Is.Null);
                Assert.That(widget.Image.Value, Is.SameAs(loadingImage));
                Assert.That(Get<AsyncOperationHandle<Sprite>>(widget, "m_assetLoadHandle").IsValid(), Is.False);
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
