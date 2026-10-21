using System;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;
using static ProjectLucid.Tests.ChallengeRewardTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // UIWidgetCutscene (Game.Runtime 0x020009b0, x86-64 0x69D830..0x69DB60) as a real component on an inactive owned host with a real
    // UI Button (the serialized button and loading sprite are authored data). The image is loaded from an addressable texture
    // reference through the AddressableManager's preloaded asset table (PreloadedAddressableSprites), as are all loads here; a load
    // that is not preloaded needs Addressables and is not covered.
    public sealed class UIWidgetCutsceneTests
    {
        private const string ValidKey = "0123456789abcdef0123456789abcdef";

        private static UIWidgetCutscene NewWidget(Owned owned, out Button button, out Sprite loadingImage)
        {
            GameObject host = owned.Host("Lucid cutscene button", false);
            button = host.AddComponent<Button>();
            var widget = host.AddComponent<UIWidgetCutscene>();
            Set(widget, "m_uiButton", button);
            loadingImage = NewSprite(owned);
            Set(widget, "m_loadingImage", loadingImage);
            return widget;
        }

        private static UIWidgetCutsceneParameters Parameters(bool locked, bool isNew, string key = "") =>
            new UIWidgetCutsceneParameters("Lucid cutscene", null, locked, new AssetReferenceT<Texture>(key), isNew);

        // dump.cs TypeDefIndex 2479 and the DummyDll row (field 0x04002896).
        [Test]
        public void DeclarationsMatchTheOriginalMetadata()
        {
            AssertMembers(typeof(UIWidgetCutscene), 0x00100101,
                new (string, int)[] { ("IsNew", 0x0026) },
                new (string, int)[]
                {
                    ("Start", 0x00c4), ("Setup", 0x00c6), ("LoadAddressableImage", 0x0081), ("OnImageLoaded", 0x0081),
                    ("Lock", 0x0081), ("SetIsNew", 0x0086), (".ctor", 0x1886),
                });
            AssertIl2CppOptions(typeof(UIWidgetCutscene), 1, 2);
            Assert.That(typeof(UIWidgetCutscene).BaseType, Is.EqualTo(typeof(UIWidgetButtonImageBase)));
        }

        // Setup 0x69D840 after the base setup: a locked entry (Lock 0x69D8F0 inlined) clears IsNew and disables the button and never
        // looks at its image; an unlocked one publishes IsNew from the parameters and enables the button. Truth table (locked, new).
        [TestCase(true, true, false, false, TestName = "ALockedNewEntryIsClearedAndDisabled")]
        [TestCase(true, false, false, false, TestName = "ALockedOldEntryIsDisabled")]
        [TestCase(false, true, true, true, TestName = "AnUnlockedNewEntryIsMarkedNewAndEnabled")]
        [TestCase(false, false, false, true, TestName = "AnUnlockedOldEntryIsEnabledButNotNew")]
        public void SetupLocksOrUnlocksTheButton(bool locked, bool isNew, bool expectedIsNew, bool expectedInteractable)
        {
            Owned.Run(owned =>
            {
                UIWidgetCutscene widget = NewWidget(owned, out Button button, out Sprite loadingImage);
                widget.IsNew.Value = !expectedIsNew;
                button.interactable = !expectedInteractable;

                widget.Setup(Parameters(locked, isNew));

                Assert.That(widget.IsNew.Value, Is.EqualTo(expectedIsNew));
                Assert.That(button.interactable, Is.EqualTo(expectedInteractable));
                Assert.That(widget.Label.Value, Is.EqualTo("Lucid cutscene"), "the base setup ran");
                Assert.That(widget.Image.Value, Is.SameAs(loadingImage), "no image is loaded for an invalid key");
            });
        }

        // LoadAddressableImage 0x69D930 tests the reference's key (virtual RuntimeKeyIsValid) and only then fetches the
        // AddressableManager (GetSystem, 0x69D9D5), which faults when there is none. IsNew and the button were already updated.
        [Test]
        public void AnUnlockedEntryWithAValidKeyAsksTheAddressableManagerForTheImage()
        {
            Owned.Run(owned =>
            {
                owned.LeaseRow<AddressableManager>();
                UIWidgetCutscene widget = NewWidget(owned, out Button button, out _);
                button.interactable = false;

                var fault = Assert.Throws<NullReferenceException>(() => widget.Setup(Parameters(false, true, ValidKey)));

                Assert.That(fault.Message, Is.EqualTo("System '" + ProcessManager.GetDefaultName<AddressableManager>() + "' is null"));
                Assert.That(widget.IsNew.Value, Is.True);
                Assert.That(button.interactable, Is.True);
            });
        }

        // LoadAddressableImage 0x69D930..0x69DA3B: with a valid key the manager's LoadAsset (0x69DA20) answers from the preloaded table,
        // OnImageLoaded publishes the sprite and the returned handle is stored in m_assetLoadHandle for the base to release.
        [Test]
        public void AnUnlockedEntryWithAPreloadedImageShowsItAndKeepsTheHandle()
        {
            PreloadedAddressableSprites.Run((owned, sprites) =>
            {
                UIWidgetCutscene widget = NewWidget(owned, out _, out _);
                AssetReferenceT<Sprite> reference = sprites.Preload(owned, out Sprite sprite);

                widget.Setup(new UIWidgetCutsceneParameters("Lucid cutscene", null, false, new AssetReferenceT<Texture>(reference.AssetGUID), false));

                Assert.That(widget.Image.Value, Is.SameAs(sprite));
                var handle = Get<AsyncOperationHandle<Sprite>>(widget, "m_assetLoadHandle");
                Assert.That(handle.IsValid(), Is.True);
                Assert.That(handle.Result, Is.SameAs(sprite));
            });
        }

        // A locked entry returns before the key is read, so a preloaded image is neither shown nor kept.
        [Test]
        public void ALockedEntryDoesNotShowOrKeepAPreloadedImage()
        {
            PreloadedAddressableSprites.Run((owned, sprites) =>
            {
                UIWidgetCutscene widget = NewWidget(owned, out _, out Sprite loadingImage);
                AssetReferenceT<Sprite> reference = sprites.Preload(owned, out _);

                widget.Setup(new UIWidgetCutsceneParameters("Lucid cutscene", null, true, new AssetReferenceT<Texture>(reference.AssetGUID), false));

                Assert.That(widget.Image.Value, Is.SameAs(loadingImage));
                Assert.That(Get<AsyncOperationHandle<Sprite>>(widget, "m_assetLoadHandle").IsValid(), Is.False);
            });
        }

        // The locked branch returns before the key is read, so even a valid key needs no manager.
        [Test]
        public void ALockedEntryNeverLoadsItsImage()
        {
            Owned.Run(owned =>
            {
                UIWidgetCutscene widget = NewWidget(owned, out _, out _);

                Assert.DoesNotThrow(() => widget.Setup(Parameters(true, false, ValidKey)));
            });
        }

        // SetIsNew 0x69DA70 and OnImageLoaded 0x69DA50 are plain bindable writes; Start 0x69D830 is a bare return.
        [Test]
        public void TheSmallMembersWriteTheirBindables()
        {
            Owned.Run(owned =>
            {
                UIWidgetCutscene widget = NewWidget(owned, out _, out _);
                Sprite loaded = NewSprite(owned);

                widget.SetIsNew(true);
                Call(widget, "OnImageLoaded", loaded);

                Assert.That(widget.IsNew.Value, Is.True);
                Assert.That(widget.Image.Value, Is.SameAs(loaded));
                widget.SetIsNew(false);
                Assert.That(widget.IsNew.Value, Is.False);
                Assert.DoesNotThrow(() => Call(widget, "Start"));
            });
        }
    }
}
