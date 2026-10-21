using System.Collections.Generic;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using static ProjectLucid.Tests.ChallengeRewardTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // The level requirements that read the open save: RequirementAtLeastOneMissionComplete (x86-64 0x5577F0),
    // RequirementRewardLevelUnlocked (0x557510), RequirementRewardTrackUnlocked (0x5587F0),
    // RequirementSpecificMissionComplete (0x557600) and RequirementHasNotCompletedMission (0x557CB0), over the
    // SaveManager, DataManager and MissionManager of LevelRequirementWorld. The mission definitions of the last
    // two are serialized references; Reflection stands in for the serializer.
    public sealed class LevelRequirementSaveDataTests
    {
        // GetCompleteMissionCount of the level's record (created on demand) must be above zero (0x557848).
        [Test]
        public void AnyCompletedMissionOfTheLevelMeetsTheAtLeastOneRequirement()
        {
            LevelRequirementWorld.Run((owned, world) =>
            {
                var requirement = Create<RequirementAtLeastOneMissionComplete>(owned);
                GameplayLevelDefinition level = world.NewLevel("lucid-at-least-one", false);
                GameplayLevelDefinition other = world.NewLevel("lucid-at-least-one-other", false);
                MissionDefinition first = world.NewMission("lucid-at-least-one-first");
                MissionDefinition second = world.NewMission("lucid-at-least-one-second");

                Assert.That(requirement.RequirementsMet(level), Is.False, "no record for the level yet");
                world.SetComplete(level, first, false);
                Assert.That(requirement.RequirementsMet(level), Is.False, "a record that is not complete");
                world.SetComplete(other, first, true);
                Assert.That(requirement.RequirementsMet(level), Is.False, "completion of another level does not count");
                world.SetComplete(level, first, true);
                Assert.That(requirement.RequirementsMet(level), Is.True, "one completed mission");
                world.SetComplete(level, second, true);
                Assert.That(requirement.RequirementsMet(level), Is.True, "more than one is still above zero");
            });
        }

        // The level GUID is used as the reward GUID (0x557553); the reward counts once it is collected (+0x28).
        [Test]
        public void ALevelRewardIsUnlockedOnlyOnceItsRecordIsCollected()
        {
            LevelRequirementWorld.Run((owned, world) =>
            {
                var requirement = Create<RequirementRewardLevelUnlocked>(owned);
                GameplayLevelDefinition level = world.NewLevel("lucid-reward-level", false);
                GameplayLevelDefinition unrelated = world.NewLevel("lucid-reward-unrelated", false);

                Assert.That(requirement.RequirementsMet(level), Is.False, "no reward record");
                world.SaveGame.CreateRewardTrackData(RewardTrackType.Adventure, "lucid-reward-track", "lucid-reward-level");
                Assert.That(requirement.RequirementsMet(level), Is.False, "record present but not collected");
                world.SaveGame.GetRewardDataByRewardGUIDUnsafe("lucid-reward-level").Collected = true;
                Assert.That(requirement.RequirementsMet(level), Is.True, "collected");
                Assert.That(requirement.RequirementsMet(unrelated), Is.False, "another level has no record");
            });
        }

        // GetOrCreateRewardTrackData(type).UnlockState (+0x28) must differ from Locked (0x558864); a missing
        // SaveManager (0x558839) or a closed save (0x558846) is simply locked.
        [Test]
        public void ARewardTrackIsUnlockedOnlyAfterItsOwnUnlockStateLeavesLocked()
        {
            LevelRequirementWorld.Run((owned, world) =>
            {
                RewardTrackType[] types = { RewardTrackType.Adventure, RewardTrackType.Outfits, RewardTrackType.Shadow };
                var requirements = new RequirementRewardTrackUnlocked[types.Length];
                for (int index = 0; index < types.Length; ++index)
                    requirements[index] = Create<RequirementRewardTrackUnlocked>(owned, "{\"m_rewardTrackType\":" + (int)types[index] + "}");

                void AssertUnlocked(string label, params bool[] expected)
                {
                    for (int index = 0; index < types.Length; ++index)
                        Assert.That(requirements[index].RequirementsMet(null), Is.EqualTo(expected[index]), types[index] + " " + label);
                }

                AssertUnlocked("before any unlock", false, false, false);
                world.SaveGame.GetOrCreateRewardTrackData(RewardTrackType.Adventure).UnlockState = SaveDataChallengeRewardTrack.FeatureUnlockState.Unlocked;
                AssertUnlocked("after Adventure is unlocked", true, false, false);
                world.SaveGame.GetOrCreateRewardTrackData(RewardTrackType.Outfits).UnlockState = SaveDataChallengeRewardTrack.FeatureUnlockState.Seen;
                AssertUnlocked("after Outfits is seen", true, true, false);
                world.SaveGame.GetOrCreateRewardTrackData(RewardTrackType.Shadow).UnlockState = SaveDataChallengeRewardTrack.FeatureUnlockState.Unlocked;
                AssertUnlocked("after Shadow is unlocked", true, true, true);

                world.Save.CloseSave();
                AssertUnlocked("with no save open", false, false, false);
            });
        }

        [Test]
        public void ARewardTrackWithoutASaveManagerIsLocked()
        {
            Owned.Run(owned =>
            {
                owned.LeaseRow<SaveManager>();
                var requirement = Create<RequirementRewardTrackUnlocked>(owned);
                Assert.That(requirement.RequirementsMet(null), Is.False);
            });
        }

        // The mission's level comes from the DataManager lookup (0x55768a, never the argument); the record is
        // looked up through the open save and its Complete flag (+0x20) is the answer. Either manager missing
        // is false before the lookup (0x557669, 0x557684).
        [Test]
        public void ASpecificMissionIsCompleteWhenItsRecordInTheLookedUpLevelIsComplete()
        {
            LevelRequirementWorld.Run((owned, world) =>
            {
                GameplayLevelDefinition level = world.NewLevel("lucid-specific-level", false);
                GameplayLevelDefinition unrelated = world.NewLevel("lucid-specific-unrelated", false);
                MissionDefinition mission = world.NewMission("lucid-specific-mission");
                MissionDefinition unregistered = world.NewMission("lucid-specific-unregistered");
                world.Data.MissionDefinitionLevelLookup.Add(mission, level);
                var requirement = Create<RequirementSpecificMissionComplete>(owned);
                Set(requirement, "m_missionDefinition", mission); // Reflection: serialized reference without a setter

                Assert.That(requirement.RequirementsMet(null), Is.False, "no record yet");
                world.SetComplete(level, mission, false);
                Assert.That(requirement.RequirementsMet(unrelated), Is.False, "a record that is not complete");
                world.SetComplete(unrelated, mission, true);
                Assert.That(requirement.RequirementsMet(unrelated), Is.False, "completion under another level does not count");
                world.SetComplete(level, mission, true);
                Assert.That(requirement.RequirementsMet(unrelated), Is.True, "complete under the looked up level");
                Assert.That(requirement.RequirementsMet(null), Is.True, "the argument is not read");

                Set(requirement, "m_missionDefinition", unregistered); // Reflection: serialized reference without a setter
                Assert.Throws<KeyNotFoundException>(() => requirement.RequirementsMet(null), "the lookup indexer throws for an unknown mission");
            });
        }

        [Test]
        public void ASpecificMissionWithoutTheManagersIsNotComplete()
        {
            Owned.Run(owned =>
            {
                owned.LeaseRow<DataManager>();
                owned.LeaseRow<SaveManager>();
                var requirement = Create<RequirementSpecificMissionComplete>(owned);
                Assert.That(requirement.RequirementsMet(null), Is.False);
            });
        }

        // IsNull on the MissionManager reference is tested first (0x557d08) and answers false; otherwise the
        // mission's own level comes from the lookup and the answer is the negated Complete flag (0x557d74).
        [Test]
        public void ANotCompletedMissionRequirementNegatesTheRecordedCompletion()
        {
            LevelRequirementWorld.Run((owned, world) =>
            {
                GameplayLevelDefinition level = world.NewLevel("lucid-not-completed-level", false);
                MissionDefinition mission = world.NewMission("lucid-not-completed-mission");
                MissionDefinition unregistered = world.NewMission("lucid-not-completed-unregistered");
                world.Data.MissionDefinitionLevelLookup.Add(mission, level);
                var requirement = Create<RequirementHasNotCompletedMission>(owned);
                Set(requirement, "m_missionDefinition", mission); // Reflection: serialized reference without a setter

                Assert.That(requirement.RequirementsMet(null), Is.True, "no record yet");
                world.SetComplete(level, mission, false);
                Assert.That(requirement.RequirementsMet(null), Is.True, "a record that is not complete");
                world.SetComplete(level, mission, true);
                Assert.That(requirement.RequirementsMet(null), Is.False, "complete");

                Set(requirement, "m_missionDefinition", unregistered); // Reflection: serialized reference without a setter
                Assert.Throws<KeyNotFoundException>(() => requirement.RequirementsMet(null), "the lookup indexer throws for an unknown mission");
            });
        }

        [Test]
        public void ANotCompletedMissionRequirementWithoutAMissionManagerIsNotMet()
        {
            Owned.Run(owned =>
            {
                owned.LeaseRow<DataManager>();
                owned.LeaseRow<MissionManager>();
                var requirement = Create<RequirementHasNotCompletedMission>(owned);
                Assert.That(requirement.RequirementsMet(null), Is.False, "false before the DataManager is consulted");
            });
        }
    }
}
