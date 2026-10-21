using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Hardlight;
using Hardlight.Enums;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static ProjectLucid.Tests.ChallengeRewardTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // ChallengeRewardGameFeature (x86-64 0x5503C0..0x550690, constructor 0x54F9D0), ChallengeRewardChallengeMode
    // (0x54F920, 0x54F980) and ChallengeRewardDreamPowers (0x54FFD0, 0x550030). Every reward is built by its real
    // constructor over real definition assets; the saving cases run over OwnedChallengeRewardServices.
    public sealed class ChallengeFeatureRewardTests
    {
        private static readonly Regex RefusedCoroutine = new Regex("Coroutine couldn't be started");
        // Strings values the cases author; the analytics name is the enum member's own name.
        private static readonly int NameValue = (int)Strings.MENU_TAILS_CHALLENGES_REWARD_TRACK_DREAM_POWER_STORE_TIER;
        private static readonly int DescriptionValue = (int)Strings.MENU_TAILS_CHALLENGES_REWARD_TRACK_MUSIC_TRACK;

        // dump.cs TypeDefIndex 1402, 1398 and 1400 with the DummyDll rows.
        [Test]
        public void DeclarationsMatchTheOriginalMetadata()
        {
            AssertMembers(typeof(ChallengeRewardGameFeature), 0x00100001,
                new (string, int)[]
                {
                    ("m_featureName", 0x0001),
                    ("m_featureDescription", 0x0001),
                    ("m_featureSprite", 0x0001),
                    ("m_spriteAsset", 0x0001),
                },
                new (string, int)[]
                {
                    ("get_RewardType", 0x08c6),
                    ("OnEnable", 0x0081),
                    ("Validate", 0x00c6),
                    ("GetAnalyticsName", 0x00c6),
                    ("GetRewardGUID", 0x00c6),
                    ("GiveReward", 0x00c6),
                    ("GetRewardName", 0x00c6),
                    ("LoadImage", 0x00c6),
                    ("UnloadImage", 0x00c6),
                    ("Save", 0x00c6),
                    ("GetMetaGameUnlock", 0x00c6),
                    (".ctor", 0x1886),
                });
            AssertMembers(typeof(ChallengeRewardChallengeMode), 0x00100001,
                new (string, int)[]
                {
                    ("m_cycleDefinition", 0x0001),
                },
                new (string, int)[]
                {
                    ("GetMetaGameUnlock", 0x00c6),
                    (".ctor", 0x1886),
                });
            AssertMembers(typeof(ChallengeRewardDreamPowers), 0x00100001,
                new (string, int)[]
                {
                    ("m_dreamPowerStoreDefinition", 0x0001),
                },
                new (string, int)[]
                {
                    ("GetMetaGameUnlock", 0x00c6),
                    (".ctor", 0x1886),
                });
            // Option 2 is ArrayBoundsChecks, 1 is NullChecks, in the DummyDll attribute order of each type.
            AssertIl2CppOptions(typeof(ChallengeRewardGameFeature), 2, 1);
            AssertIl2CppOptions(typeof(ChallengeRewardChallengeMode), 1, 2);
            AssertIl2CppOptions(typeof(ChallengeRewardDreamPowers), 1, 2);
            Assert.That(typeof(ChallengeRewardGameFeature).IsDefined(typeof(CreateAssetMenuAttribute), false), Is.False,
                "the base feature reward has no asset menu entry");
            Assert.That(typeof(ChallengeRewardChallengeMode).IsDefined(typeof(CreateAssetMenuAttribute), false), Is.True);
            Assert.That(typeof(ChallengeRewardDreamPowers).IsDefined(typeof(CreateAssetMenuAttribute), false), Is.True);
        }

        // get_RewardType 0x5503C0 returns 8; GetRewardName 0x550540 returns the feature name (+0x28); GetRewardGUID
        // 0x550520 returns the reward's own GUID (+0x18); GetAnalyticsName 0x550460 formats "{0}_{1}" with the virtual
        // reward type boxed and the feature name's string; GiveReward 0x550530 and Validate 0x550450 are lone
        // rets; GetMetaGameUnlock 0x550690 returns null (xor eax, eax) for every track.
        [Test]
        public void FeatureRewardAnswersFromItsAuthoredNameAndOwnGuid()
        {
            Owned.Run(owned =>
            {
                LeaseRewardRows(owned);
                var reward = NewReward<ChallengeRewardGameFeature>(owned,
                    "{\"m_guid\":\"lucid-feature-reward\",\"m_featureName\":" + NameValue + ",\"m_featureDescription\":" + DescriptionValue + "}");
                Assert.That((int)reward.RewardType, Is.EqualTo(8));
                Assert.That(reward.GetRewardName(), Is.EqualTo(Strings.MENU_TAILS_CHALLENGES_REWARD_TRACK_DREAM_POWER_STORE_TIER),
                    "the feature name, not the description");
                Assert.That(reward.GetRewardGUID(), Is.EqualTo("lucid-feature-reward"));
                Assert.That(reward.GetAnalyticsName(), Is.EqualTo("GameFeature_MENU_TAILS_CHALLENGES_REWARD_TRACK_DREAM_POWER_STORE_TIER"));
                Assert.DoesNotThrow(() => reward.Validate(null));
                Assert.DoesNotThrow(reward.GiveReward);
                foreach (RewardTrackType track in TrackTypes)
                    Assert.That(reward.GetMetaGameUnlock(track), Is.Null, track.ToString());
                Assert.That((int)Call(reward, "GetDefaultRewardSize"), Is.EqualTo(-915112498),
                    "the inherited Medium default (0xc9747dce); the feature reward does not override it");
            });
        }

        // OnEnable 0x5503D0 allocates a ManagedAddressableAsset<Sprite> over the feature sprite reference (+0x30) and
        // publishes it at +0x38; LoadImage 0x550550 calls Load on it and passes the sprite to the second (sprite)
        // callback through a tail call, never touching the texture callback; UnloadImage 0x5505B0 tail-calls Unload.
        [Test]
        public void FeatureRewardLoadsItsSpriteIntoTheSpriteCallbackAndUnloadsIt()
        {
            Owned.Run(owned =>
            {
                LeaseRewardRows(owned);
                var reward = NewReward<ChallengeRewardGameFeature>(owned);
                var reference = new UnityEngine.AddressableAssets.AssetReferenceAtlasedSprite("lucid-feature-sprite-guid");
                Set(reward, "m_featureSprite", reference);
                Call(reward, "OnEnable");
                var wrapper = Get<ManagedAddressableAsset<Sprite>>(reward, "m_spriteAsset");
                Assert.That(Get<object>(wrapper, "m_assetReference"), Is.SameAs(reference), "the wrapper captures the authored reference");
                Call(reward, "OnEnable");
                Assert.That(Get<ManagedAddressableAsset<Sprite>>(reward, "m_spriteAsset"), Is.Not.SameAs(wrapper), "each enable publishes a fresh wrapper");

                var current = Get<ManagedAddressableAsset<Sprite>>(reward, "m_spriteAsset");
                Sprite sprite = NewSprite(owned);
                Cache(current, sprite);
                var textures = new List<Texture>();
                var sprites = new List<Sprite>();
                reward.LoadImage(textures.Add, sprites.Add);
                Assert.That(sprites, Is.EqualTo(new[] { sprite }));
                Assert.That(textures, Is.Empty, "the texture callback is never used");
                Assert.That(Count(current), Is.EqualTo(1));
                reward.LoadImage(null, sprites.Add);
                Assert.That(sprites.Count, Is.EqualTo(2), "a null texture callback is not consulted");
                Assert.Throws<NullReferenceException>(() => reward.LoadImage(textures.Add, null), "the sprite callback has no null guard");
                Assert.That(Count(current), Is.EqualTo(3), "the load counts even when the callback faults");
                reward.UnloadImage();
                reward.UnloadImage();
                reward.UnloadImage();
                Assert.That(Count(current), Is.EqualTo(0));
                Assert.That(IsCached(current), Is.False);
            });
        }

        // Save 0x5505F0 reads the SaveManager through the reward base's static reference, takes the open save
        // (+0x80), looks the reward record up by this reward's own GUID (+0x18) as a track GUID
        // (GetRewardDataByTrackGUIDUnsafe), marks it collected and requests a save. A record reachable only by the
        // reward GUID, or one of another reward, is left alone.
        [Test]
        public void FeatureRewardSaveCollectsTheRecordWithItsOwnTrackGuid()
        {
            Owned.Run(owned =>
            {
                var services = owned.Lease(new OwnedChallengeRewardServices());
                var reward = NewReward<ChallengeRewardGameFeature>(owned, "{\"m_guid\":\"lucid-feature-track\"}");
                SaveDataChallengeReward own = services.AddRecord("lucid-feature-track", "lucid-feature-reward-guid");
                SaveDataChallengeReward other = services.AddRecord("lucid-other-track", "lucid-feature-track");
                SaveDataChallengeReward unrelated = services.AddRecord("lucid-unrelated-track", "lucid-unrelated-reward");
                Assert.That(own.Collected || other.Collected || unrelated.Collected, Is.False);

                LogAssert.Expect(LogType.Error, RefusedCoroutine);
                reward.Save();

                Assert.That(own.Collected, Is.True, "found by the track GUID");
                Assert.That(other.Collected, Is.False, "a record whose reward GUID equals the track GUID is not the one");
                Assert.That(unrelated.Collected, Is.False);
            });
        }

        // GetMetaGameUnlock 0x54F920 builds a MetaGameUnlockChallengesFeature over the authored cycle definition (+0x40)
        // and ignores the requested track; the unlock reads the cycle's widget, so a different cycle is a different
        // unlock.
        [Test]
        public void ChallengeModeRewardUnlocksTheChallengesFeatureOfItsCycle()
        {
            Owned.Run(owned =>
            {
                LeaseRewardRows(owned);
                var reward = NewReward<ChallengeRewardChallengeMode>(owned);
                var cycle = Create<ChallengeCycleDefinition>(owned);
                var widget = owned.Host("Lucid challenge widget", false).AddComponent<UIWidgetProgression>();
                Set(cycle, "m_progressionWidget", widget);
                Set(reward, "m_cycleDefinition", cycle);
                var unlocks = new List<IMetaGameUnlock>();
                foreach (RewardTrackType track in TrackTypes)
                    unlocks.Add(reward.GetMetaGameUnlock(track));
                foreach (IMetaGameUnlock unlock in unlocks)
                {
                    Assert.That(unlock, Is.TypeOf<MetaGameUnlockChallengesFeature>());
                    Assert.That(unlock.GetWidget(), Is.SameAs(widget));
                }
                Assert.That(unlocks[0], Is.Not.SameAs(unlocks[1]), "a new unlock per call");
                var otherCycle = Create<ChallengeCycleDefinition>(owned);
                Set(reward, "m_cycleDefinition", otherCycle);
                Assert.That(reward.GetMetaGameUnlock(RewardTrackType.Adventure).GetWidget(), Is.Null);
                // The inherited reward members stay the feature base's.
                Assert.That((int)reward.RewardType, Is.EqualTo(8));
                Assert.That(reward.GetAnalyticsName(), Does.StartWith("GameFeature_"));
            });
        }

        // GetMetaGameUnlock 0x54FFD0 builds a MetaGameUnlockDreamPowerStore over the authored store definition
        // (+0x40), ignoring the track; its identifier is that store's GUID.
        [Test]
        public void DreamPowersRewardUnlocksTheDreamPowerStoreItAuthors()
        {
            Owned.Run(owned =>
            {
                LeaseRewardRows(owned);
                var reward = NewReward<ChallengeRewardDreamPowers>(owned);
                var store = Create<DreamPowerStoreDefinition>(owned, "{\"m_guid\":\"lucid-powers-store\"}");
                var other = Create<DreamPowerStoreDefinition>(owned, "{\"m_guid\":\"lucid-other-store\"}");
                Set(reward, "m_dreamPowerStoreDefinition", store);
                foreach (RewardTrackType track in TrackTypes)
                {
                    IMetaGameUnlock unlock = reward.GetMetaGameUnlock(track);
                    Assert.That(unlock, Is.TypeOf<MetaGameUnlockDreamPowerStore>());
                    Assert.That(unlock.GetId(), Is.EqualTo("lucid-powers-store"));
                }
                Set(reward, "m_dreamPowerStoreDefinition", other);
                Assert.That(reward.GetMetaGameUnlock(RewardTrackType.Shadow).GetId(), Is.EqualTo("lucid-other-store"));
                Assert.That((int)reward.RewardType, Is.EqualTo(8), "the inherited feature reward type");
            });
        }
    }
}
