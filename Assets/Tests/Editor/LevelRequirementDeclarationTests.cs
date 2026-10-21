using System;
using System.Linq;
using System.Reflection;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // Metadata flags, member order, IL2CPP options, asset menus and serialized fields of the restored
    // requirement, prompt, save and leaderboard types, against the DummyDll rows of Game.Runtime
    // (type flags, fields 0x040027f4.. / 0x0400146e.. / 0x04001a63.., methods 0x06001ef6.. / 0x06003741..).
    public sealed class LevelRequirementDeclarationTests
    {
        private const string RequirementsMenu = "HardlightProject/Requirements/";
        private const string DefinitionsMenu = "HardlightProject/DefinitionData/Definitions/";
        private const int Public = 0x00100001;
        private const int PublicSealed = 0x00100101;
        private const int Ref = 0x0021;
        private const int Plain = 0x0001;
        private static readonly (string, int) Met = ("RequirementsMet", 0x00c6);
        private static readonly (string, int) Ctor = (".ctor", 0x1886);

        private static void AssertMenu(Type type, string menu)
        {
            CustomAttributeData data = type.GetCustomAttributesData().Single(d => d.AttributeType == typeof(CreateAssetMenuAttribute));
            Assert.That(data.NamedArguments.Select(a => (a.MemberName, a.TypedValue.Value)).ToArray(), Is.EqualTo(new (string, object)[]
            {
                ("fileName", type.Name),
                ("menuName", menu + type.Name),
            }), type.Name);
        }

        private static void AssertSerialized(Type type, params string[] names)
        {
            foreach (string name in names)
                Assert.That(Field(type, name).GetCustomAttributes(typeof(SerializeField), false), Has.Length.EqualTo(1), type.Name + "." + name);
        }

        // Option values: 1 NullChecks, 2 ArrayBoundsChecks, in metadata order.
        private static void AssertShape(Type type, Type baseType, int typeFlags, int[] options, (string, int)[] fields, (string, int)[] methods)
        {
            Assert.That(type.BaseType, Is.EqualTo(baseType), type.Name + " base");
            AssertMembers(type, typeFlags, fields, methods);
            AssertIl2CppOptions(type, options);
        }

        [Test]
        public void GameplayLevelRequirementsKeepTheirMetadata()
        {
            AssertShape(typeof(RequirementAppFSMValueBool), typeof(RequirementAppFSMValue<bool>), Public, new[] { 1, 2 },
                new (string, int)[0], new[] { Ctor });
            AssertMenu(typeof(RequirementAppFSMValueBool), RequirementsMenu);
            AssertShape(typeof(RequirementAtLeastOneMissionComplete), typeof(RequirementGameplayLevelBase), Public, new[] { 1, 2 },
                new[] { ("m_saveManagerRef", Ref) }, new[] { Met, Ctor });
            AssertMenu(typeof(RequirementAtLeastOneMissionComplete), RequirementsMenu);
            AssertShape(typeof(RequirementChallengeRewardCollected), typeof(RequirementGameplayLevelBase), Public, new[] { 1, 2 },
                new[] { ("m_challengeReward", Plain), ("s_saveManagerRef", 0x0031) }, new[] { Met, Ctor, (".cctor", 0x1891) });
            AssertMenu(typeof(RequirementChallengeRewardCollected), RequirementsMenu);
            AssertSerialized(typeof(RequirementChallengeRewardCollected), "m_challengeReward");
            AssertShape(typeof(RequirementChallengesFeatureSeen), typeof(RequirementGameplayLevelBase), Public, new[] { 1, 2 },
                new[] { ("m_challengeManagerRef", Ref) }, new[] { Met, Ctor });
            AssertMenu(typeof(RequirementChallengesFeatureSeen), RequirementsMenu);
            AssertShape(typeof(RequirementChallengesFeatureUnlocked), typeof(RequirementGameplayLevelBase), Public, new[] { 1, 2 },
                new[] { ("m_challengeManagerRef", Ref), ("m_unlockedStates", Ref) }, new[] { Met, Ctor });
            AssertMenu(typeof(RequirementChallengesFeatureUnlocked), RequirementsMenu);
            AssertShape(typeof(RequirementChallengesOpen), typeof(RequirementGameplayLevelBase), Public, new[] { 2, 1 },
                new[] { ("m_appRef", Ref) }, new[] { Met, Ctor });
            AssertMenu(typeof(RequirementChallengesOpen), RequirementsMenu);
            AssertShape(typeof(RequirementHasNotCompletedMission), typeof(RequirementGameplayLevelBase), Public, new[] { 2, 1 },
                new[] { ("m_missionDefinition", Plain), ("m_dataManagerRef", Ref), ("m_missionManagerRef", Ref) }, new[] { Met, Ctor });
            AssertMenu(typeof(RequirementHasNotCompletedMission), RequirementsMenu);
            AssertSerialized(typeof(RequirementHasNotCompletedMission), "m_missionDefinition");
            AssertShape(typeof(RequirementPlayerHasEnoughRewards), typeof(RequirementGameplayLevelBase), PublicSealed, new[] { 2, 1 },
                new[] { ("m_levelManagerRef", Ref) }, new[] { Met, Ctor });
            AssertMenu(typeof(RequirementPlayerHasEnoughRewards), RequirementsMenu);
            AssertShape(typeof(RequirementPreviousActFirstMissionCompleted), typeof(RequirementGameplayLevelBase), PublicSealed, new[] { 1, 2 },
                new[] { ("m_levelManagerRef", Ref), ("m_missionManagerRef", Ref) }, new[] { Met, Ctor });
            AssertMenu(typeof(RequirementPreviousActFirstMissionCompleted), RequirementsMenu);
            AssertShape(typeof(RequirementPreviousZoneBossDefeated), typeof(RequirementGameplayLevelBase), PublicSealed, new[] { 2, 1 },
                new[] { ("m_levelManagerRef", Ref), ("m_missionManagerRef", Ref) }, new[] { Met, Ctor });
            AssertMenu(typeof(RequirementPreviousZoneBossDefeated), RequirementsMenu);
            AssertShape(typeof(RequirementRewardLevelUnlocked), typeof(RequirementGameplayLevelBase), Public, new[] { 1, 2 },
                new[] { ("m_saveManagerRef", Ref) }, new[] { Met, Ctor });
            AssertMenu(typeof(RequirementRewardLevelUnlocked), RequirementsMenu);
            AssertShape(typeof(RequirementRewardTrackUnlocked), typeof(RequirementGameplayLevelBase), Public, new[] { 2, 1 },
                new[] { ("m_rewardTrackType", Plain), ("m_saveManagerRef", Ref) }, new[] { Met, Ctor });
            AssertMenu(typeof(RequirementRewardTrackUnlocked), RequirementsMenu);
            AssertSerialized(typeof(RequirementRewardTrackUnlocked), "m_rewardTrackType");
            AssertShape(typeof(RequirementSpecificMissionComplete), typeof(RequirementGameplayLevelBase), Public, new[] { 1, 2 },
                new[] { ("m_missionDefinition", Plain), ("m_dataManagerRef", Ref), ("m_saveManagerRef", Ref) }, new[] { Met, Ctor });
            AssertMenu(typeof(RequirementSpecificMissionComplete), RequirementsMenu);
            AssertSerialized(typeof(RequirementSpecificMissionComplete), "m_missionDefinition");
        }

        [Test]
        public void TheGenericAppValueRequirementKeepsItsConstraintAndSerializedFields()
        {
            Type type = typeof(RequirementAppFSMValue<>);
            AssertShape(type, typeof(RequirementGameplayLevelBase), 0x00100081, new[] { 1, 2 },
                new[] { ("m_appRef", Ref), ("m_appFsmKey", Plain), ("m_valueToMatch", Plain), ("m_default", Plain), ("m_cachedKey", Plain) },
                new[] { Met, (".ctor", 0x1884) });
            Type parameter = type.GetGenericArguments().Single();
            Assert.That(parameter.GetGenericParameterConstraints(), Is.EqualTo(new[] { typeof(IEquatable<>).MakeGenericType(parameter) }));
            AssertSerialized(type, "m_appFsmKey", "m_valueToMatch", "m_default");
            Assert.That(Tooltip(Field(type, "m_default")), Is.EqualTo("Value to use if key is not present in App FSM storage"));
        }

        [Test]
        public void MissionGroupAndCategoryRequirementsKeepTheirMetadata()
        {
            AssertShape(typeof(RequirementAllPersistentMissionsAreComplete), typeof(RequirementMissionGroupBase), PublicSealed, new[] { 2, 1 },
                new (string, int)[0], new[] { Met, Ctor });
            AssertMenu(typeof(RequirementAllPersistentMissionsAreComplete), DefinitionsMenu);
            AssertShape(typeof(RequirementMissionGroupGatedByPlayerOrbs), typeof(RequirementMissionGroupBase), PublicSealed, new[] { 1, 2 },
                new[] { ("m_orbs", Plain) }, new[] { Met, Ctor });
            AssertMenu(typeof(RequirementMissionGroupGatedByPlayerOrbs), DefinitionsMenu);
            AssertSerialized(typeof(RequirementMissionGroupGatedByPlayerOrbs), "m_orbs");
            AssertShape(typeof(RequirementMissionGroupShadowFTUEAvailable), typeof(RequirementMissionGroupBase), Public, new[] { 1, 2 },
                new[] { ("m_challengeManagerRef", Ref) }, new[] { Met, Ctor });
            AssertMenu(typeof(RequirementMissionGroupShadowFTUEAvailable), DefinitionsMenu);
            AssertShape(typeof(RequirementMissionsCompleted), typeof(RequirementMissionGroupBase), PublicSealed, new[] { 2, 1 },
                new[] { ("m_missionDefinitions", Plain), ("m_levelManagerRef", Ref), ("m_missionManagerRef", Ref) }, new[] { Met, Ctor });
            AssertMenu(typeof(RequirementMissionsCompleted), DefinitionsMenu);
            AssertSerialized(typeof(RequirementMissionsCompleted), "m_missionDefinitions");
            AssertShape(typeof(RequirementPreviousMissionGroupsAreComplete), typeof(RequirementMissionGroupBase), PublicSealed, new[] { 1, 2 },
                new (string, int)[0], new[] { Met, Ctor });
            AssertMenu(typeof(RequirementPreviousMissionGroupsAreComplete), DefinitionsMenu);
            AssertShape(typeof(RequirementLockCategoryByBoss), typeof(RequirementLevelCategoryBase), PublicSealed, new[] { 1, 2 },
                new[] { ("m_saveManagerRef", Ref), ("m_missionManagerRef", Ref), ("m_levelManagerRef", Ref) }, new[] { Met, Ctor });
            AssertMenu(typeof(RequirementLockCategoryByBoss), RequirementsMenu);
        }

        [Test]
        public void PromptSaveSequencedAndLeaderboardTypesKeepTheirMetadata()
        {
            AssertShape(typeof(PromptTriggerHideAll), typeof(MonoBehaviour), Public, new[] { 2, 1 },
                new[] { ("m_promptManagerRef", Ref) }, new[] { ("Action_HideAllActivePrompts", 0x0086), Ctor });
            AssertShape(typeof(SaveGameUIActions), typeof(MonoBehaviour), Public, new[] { 1, 2 },
                new (string, int)[0], new[] { ("Action_DeleteAllSaves", 0x0086), ("Action_ResetAnalyticsData", 0x0086), Ctor });
            AssertShape(typeof(SequencedPromptTrigger), typeof(SequencedEffect), Public, new[] { 2, 1 },
                new[] { ("m_promptTrigger", Plain) },
                new[] { ("OnEffectTriggered", 0x00c4), ("OnPromptHidden", 0x0081), ("Reset", 0x0081), Ctor });
            AssertSerialized(typeof(SequencedPromptTrigger), "m_promptTrigger");
            AssertShape(typeof(TimeTrialLeaderboardDev), typeof(object), PublicSealed, new[] { 1, 2 },
                new[] { ("DebugMenuRoot", 0x8051), ("m_debugMenuTimeTrials", Ref), ("m_leaderboardsByLabel", Ref), ("m_debugMenuRef", Ref) },
                new[] { Ctor, ("IsReady", 0x01e6), ("SubmitBestTime", 0x01e6), ("ShowLeaderboard", 0x01e6), ("IssueChallenge", 0x01e6),
                    ("Shutdown", 0x01e6), ("GetDefaultMessage", 0x0091), ("GetBestTimeLabel", 0x0091) });
            Assert.That(typeof(TimeTrialLeaderboardDev).GetInterfaces(), Is.EqualTo(new[] { typeof(ITimeTrialLeaderboard) }));
            Assert.That(Field(typeof(TimeTrialLeaderboardDev), "DebugMenuRoot").GetRawConstantValue(), Is.EqualTo("Leaderboards"));
        }
    }
}
