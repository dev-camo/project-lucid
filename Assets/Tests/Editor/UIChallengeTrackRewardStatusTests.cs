using System;
using Hardlight.Enums;
using HardlightProject;
using NUnit.Framework;
using ProjectLucid.Tests.UIContainerShared;
using UnityEngine;
using static ProjectLucid.Tests.ChallengeRewardTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // UIChallengeTrackRewardStatus (Game.Runtime 0x0200085a, x86-64 0x60F820..0x60F970) over a recording ChallengeReward built by
    // its real constructor. The reward base's static initialiser takes the data and save manager rows, so every case leases them.
    public sealed class UIChallengeTrackRewardStatusTests
    {
        private sealed class StubUnlock : IMetaGameUnlock
        {
            public string GetId() => "lucid-stub-unlock";
            public PlayerProgressionTypes GetProgressionType() => default;
            public UIWidgetProgression GetWidget() => null;
            public Strings GetTitle() => default;
            public Strings GetBody() => default;
            public HLAudioClipIdentifier GetAudio() => default;
            public void Save(SaveManager saveManager) { }
            public void GetCustomTexture(Action<Texture> onLoaded) { }
            public void GetCustomSprite(Action<Sprite> onLoaded) { }
            public void ReleaseAssets() { }
            public bool CanExitToMenu() => false;
            public bool UsesAlternativeExitToMenuEvent() => false;
        }

        private static void Run(Action<UIChallengeTrackRewardRecordingReward> body) => Owned.Run(owned =>
        {
            LeaseRewardRows(owned);
            body(NewReward<UIChallengeTrackRewardRecordingReward>(owned));
        });

        // dump.cs TypeDefIndex 2137 and the DummyDll rows (fields 0x04002016..0x0400201a, methods 0x06003068..0x06003070). The
        // collected flag is the only mutable field; its setter is private.
        [Test]
        public void DeclarationsMatchTheOriginalMetadata()
        {
            AssertMembers(typeof(UIChallengeTrackRewardStatus), 0x00100001,
                new (string, int)[]
                {
                    ("<Reward>k__BackingField", 0x0021),
                    ("<Unlocked>k__BackingField", 0x0021),
                    ("<Collected>k__BackingField", 0x0001),
                    ("m_rewardNumber", 0x0021),
                    ("m_rewardTrackType", 0x0021),
                },
                new (string, int)[]
                {
                    ("get_Reward", 0x0886),
                    ("get_Unlocked", 0x0886),
                    ("get_Collected", 0x0886),
                    ("set_Collected", 0x0881),
                    ("IsReadyToCollect", 0x0086),
                    (".ctor", 0x1886),
                    ("Save", 0x0086),
                    ("FireAnalyticsEvents", 0x0086),
                    ("GetMetaGameUnlock", 0x0086),
                });
            AssertIl2CppOptions(typeof(UIChallengeTrackRewardStatus), 2, 1);
        }

        // The constructor 0x60F880 takes (reward, collected, unlocked, rewardNumber, rewardTrackType) and stores collected at +0x19 and
        // unlocked at +0x18; a swap of the two flags would show in the asymmetric pair.
        [Test]
        public void ConstructionStoresTheRewardAndTheTwoFlagsInTheirOwnFields()
        {
            Run(reward =>
            {
                var collectedOnly = new UIChallengeTrackRewardStatus(reward, true, false, 4, RewardTrackType.Shadow);
                Assert.That(collectedOnly.Reward, Is.SameAs(reward));
                Assert.That(collectedOnly.Collected, Is.True);
                Assert.That(collectedOnly.Unlocked, Is.False);
                var unlockedOnly = new UIChallengeTrackRewardStatus(reward, false, true, 4, RewardTrackType.Shadow);
                Assert.That(unlockedOnly.Collected, Is.False);
                Assert.That(unlockedOnly.Unlocked, Is.True);
                Assert.That(Get<int>(unlockedOnly, "m_rewardNumber"), Is.EqualTo(4));
                Assert.That(Get<RewardTrackType>(unlockedOnly, "m_rewardTrackType"), Is.EqualTo(RewardTrackType.Shadow));
            });
        }

        // IsReadyToCollect 0x60F860: unlocked (+0x18) tested first, then collected (+0x19) must be clear. Truth table over both flags.
        [TestCase(false, false, false, TestName = "ALockedUncollectedRewardIsNotReady")]
        [TestCase(false, true, false, TestName = "ALockedCollectedRewardIsNotReady")]
        [TestCase(true, false, true, TestName = "AnUnlockedUncollectedRewardIsReady")]
        [TestCase(true, true, false, TestName = "AnUnlockedCollectedRewardIsNotReady")]
        public void ARewardIsReadyToCollectOnlyWhenUnlockedAndNotCollected(bool unlocked, bool collected, bool expected)
        {
            Run(reward =>
                Assert.That(new UIChallengeTrackRewardStatus(reward, collected, unlocked, 0, RewardTrackType.Adventure).IsReadyToCollect(), Is.EqualTo(expected)));
        }

        // Save 0x60F8E0: the reward's virtual Save (slot 14) is called first and the collected flag (+0x19) is set only after it
        // returns; the unlocked flag and the reward are left alone.
        [Test]
        public void SavingSavesTheRewardAndThenMarksItCollected()
        {
            Run(reward =>
            {
                var status = new UIChallengeTrackRewardStatus(reward, false, true, 1, RewardTrackType.Adventure);
                bool collectedWhileSaving = true;
                reward.OnSave = () => collectedWhileSaving = status.Collected;

                status.Save();

                Assert.That(reward.Calls, Is.EqualTo(new[] { "Save" }));
                Assert.That(collectedWhileSaving, Is.False, "the flag is set after the reward saved");
                Assert.That(status.Collected, Is.True);
                Assert.That(status.Unlocked, Is.True);
                Assert.That(status.IsReadyToCollect(), Is.False);
            });
        }

        // A fault in the reward's Save leaves the flag as it was (the store at 0x60F8FD follows the call).
        [Test]
        public void ASaveThatFaultsLeavesTheRewardUncollected()
        {
            Run(reward =>
            {
                var status = new UIChallengeTrackRewardStatus(reward, false, true, 1, RewardTrackType.Adventure);
                reward.OnSave = () => throw new InvalidOperationException("lucid save fault");

                Assert.Throws<InvalidOperationException>(status.Save);

                Assert.That(status.Collected, Is.False);
            });
        }

        // FireAnalyticsEvents 0x60F910 tail-calls the reward's virtual hook (slot 18) with the track type (+0x20) in the first
        // argument and the reward number (+0x1C) in the second.
        [Test]
        public void AnalyticsEventsPassTheTrackTypeAndTheRewardNumberInOrder()
        {
            Run(reward =>
            {
                var status = new UIChallengeTrackRewardStatus(reward, false, true, 7, RewardTrackType.Outfits);

                status.FireAnalyticsEvents();
                status.FireAnalyticsEvents();

                Assert.That(reward.AnalyticsEvents, Is.EqualTo(new[] { (RewardTrackType.Outfits, 7), (RewardTrackType.Outfits, 7) }));
            });
        }

        // GetMetaGameUnlock 0x60F940 tail-calls the reward's virtual member (slot 16) with the track type (+0x20) and returns its answer.
        [Test]
        public void TheMetaGameUnlockIsTheRewardsUnlockForTheStatusTrack()
        {
            Run(reward =>
            {
                reward.Unlock = new StubUnlock();
                var status = new UIChallengeTrackRewardStatus(reward, false, false, 2, RewardTrackType.Shadow);

                Assert.That(status.GetMetaGameUnlock(), Is.SameAs(reward.Unlock));
                Assert.That(reward.UnlockRequests, Is.EqualTo(new[] { RewardTrackType.Shadow }));
            });
        }

        // The members dereference the reward without a null test (0x60F8E9, 0x60F914, 0x60F944); only the readiness answer is
        // independent of it.
        [Test]
        public void ARewardlessStatusFaultsOnEveryReadOfTheReward()
        {
            var status = new UIChallengeTrackRewardStatus(null, false, true, 0, RewardTrackType.Adventure);

            Assert.That(status.IsReadyToCollect(), Is.True);
            Assert.Throws<NullReferenceException>(status.Save);
            Assert.Throws<NullReferenceException>(status.FireAnalyticsEvents);
            Assert.Throws<NullReferenceException>(() => status.GetMetaGameUnlock());
            Assert.That(status.Collected, Is.False, "the failed save did not mark it collected");
        }
    }
}
