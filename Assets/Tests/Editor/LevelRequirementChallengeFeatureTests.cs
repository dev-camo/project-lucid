using System;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using static ProjectLucid.Tests.ChallengeRewardTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // The requirements that read ChallengeManager.FeatureState (+0xE8): RequirementChallengesFeatureSeen
    // (x86-64 0x557A80), RequirementChallengesFeatureUnlocked (0x557910, with the int32 array 1, 2, 3 that its
    // constructor loads from the metadata blob) and RequirementMissionGroupShadowFTUEAvailable (0x558E40).
    // The manager is a real ChallengeManager on an inactive host, as in ChallengeManagerBindersTests.
    public sealed class LevelRequirementChallengeFeatureTests
    {
        private static readonly ChallengeManager.ChallengeFeatureState[] States =
        {
            ChallengeManager.ChallengeFeatureState.Locked,
            ChallengeManager.ChallengeFeatureState.UnlockedShowInterstitial,
            ChallengeManager.ChallengeFeatureState.UnlockedSeenInterstitial,
            ChallengeManager.ChallengeFeatureState.UnlockedSeenFeature,
        };

        // ChallengeManager's field initialisers read these six rows; the manager is added while an empty category
        // lookup is cached for the editor's Reset and OnValidate, then registered so the requirements' typed
        // references find it.
        private static ChallengeManager RegisteredManager(Owned owned)
        {
            owned.LeaseRow<DataManager>();
            owned.LeaseRow<VisualQualityManager_SDT>();
            owned.LeaseRow<CutsceneManager>();
            owned.LeaseRow<MissionManager>();
            owned.LeaseRow<SaveManager>();
            owned.LeaseRow<OrnamentManager>();
            return owned.Register(owned.Lease(new OwnedTimeScaledObjects("lucid-challenge-requirements")).Add<ChallengeManager>("manager"));
        }

        // Reflection: the state's setter is private and the public routes need an open save and the App.
        private static void SetFeatureState(ChallengeManager manager, ChallengeManager.ChallengeFeatureState state) =>
            typeof(ChallengeManager).GetProperty(nameof(ChallengeManager.FeatureState)).GetSetMethod(true).Invoke(manager, new object[] { state });

        // 0x557acc compares the state with 3 (UnlockedSeenFeature) and nothing else.
        [Test]
        public void TheFeatureIsSeenOnlyInTheFinalState()
        {
            Owned.Run(owned =>
            {
                ChallengeManager manager = RegisteredManager(owned);
                var requirement = Create<RequirementChallengesFeatureSeen>(owned);
                foreach (ChallengeManager.ChallengeFeatureState state in States)
                {
                    SetFeatureState(manager, state);
                    Assert.That(requirement.RequirementsMet(null), Is.EqualTo((int)state == 3), state.ToString());
                }
            });
        }

        // The constructor's array holds 1, 2 and 3 (blob 4636993D... at metadata 0x696D40); Locked is not in it.
        [Test]
        public void TheFeatureIsUnlockedInEveryStatePastLocked()
        {
            Owned.Run(owned =>
            {
                ChallengeManager manager = RegisteredManager(owned);
                var requirement = Create<RequirementChallengesFeatureUnlocked>(owned);
                foreach (ChallengeManager.ChallengeFeatureState state in States)
                {
                    SetFeatureState(manager, state);
                    Assert.That(requirement.RequirementsMet(null), Is.EqualTo((int)state != 0), state.ToString());
                }
            });
        }

        // Original-derived declaration oracle: blob 4636993D... at metadata 0x696D40 holds 1, 2, 3 in that order.
        [Test]
        public void TheUnlockedStatesAreTheThreeStatesPastLockedInOrder()
        {
            Owned.Run(owned =>
            {
                RegisteredManager(owned);
                var requirement = Create<RequirementChallengesFeatureUnlocked>(owned);
                Assert.That(Get<ChallengeManager.ChallengeFeatureState[]>(requirement, "m_unlockedStates"),
                    Is.EqualTo(new[] { (ChallengeManager.ChallengeFeatureState)1, (ChallengeManager.ChallengeFeatureState)2, (ChallengeManager.ChallengeFeatureState)3 }));
            });
        }

        // 0x558e8e calls ShouldShowShadowFTUE and 0x558e97 compares the state with Locked: the answer is
        // !ShouldShowShadowFTUE() && FeatureState != Locked. ShouldShowShadowFTUE is true until the FTUE cutscene has
        // been seen or the FTUE challenge mission is complete; only the mission route is driven here, since the
        // cutscene route needs a started CutsceneManager (not covered; its row stays empty, so the cutscene is unseen).
        [Test]
        public void TheShadowFtueIsAvailableOnceItsMissionIsCompleteAndTheFeatureIsNotLocked()
        {
            LevelRequirementWorld.Run((owned, world) =>
            {
                // The world already registers the DataManager, SaveManager and MissionManager that the manager reads.
                owned.LeaseRow<VisualQualityManager_SDT>();
                owned.LeaseRow<CutsceneManager>();
                owned.LeaseRow<OrnamentManager>();
                ChallengeManager manager = owned.Register(owned.Lease(new OwnedTimeScaledObjects("lucid-ftue-requirement")).Add<ChallengeManager>("manager"));
                MissionDefinition mission = world.NewMission("lucid-ftue-mission");
                GameplayLevelDefinition level = world.NewLevel("lucid-ftue-level", false, world.NewGroup("lucid-ftue-group", mission));
                var challenge = Create<ChallengeDefinition>(owned);
                Set(challenge, "m_mission", mission); // Reflection: serialized references without setters
                Set(challenge, "m_level", level);
                Set(manager, "m_shadowFtueChallenge", challenge);
                var requirement = Create<RequirementMissionGroupShadowFTUEAvailable>(owned);

                foreach (ChallengeManager.ChallengeFeatureState state in States)
                {
                    SetFeatureState(manager, state);
                    Assert.That(requirement.RequirementsMet(null, null), Is.False, state + " with the FTUE still to be shown");
                }
                world.SetComplete(level, mission, true);
                foreach (ChallengeManager.ChallengeFeatureState state in States)
                {
                    SetFeatureState(manager, state);
                    Assert.That(requirement.RequirementsMet(null, null), Is.EqualTo((int)state != 0), state + " with the FTUE mission complete");
                }
            });
        }

        // TryGet fails when no ChallengeManager is registered (0x557ac4, 0x557962, 0x558e86): the answer is false.
        [Test]
        public void WithoutAChallengeManagerNoFeatureRequirementIsMet()
        {
            Owned.Run(owned =>
            {
                owned.LeaseRow<ChallengeManager>();
                Assert.That(Create<RequirementChallengesFeatureSeen>(owned).RequirementsMet(null), Is.False);
                Assert.That(Create<RequirementChallengesFeatureUnlocked>(owned).RequirementsMet(null), Is.False);
                Assert.That(Create<RequirementMissionGroupShadowFTUEAvailable>(owned).RequirementsMet(null, null), Is.False);
            });
        }
    }
}
