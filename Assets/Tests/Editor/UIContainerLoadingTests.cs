using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Hardlight;
using Hardlight.Enums;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AddressableAssets;
using static ProjectLucid.Tests.ChallengeRewardTestSupport;
using static ProjectLucid.Tests.UiWidgetImageFixtures;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // UIContainerLoading (Game.Runtime 0x020008d4, x86-64 0x63B120..0x63B3E0) as a real component on an inactive owned host over a
    // real GameplayLevelDefinition, MissionDefinition and UIContainerGameTips. The definitions never start an Addressables load: a
    // level without an image key answers its image request with null (0x79B4F0 is the original body), and a mission override image
    // is a wrapper that already holds its sprite, the state a finished load leaves. The serialized fields are authored data.
    // Not covered: the level image of a level with a valid key, which needs Addressables.
    public sealed class UIContainerLoadingTests : RandomStateFixture
    {
        private const string ValidKey = "0123456789abcdef0123456789abcdef";

        private static readonly Dictionary<Strings, string> TipTexts = new Dictionary<Strings, string>
        {
            { (Strings)0x401, "loading tip A" },
            { (Strings)0x402, "loading tip B" },
            { (Strings)0x403, "authored tip" },
        };

        private sealed class Rig
        {
            public Owned Owned;
            public UIContainerLoading Container;
            public Sprite DefaultImage;
            public GameplayLevelDefinition Level;
            public MissionDefinition Mission;
            public UIContainerGameTips Tips;
        }

        private static void Run(Action<Rig> body) => OwnedStringTable.Run(TipTexts, () => Owned.Run(owned =>
        {
            owned.LeaseRow<TimeTrialLeaderboardManager>(); // MissionDefinition's field initialisers
            owned.LeaseRow<SaveManager>();
            var rig = new Rig { Owned = owned };
            rig.Container = owned.Host("Lucid loading container", false).AddComponent<UIContainerLoading>();
            rig.DefaultImage = NewSprite(owned);
            Set(rig.Container, "m_defaultImage", rig.DefaultImage);
            rig.Tips = owned.Host("Lucid loading tips", false).AddComponent<UIContainerGameTips>();
            rig.Level = Create<GameplayLevelDefinition>(owned);
            Set(rig.Level, "m_guid", "lucid-loading-level");
            Set(rig.Level, "m_loadingImageReference", new AssetReferenceT<Sprite>("")); // no level image key
            rig.Mission = Create<MissionDefinition>(owned);
            Set(rig.Mission, "m_guid", "lucid-loading-mission");
            Set(rig.Mission, "m_overrideLoadingImage", new AssetReferenceT<Sprite>("")); // no override key
            body(rig);
        }));

        // A mission whose loading image override is a wrapper that already holds its sprite.
        private static Sprite GiveOverride(Rig rig)
        {
            Set(rig.Mission, "m_overrideLoadingImage", new AssetReferenceT<Sprite>(ValidKey));
            rig.Mission.LoadingImageOverride = LoadedWrapper(rig.Owned, out Sprite sprite);
            return sprite;
        }

        private static void GiveTips(Rig rig, params Strings[] tips)
        {
            var definition = Create<GameTipsDefinition>(rig.Owned);
            Set(definition, "m_tipIds", new List<Strings>(tips));
            Set(rig.Mission, "m_tipsOverride", definition);
        }

        // dump.cs TypeDefIndex 2259 and the DummyDll rows (fields 0x0400234c..0x04002350, methods 0x060032f5..0x060032f8).
        [Test]
        public void DeclarationsMatchTheOriginalMetadata()
        {
            AssertMembers(typeof(UIContainerLoading), 0x00100101,
                new (string, int)[]
                {
                    ("m_defaultImage", 0x0001), ("m_uiContainerGameTips", 0x0001), ("Image", 0x0026), ("m_levelDefinition", 0x0001),
                    ("m_missionDefinition", 0x0001),
                },
                new (string, int)[] { ("OnDisable", 0x0081), ("Setup", 0x00c6), (".ctor", 0x1886), ("<Setup>b__6_0", 0x0081) });
            AssertIl2CppOptions(typeof(UIContainerLoading), 2, 1);
            Assert.That(typeof(UIContainerLoading).BaseType, Is.EqualTo(typeof(UIContainer)));
            foreach (string field in new[] { "m_defaultImage", "m_uiContainerGameTips" })
                Assert.That(AttributeTypes(typeof(UIContainerLoading).GetField(field, Declared)), Is.EqualTo(new[] { typeof(SerializeField) }), field);
            Assert.That(AttributeTypes(typeof(UIContainerLoading).GetMethod("<Setup>b__6_0", Declared)),
                Is.EqualTo(new[] { typeof(CompilerGeneratedAttribute) }));
        }

        // Setup 0x63B170 calls the base setup, then GetAs: without parameters (the cast of null, test rax at 0x63B1F4) or without a
        // level (ScriptableObjectWithGuid.op_Equality 0x63B204) the default image is published and nothing else is touched.
        [Test]
        public void WithoutParametersTheDefaultImageIsShown()
        {
            Run(rig =>
            {
                rig.Container.Setup(null);

                Assert.That(rig.Container.Image.Value, Is.SameAs(rig.DefaultImage));
                Assert.That(Get<GameplayLevelDefinition>(rig.Container, "m_levelDefinition") == null, Is.True);
            });
        }

        [Test]
        public void WithoutALevelTheDefaultImageIsShownAndTheTipsAreLeftAlone()
        {
            Run(rig =>
            {
                Set(rig.Container, "m_uiContainerGameTips", rig.Tips); // Reflection: serialized reference
                GiveTips(rig, (Strings)0x401);

                rig.Container.Setup(new UIContainerLoadingParameters(null, rig.Mission));

                Assert.That(rig.Container.Image.Value, Is.SameAs(rig.DefaultImage));
                Assert.That(Get<List<Strings>>(rig.Tips, "m_shuffledTipIds"), Is.Empty, "the early return skips the tips (0x63B32A)");
                Assert.That(Get<MissionDefinition>(rig.Container, "m_missionDefinition") == null, Is.True);
            });
        }

        // A level without an image key: the level is remembered (0x63B23C), the level image request (LoadLevelImage 0x63B2CA) answers
        // with null for a missing key and the callback publishes it over whatever the image was.
        [Test]
        public void ALevelWithoutAnImageKeyPublishesItsNullImage()
        {
            Run(rig =>
            {
                rig.Container.Image.Value = rig.DefaultImage;

                rig.Container.Setup(new UIContainerLoadingParameters(rig.Level));

                Assert.That(rig.Container.Image.Value == null, Is.True);
                Assert.That(Get<GameplayLevelDefinition>(rig.Container, "m_levelDefinition"), Is.SameAs(rig.Level));
                Assert.That(Get<MissionDefinition>(rig.Container, "m_missionDefinition") == null, Is.True, "no mission was given");
            });
        }

        // The compiler-generated callback <Setup>b__6_0 0x63B3C0 publishes the sprite it is handed through the image binding.
        [Test]
        public void TheImageCallbackPublishesTheSprite()
        {
            Run(rig =>
            {
                Sprite sprite = NewSprite(rig.Owned);

                Call(rig.Container, "<Setup>b__6_0", sprite);

                Assert.That(rig.Container.Image.Value, Is.SameAs(sprite));
            });
        }

        // A mission without an override key (TryGetLoadingImageOverride 0x63B262 answers false) is not remembered and the level
        // image is requested instead.
        [Test]
        public void AMissionWithoutAnOverrideLeavesTheLevelImageInCharge()
        {
            Run(rig =>
            {
                rig.Container.Image.Value = rig.DefaultImage;

                rig.Container.Setup(new UIContainerLoadingParameters(rig.Level, rig.Mission));

                Assert.That(rig.Container.Image.Value == null, Is.True, "the level's (missing) image replaced the placeholder");
                Assert.That(Get<MissionDefinition>(rig.Container, "m_missionDefinition") == null, Is.True);
                Assert.That(Get<GameplayLevelDefinition>(rig.Container, "m_levelDefinition"), Is.SameAs(rig.Level));
            });
        }

        // A mission with an override: the mission is remembered (0x63B26F), its sprite is published at once (0x63B28D) and the level's
        // image is not requested.
        [Test]
        public void AMissionOverrideImageIsShownInsteadOfTheLevelImage()
        {
            Run(rig =>
            {
                Sprite overrideImage = GiveOverride(rig);

                rig.Container.Setup(new UIContainerLoadingParameters(rig.Level, rig.Mission));

                Assert.That(rig.Container.Image.Value, Is.SameAs(overrideImage), "the level's null image did not replace it");
                Assert.That(Get<MissionDefinition>(rig.Container, "m_missionDefinition"), Is.SameAs(rig.Mission));
                Assert.That(Get<GameplayLevelDefinition>(rig.Container, "m_levelDefinition"), Is.SameAs(rig.Level));
                Assert.That(Count(rig.Mission.LoadingImageOverride), Is.EqualTo(1));
            });
        }

        // The tips (0x63B2F2..0x63B325): shown only with a tips container, a mission and a mission that has tips
        // (MissionDefinition.TryGetTips 0x63B314); they are handed to the container, which shuffles them.
        [Test]
        public void TheMissionTipsAreHandedToTheTipsContainer()
        {
            Run(rig =>
            {
                Set(rig.Container, "m_uiContainerGameTips", rig.Tips); // Reflection: serialized reference
                GiveTips(rig, (Strings)0x401, (Strings)0x402);

                rig.Container.Setup(new UIContainerLoadingParameters(rig.Level, rig.Mission));

                Assert.That(Get<List<Strings>>(rig.Tips, "m_shuffledTipIds"), Is.EquivalentTo(new[] { (Strings)0x401, (Strings)0x402 }));
            });
        }

        [Test]
        public void TheTipsOfAnOverridingMissionAreStillHandedOver()
        {
            Run(rig =>
            {
                Set(rig.Container, "m_uiContainerGameTips", rig.Tips); // Reflection: serialized reference
                GiveOverride(rig);
                GiveTips(rig, (Strings)0x402);

                rig.Container.Setup(new UIContainerLoadingParameters(rig.Level, rig.Mission));

                Assert.That(Get<List<Strings>>(rig.Tips, "m_shuffledTipIds"), Is.EqualTo(new[] { (Strings)0x402 }));
            });
        }

        [Test]
        public void WithoutTipsInTheMissionOrAContainerNothingIsShuffled()
        {
            Run(rig =>
            {
                // No tips container assigned: the mission's tips go nowhere.
                GiveTips(rig, (Strings)0x401);
                Assert.DoesNotThrow(() => rig.Container.Setup(new UIContainerLoadingParameters(rig.Level, rig.Mission)));
                Assert.That(Get<List<Strings>>(rig.Tips, "m_shuffledTipIds"), Is.Empty);

                // A container, but a mission with no tips definition.
                Set(rig.Mission, "m_tipsOverride", null);
                Set(rig.Container, "m_uiContainerGameTips", rig.Tips);
                rig.Container.Setup(new UIContainerLoadingParameters(rig.Level, rig.Mission));
                Assert.That(Get<List<Strings>>(rig.Tips, "m_shuffledTipIds"), Is.Empty);
            });
        }

        // A level whose tips are empty does not count as having tips (TryGetTips needs a non-empty list).
        [Test]
        public void AnEmptyTipListIsNotShuffled()
        {
            Run(rig =>
            {
                Set(rig.Container, "m_uiContainerGameTips", rig.Tips); // Reflection: serialized reference
                GiveTips(rig);

                rig.Container.Setup(new UIContainerLoadingParameters(rig.Level, rig.Mission));

                Assert.That(Get<List<Strings>>(rig.Tips, "m_shuffledTipIds"), Is.Empty);
            });
        }

        // OnDisable 0x63B120 unloads the level image and the mission's override image, each only when remembered. The level's
        // wrapper stands for one an image request made; the mission's was loaded by Setup.
        [Test]
        public void DisablingUnloadsTheImagesOfTheRememberedDefinitions()
        {
            Run(rig =>
            {
                GiveOverride(rig);
                var levelWrapper = LoadedWrapper(rig.Owned, out _);
                levelWrapper.Load();
                Set(rig.Level, "m_managedLoadingImage", levelWrapper); // Reflection: stands for a wrapper an earlier image request made
                rig.Container.Setup(new UIContainerLoadingParameters(rig.Level, rig.Mission));
                Assert.That(Count(rig.Mission.LoadingImageOverride), Is.EqualTo(1), "set-up: the override image is held");
                Assert.That(Count(levelWrapper), Is.EqualTo(1), "set-up: the level image is held");

                Call(rig.Container, "OnDisable");

                Assert.That(Count(rig.Mission.LoadingImageOverride), Is.Zero);
                Assert.That(IsCached(rig.Mission.LoadingImageOverride), Is.False);
                Assert.That(Count(levelWrapper), Is.Zero);
                Assert.That(IsCached(levelWrapper), Is.False);
            });
        }

        [Test]
        public void DisablingBeforeAnySetupUnloadsNothing()
        {
            Run(rig => Assert.DoesNotThrow(() => Call(rig.Container, "OnDisable")));
        }
    }
}
