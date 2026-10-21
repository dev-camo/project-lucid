using System.Collections.Generic;
using HardlightProject;
using NUnit.Framework;
using static ProjectLucid.Tests.ChallengeRewardTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // The requirements that walk the level, zone and mission-group graph, over LevelRequirementWorld's authored
    // GameLevels, real MissionManager and open save. Every expected value restates the compiled predicate of the
    // cited 1.10.1 x86-64 body over a hand-built graph; the zone order is the authored order and the mission
    // records are the save's own.
    public sealed class LevelRequirementProgressionTests
    {
        // 0x556b90: the previous level (TryGetPreviousLevel) must exist and have mission groups; its first group
        // must hold a mission, and that first mission's record in that level must be complete (0x556dec).
        [Test]
        public void ThePreviousActRequirementChecksOnlyTheFirstMissionOfThePreviousLevel()
        {
            LevelRequirementWorld.Run((owned, world) =>
            {
                MissionDefinition firstMission = world.NewMission("lucid-act-first");
                MissionDefinition secondMission = world.NewMission("lucid-act-second");
                MissionDefinition otherGroupMission = world.NewMission("lucid-act-other-group");
                GameplayLevelDefinition first = world.NewLevel("lucid-act-level-1", false,
                    world.NewGroup("lucid-act-group-1", firstMission, secondMission), world.NewGroup("lucid-act-group-2", otherGroupMission));
                GameplayLevelDefinition second = world.NewLevel("lucid-act-level-2", false);
                GameplayLevelDefinition third = world.NewLevel("lucid-act-level-3", false, (MissionGroup[])null);
                GameplayLevelDefinition fourth = world.NewLevel("lucid-act-level-4", false, world.NewGroup("lucid-act-group-empty"));
                GameplayLevelDefinition fifth = world.NewLevel("lucid-act-level-5", false);
                world.AddZone("lucid-act-zone", first, second, third, fourth, fifth);
                var requirement = Create<RequirementPreviousActFirstMissionCompleted>(owned);

                Assert.That(requirement.RequirementsMet(first), Is.False, "nothing precedes the first level");
                Assert.That(requirement.RequirementsMet(second), Is.False, "the first mission is not complete");
                world.SetComplete(first, secondMission, true);
                Assert.That(requirement.RequirementsMet(second), Is.False, "only a later mission of the first group is complete");
                world.SetComplete(first, otherGroupMission, true);
                Assert.That(requirement.RequirementsMet(second), Is.False, "only a mission of another group is complete");
                world.SetComplete(first, firstMission, true);
                Assert.That(requirement.RequirementsMet(second), Is.True, "the first mission of the first group is complete");

                Assert.That(requirement.RequirementsMet(fourth), Is.False, "the previous level has no mission list");
                Assert.That(requirement.RequirementsMet(fifth), Is.False, "the previous level's first group is empty");
            });
        }

        // 0x556ed0: the zone before the level's own zone supplies the boss level whose first mission must be
        // complete. The scan keeps the zone it last passed and, for the first zone, falls back to that zone itself
        // (0x557184 cmovne), so the first zone is gated by its own boss. A level outside every zone, or a previous
        // zone without a boss level, fails.
        [Test]
        public void ThePreviousZoneBossRequirementChecksTheFirstMissionOfTheBossOfThePreviousZone()
        {
            LevelRequirementWorld.Run((owned, world) =>
            {
                MissionDefinition bossOne = world.NewMission("lucid-zone-boss-1");
                MissionDefinition bossTwo = world.NewMission("lucid-zone-boss-2");
                MissionDefinition plain = world.NewMission("lucid-zone-plain");
                GameplayLevelDefinition levelOne = world.NewLevel("lucid-zone-level-1", true, world.NewGroup("lucid-zone-group-1", bossOne));
                GameplayLevelDefinition levelTwo = world.NewLevel("lucid-zone-level-2", false, world.NewGroup("lucid-zone-group-2", plain));
                GameplayLevelDefinition levelThree = world.NewLevel("lucid-zone-level-3", true, world.NewGroup("lucid-zone-group-3", bossTwo));
                GameplayLevelDefinition levelFour = world.NewLevel("lucid-zone-level-4", false);
                GameplayLevelDefinition levelFive = world.NewLevel("lucid-zone-level-5", false);
                GameplayLevelDefinition levelSix = world.NewLevel("lucid-zone-level-6", false);
                GameplayLevelDefinition loose = world.NewLevel("lucid-zone-loose", false);
                world.AddZone("lucid-zone-1", levelOne, levelTwo);
                world.AddZone("lucid-zone-2", levelThree, levelFour);
                world.AddZone("lucid-zone-3", levelFive);
                world.AddZone("lucid-zone-4", levelSix);
                var requirement = Create<RequirementPreviousZoneBossDefeated>(owned);

                Assert.That(requirement.RequirementsMet(levelThree), Is.False, "the boss of zone 1 is not defeated");
                Assert.That(requirement.RequirementsMet(levelOne), Is.False, "zone 1 is gated by its own boss");
                world.SetComplete(levelThree, bossTwo, true);
                Assert.That(requirement.RequirementsMet(levelThree), Is.False, "completing the level's own boss is not enough");
                world.SetComplete(levelOne, bossOne, true);
                Assert.That(requirement.RequirementsMet(levelThree), Is.True, "zone 2 once the boss of zone 1 is defeated");
                Assert.That(requirement.RequirementsMet(levelFour), Is.True, "any level of zone 2");
                Assert.That(requirement.RequirementsMet(levelOne), Is.True, "zone 1 by its own boss");
                Assert.That(requirement.RequirementsMet(levelTwo), Is.True, "any level of zone 1");
                Assert.That(requirement.RequirementsMet(levelFive), Is.True, "zone 3 once the boss of zone 2 is defeated");
                Assert.That(requirement.RequirementsMet(levelSix), Is.False, "the previous zone 3 has no boss level");
                Assert.That(requirement.RequirementsMet(loose), Is.False, "a level in no zone");
            });
        }

        // 0x557e40: the first zone is open; any other listed zone is open when every boss level of the zone
        // before it meets its own requirements and has every mission of its first group complete (MissionList.Groups
        // at index 0 only, 0x5582f8..0x558366). A zone before it without boss levels is vacuously satisfied; a
        // category outside the zone list is locked.
        [Test]
        public void ACategoryIsLockedUntilEveryBossLevelOfThePreviousZoneIsFinished()
        {
            LevelRequirementWorld.Run((owned, world) =>
            {
                MissionDefinition bossA = world.NewMission("lucid-lock-boss-a");
                MissionDefinition bossB = world.NewMission("lucid-lock-boss-b");
                MissionDefinition plain = world.NewMission("lucid-lock-plain");
                MissionDefinition bossTwo = world.NewMission("lucid-lock-boss-two");
                MissionDefinition bossThree = world.NewMission("lucid-lock-boss-three");
                MissionDefinition laterGroup = world.NewMission("lucid-lock-later-group"); // never completed: only group 0 is read
                GameplayLevelDefinition boss = world.NewLevel("lucid-lock-level-1", true,
                    world.NewGroup("lucid-lock-group-1", bossA, bossB), world.NewGroup("lucid-lock-group-1b", laterGroup));
                GameplayLevelDefinition ordinary = world.NewLevel("lucid-lock-level-2", false, world.NewGroup("lucid-lock-group-2", plain));
                GameplayLevelDefinition secondBoss = world.NewLevel("lucid-lock-level-3", true, world.NewGroup("lucid-lock-group-3", bossTwo));
                GameplayLevelDefinition lockedBoss = world.NewLevel("lucid-lock-level-4", true, world.NewGroup("lucid-lock-group-4", bossThree));
                var alwaysLocked = Create<RequirementLevelAlwaysLocked>(owned);
                Set(lockedBoss, "m_requirements", new List<RequirementGameplayLevelBase> { alwaysLocked }); // Reflection: authored level data
                GameplayLevelDefinition noBoss = world.NewLevel("lucid-lock-level-5", false);
                GameplayLevelDefinition last = world.NewLevel("lucid-lock-level-6", false);
                GameplayLevelCategory zoneOne = world.AddZone("lucid-lock-zone-1", boss, ordinary);
                GameplayLevelCategory zoneTwo = world.AddZone("lucid-lock-zone-2", secondBoss);
                GameplayLevelCategory zoneThree = world.AddZone("lucid-lock-zone-3", lockedBoss);
                GameplayLevelCategory zoneFour = world.AddZone("lucid-lock-zone-4", noBoss);
                GameplayLevelCategory zoneFive = world.AddZone("lucid-lock-zone-5", last);
                GameplayLevelCategory unlisted = Create<GameplayLevelCategory>(owned, "{\"m_guid\":\"lucid-lock-unlisted\"}");
                var requirement = Create<RequirementLockCategoryByBoss>(owned);

                Assert.That(requirement.RequirementsMet(zoneOne), Is.True, "the first zone is always open");
                Assert.That(requirement.RequirementsMet(zoneTwo), Is.False, "the boss of zone 1 is not finished");
                world.SetComplete(boss, bossA, true);
                Assert.That(requirement.RequirementsMet(zoneTwo), Is.False, "only one mission of the boss group is complete");
                world.SetComplete(boss, bossB, true);
                Assert.That(requirement.RequirementsMet(zoneTwo), Is.True, "every mission of the boss's first group is complete, whatever a later group holds");
                Assert.That(requirement.RequirementsMet(unlisted), Is.False, "a category that is not a zone");

                Assert.That(requirement.RequirementsMet(zoneThree), Is.False, "the boss of zone 2 is not finished");
                world.SetComplete(secondBoss, bossTwo, true);
                Assert.That(requirement.RequirementsMet(zoneThree), Is.True, "zone 2's boss is finished");

                world.SetComplete(lockedBoss, bossThree, true);
                Assert.That(requirement.RequirementsMet(zoneFour), Is.False, "the boss level's own requirements are not met");
                Assert.That(requirement.RequirementsMet(zoneFive), Is.True, "the zone before has no boss level");
            });
        }

        // 0x5593b0: every mission of each group listed before the group (FindIndex by reference) must be
        // complete in this level's save data. The first group, or a group the level does not list, has nothing
        // before it.
        [Test]
        public void EveryGroupBeforeTheRequirementsGroupMustBeComplete()
        {
            LevelRequirementWorld.Run((owned, world) =>
            {
                MissionDefinition a1 = world.NewMission("lucid-groups-a1");
                MissionDefinition a2 = world.NewMission("lucid-groups-a2");
                MissionDefinition b1 = world.NewMission("lucid-groups-b1");
                MissionDefinition c1 = world.NewMission("lucid-groups-c1");
                MissionGroup groupA = world.NewGroup("lucid-groups-a", a1, a2);
                MissionGroup groupB = world.NewGroup("lucid-groups-b", b1);
                MissionGroup groupC = world.NewGroup("lucid-groups-c", c1);
                MissionGroup foreign = world.NewGroup("lucid-groups-foreign", c1);
                GameplayLevelDefinition level = world.NewLevel("lucid-groups-level", false, groupA, groupB, groupC);
                var requirement = Create<RequirementPreviousMissionGroupsAreComplete>(owned);

                Assert.That(requirement.RequirementsMet(groupA, level), Is.True, "nothing precedes the first group");
                Assert.That(requirement.RequirementsMet(foreign, level), Is.True, "a group the level does not list");
                Assert.That(requirement.RequirementsMet(groupB, level), Is.False, "group A is not complete");
                world.SetComplete(level, a1, true);
                Assert.That(requirement.RequirementsMet(groupB, level), Is.False, "one mission of group A is missing");
                world.SetComplete(level, a2, true);
                Assert.That(requirement.RequirementsMet(groupB, level), Is.True, "group A is complete");
                Assert.That(requirement.RequirementsMet(groupC, level), Is.False, "group B is not complete");
                world.SetComplete(level, a2, false);
                world.SetComplete(level, b1, true);
                Assert.That(requirement.RequirementsMet(groupC, level), Is.False, "every earlier group counts, not only the one before");
                world.SetComplete(level, a2, true);
                Assert.That(requirement.RequirementsMet(groupC, level), Is.True, "groups A and B are complete");
            });
        }

        // 0x559150: each listed mission is looked up in the save data of the first level that hosts it
        // (GetFirstLevelForMission, GetMissionSaveDataForLevel); a found record that is not complete fails.
        [Test]
        public void EveryListedMissionMustBeCompleteInItsFirstLevel()
        {
            LevelRequirementWorld.Run((owned, world) =>
            {
                MissionDefinition a1 = world.NewMission("lucid-listed-a1");
                MissionDefinition b1 = world.NewMission("lucid-listed-b1");
                GameplayLevelDefinition first = world.NewLevel("lucid-listed-level-1", false, world.NewGroup("lucid-listed-group-1", a1));
                GameplayLevelDefinition second = world.NewLevel("lucid-listed-level-2", false, world.NewGroup("lucid-listed-group-2", b1));
                world.AddZone("lucid-listed-zone", first, second);
                var requirement = Create<RequirementMissionsCompleted>(owned);
                Set(requirement, "m_missionDefinitions", new[] { a1, b1 }); // Reflection: serialized array without a setter

                Assert.That(requirement.RequirementsMet(null, null), Is.False, "neither mission is complete");
                world.SetComplete(first, a1, true);
                Assert.That(requirement.RequirementsMet(null, null), Is.False, "the second mission is not complete");
                world.SetComplete(second, b1, true);
                Assert.That(requirement.RequirementsMet(null, null), Is.True, "both missions are complete");
                Set(requirement, "m_missionDefinitions", new MissionDefinition[0]); // Reflection: serialized array without a setter
                Assert.That(requirement.RequirementsMet(null, null), Is.True, "no listed missions");
            });
        }

        // 0x559232: GetFirstLevelForMission picks the first hosting level in authored order, so a mission hosted
        // by two levels is read from the first one's record (GetMissionSaveDataForLevel creates it incomplete).
        [Test]
        public void AMissionHostedByTwoLevelsIsReadFromTheFirstHostingLevel()
        {
            LevelRequirementWorld.Run((owned, world) =>
            {
                MissionDefinition shared = world.NewMission("lucid-shared-mission");
                GameplayLevelDefinition first = world.NewLevel("lucid-shared-level-1", false, world.NewGroup("lucid-shared-group-1", shared));
                GameplayLevelDefinition second = world.NewLevel("lucid-shared-level-2", false, world.NewGroup("lucid-shared-group-2", shared));
                world.AddZone("lucid-shared-zone", first, second);
                var requirement = Create<RequirementMissionsCompleted>(owned);
                Set(requirement, "m_missionDefinitions", new[] { shared }); // Reflection: serialized array without a setter

                world.SetComplete(second, shared, true);
                Assert.That(requirement.RequirementsMet(null, null), Is.False, "complete only in the second hosting level");
                world.SetComplete(first, shared, true);
                Assert.That(requirement.RequirementsMet(null, null), Is.True, "complete in the first hosting level");
                world.SetComplete(second, shared, false);
                Assert.That(requirement.RequirementsMet(null, null), Is.True, "the second hosting level is not read");
            });
        }

        // 0x558900: only persistent trackers that already have a mission state can fail; a tracker without a state
        // counts as complete, and states of other missions are not looked at.
        [Test]
        public void OnlyExistingPersistentTrackerStatesCanFailThePersistentRequirement()
        {
            LevelRequirementWorld.Run((owned, world) =>
            {
                MissionDefinition p1 = world.NewMission("lucid-persistent-1", true);
                MissionDefinition p2 = world.NewMission("lucid-persistent-2", true);
                MissionDefinition ordinary = world.NewMission("lucid-persistent-ordinary");
                MissionGroup group = world.NewGroup("lucid-persistent-group", p1, p2, ordinary);
                GameplayLevelDefinition level = world.NewLevel("lucid-persistent-level", false, group);
                var requirement = Create<RequirementAllPersistentMissionsAreComplete>(owned);

                Assert.That(requirement.RequirementsMet(group, null), Is.True, "no mission states exist");
                world.AddMissionState(level, p1);
                Assert.That(requirement.RequirementsMet(group, null), Is.False, "a persistent tracker with an incomplete state");
                world.SetComplete(level, p1, true);
                Assert.That(requirement.RequirementsMet(group, null), Is.True, "its state is complete; p2 has no state");
                world.AddMissionState(level, p2);
                Assert.That(requirement.RequirementsMet(group, null), Is.False, "the second persistent tracker is incomplete");
                world.SetComplete(level, p2, true);
                world.AddMissionState(level, ordinary);
                Assert.That(requirement.RequirementsMet(group, null), Is.True, "an incomplete ordinary mission state is ignored");
            });
        }

        // 0x556970: a level that GameLevels does not list is not met, before the progression totals are consulted.
        [Test]
        public void ALevelOutsideTheGameLevelsDoesNotHaveEnoughRewards()
        {
            LevelRequirementWorld.Run((owned, world) =>
            {
                GameplayLevelDefinition listed = world.NewLevel("lucid-rewards-listed", false);
                GameplayLevelDefinition unlisted = world.NewLevel("lucid-rewards-unlisted", false);
                world.AddZone("lucid-rewards-zone", listed);
                world.AuthorRewardRequirements(unlisted);
                var requirement = Create<RequirementPlayerHasEnoughRewards>(owned);
                Assert.That(requirement.RequirementsMet(unlisted), Is.False, "an empty requirement list does not rescue an unlisted level");
            });
        }

        // 0x556aa2 compares the collected total with the count (setge) and the loop ANDs every result (0x556aa8, r14b);
        // no requirement leaves the initial true (0x556a73 jle to 0x556adf). A type with no total counts as zero.
        [Test]
        public void EveryRewardRequirementOfAListedLevelMustBeReached()
        {
            LevelRequirementWorld.Run((owned, world) =>
            {
                GameplayLevelDefinition none = world.NewLevel("lucid-rewards-none", false);
                GameplayLevelDefinition single = world.NewLevel("lucid-rewards-single", false);
                GameplayLevelDefinition both = world.NewLevel("lucid-rewards-both", false);
                world.AddZone("lucid-rewards-reached-zone", none, single, both);
                world.AuthorRewardRequirements(none);
                world.AuthorRewardRequirements(single, (CollectableType.Orb, 5));
                world.AuthorRewardRequirements(both, (CollectableType.Orb, 5), (CollectableType.BlueCoin, 3));
                Dictionary<CollectableType, ProgressionCollectable> totals = world.AddProgression();
                var requirement = Create<RequirementPlayerHasEnoughRewards>(owned);
                void Collect(int orbs, int blueCoins)
                {
                    totals[CollectableType.Orb] = new ProgressionCollectable(CollectableType.Orb, orbs, 100);
                    totals[CollectableType.BlueCoin] = new ProgressionCollectable(CollectableType.BlueCoin, blueCoins, 100);
                }

                totals.Clear();
                Assert.That(requirement.RequirementsMet(none), Is.True, "no requirements");
                Assert.That(requirement.RequirementsMet(single), Is.False, "no total for the type counts as zero");
                Collect(4, 3);
                Assert.That(requirement.RequirementsMet(single), Is.False, "one below the count");
                Assert.That(requirement.RequirementsMet(both), Is.False, "the second requirement alone is reached");
                Collect(5, 2);
                Assert.That(requirement.RequirementsMet(single), Is.True, "exactly the count");
                Assert.That(requirement.RequirementsMet(both), Is.False, "the first requirement alone is reached");
                Collect(5, 3);
                Assert.That(requirement.RequirementsMet(both), Is.True, "both are reached");
                Collect(9, 7);
                Assert.That(requirement.RequirementsMet(both), Is.True, "above both counts");
            });
        }

        // 0x558db9 compares m_orbs with the Orb total (setle) after fetching GetTotalProgress(0x6b27f37a, the
        // CollectableType.Orb hash); other types never count.
        [Test]
        public void OrbGatedGroupsNeedAtLeastTheAuthoredOrbCountCollected()
        {
            LevelRequirementWorld.Run((owned, world) =>
            {
                Dictionary<CollectableType, ProgressionCollectable> totals = world.AddProgression();
                RequirementMissionGroupGatedByPlayerOrbs Gate(int orbs) =>
                    Create<RequirementMissionGroupGatedByPlayerOrbs>(owned, "{\"m_orbs\":" + orbs + "}");

                Assert.That(Gate(0).RequirementsMet(null, null), Is.True, "nothing needed, nothing collected");
                Assert.That(Gate(1).RequirementsMet(null, null), Is.False, "one needed, nothing collected");
                totals[CollectableType.BlueCoin] = new ProgressionCollectable(CollectableType.BlueCoin, 99, 99);
                Assert.That(Gate(1).RequirementsMet(null, null), Is.False, "another type's total is not counted");
                totals[CollectableType.Orb] = new ProgressionCollectable(CollectableType.Orb, 5, 40);
                Assert.That(Gate(5).RequirementsMet(null, null), Is.True, "exactly the orbs needed");
                Assert.That(Gate(4).RequirementsMet(null, null), Is.True, "more than needed");
                Assert.That(Gate(6).RequirementsMet(null, null), Is.False, "one orb short");
            });
        }
    }
}
