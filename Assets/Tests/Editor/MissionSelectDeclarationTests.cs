using System;
using System.Linq;
using System.Reflection;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // Declarations of the mission select and active mission UI types (Game.Runtime 0x02000876 UIContainerActiveMission,
    // 0x02000879 UIContainerBonusZone, 0x02000942 UIContainerSkipMissionIntro, 0x0200084d UIWidgetForGameplayLevelDefinition,
    // 0x020009c4 UIWidgetSDTLevel, 0x020009d4 UIWidgetToggleMission, 0x020009d5 UIWidgetToggleMissionParameters) against the
    // DummyDll metadata rows: type flags, member order and flags, the Il2CppSetOption order, serialized-field attributes.
    public sealed class MissionSelectDeclarationTests
    {
        private static void AssertSerializeFieldOnly(Type type, params string[] fields)
        {
            foreach (string field in fields)
                Assert.That(AttributeTypes(type.GetField(field, Declared)), Is.EqualTo(new[] { typeof(SerializeField) }), field);
        }

        // dump.cs TypeDefIndex 2165 and the DummyDll rows (fields 0x040020b6..0x040020e0, methods 0x060030e9..0x060030f7).
        [Test]
        public void ActiveMissionDeclarationsMatchTheOriginalMetadata()
        {
            AssertMembers(typeof(UIContainerActiveMission), 0x00100101,
                new (string, int)[]
                {
                    ("m_characterArchetypeImageSpawner", 0x0001), ("m_timeFormatString", 0x0001), ("m_includeIslandOverrides", 0x0001),
                    ("m_syncRankTimeWithMissionTime", 0x0001), ("m_useMissionTimeRankIcon", 0x0001), ("m_showBestTime", 0x0001),
                    ("m_hideRankIfBestTimeIsInvalid", 0x0001), ("m_missionIconAnimation", 0x0001),
                    ("ShowMission", 0x0026), ("MissionDescription", 0x0026), ("MissionIcon", 0x0026), ("MissionIconBackground", 0x0026),
                    ("MissionIconForeground", 0x0026), ("MissionComplete", 0x0026), ("IsUnlocked", 0x0026), ("IsChallenge", 0x0026),
                    ("TimeElapsedFormatted", 0x0026), ("TimeRemainingFormatted", 0x0026), ("TimeTotalSeconds", 0x0026),
                    ("ShowBestTime", 0x0026), ("NewBestTime", 0x0026), ("BestTimeElapsedFormatted", 0x0026), ("BestTimeRankIcon", 0x0026),
                    ("BestTimeRankIconBackground", 0x0026), ("BestTimeRankIconForeground", 0x0026), ("ShowRank", 0x0026),
                    ("NextRankTimeToBeatFormatted", 0x0026), ("NextRankIcon", 0x0026), ("NextRankIconBackground", 0x0026),
                    ("NextRankIconForeground", 0x0026), ("LowestRankTimeElapsedFormatted", 0x0026), ("LowestRankIcon", 0x0026),
                    ("LowestRankIconBackground", 0x0026), ("LowestRankIconForeground", 0x0026),
                    ("m_appRef", 0x0021), ("m_saveManagerRef", 0x0021), ("m_missionManagerRef", 0x0021), ("m_managedAddressableSprites", 0x0021),
                    ("m_missionManager", 0x0001), ("m_currentMissionState", 0x0001), ("m_currentMissionRank", 0x0001), ("m_showRank", 0x0001),
                    ("m_shouldPlayRankAudio", 0x0001),
                },
                new (string, int)[]
                {
                    ("Start", 0x0081), ("OnDestroy", 0x0081), ("Update", 0x0081), ("OnMissionManagerValid", 0x0081),
                    ("OnMissionIslandActive", 0x0081), ("Refresh", 0x0086), ("Refresh", 0x0086), ("UpdateMissionDescription", 0x0081),
                    ("SetRankAudioActive", 0x0086), ("PlayRankAudio", 0x0086), ("UpdateIconography", 0x0081), ("SynchroniseRankTimes", 0x0081),
                    ("SetIcon", 0x0081), ("AdjustForObjectives", 0x0091), (".ctor", 0x1886),
                });
            AssertIl2CppOptions(typeof(UIContainerActiveMission), 1, 2);
            Assert.That(typeof(UIContainerActiveMission).BaseType, Is.EqualTo(typeof(UIContainer)));
            AssertSerializeFieldOnly(typeof(UIContainerActiveMission), "m_characterArchetypeImageSpawner");
            // The serialized order of Tooltip and SerializeField is the declaration order of the original attributes.
            FieldInfo icon = typeof(UIContainerActiveMission).GetField("m_missionIconAnimation", Declared);
            Assert.That(AttributeTypes(icon), Is.EqualTo(new[] { typeof(SerializeField), typeof(TooltipAttribute) }));
            Assert.That(Tooltip(icon), Is.EqualTo("Optional animation to play on the mission icon, for situations such as when the ranks are changing."));
            var tooltips = new (string field, string text)[]
            {
                ("m_timeFormatString", "Elapsed time format. Parameters: {0} minutes, {1} seconds."),
                ("m_includeIslandOverrides", "Will display mission information for sub-missions if they have an island override."),
                ("m_syncRankTimeWithMissionTime", "By default the rank times are calculated once at the start, with this option the container will continuously synchronise the rank times with the mission times."),
                ("m_useMissionTimeRankIcon", "By default the mission icon will be set from definition, if this is true then the icon will be rank achieved by the mission's elapsed time."),
                ("m_showBestTime", "By default the best time is not displayed, this turns it on for time trial missions."),
                ("m_hideRankIfBestTimeIsInvalid", "Hide rank information, if the best time is invalid."),
            };
            foreach ((string field, string text) in tooltips)
            {
                FieldInfo serialized = typeof(UIContainerActiveMission).GetField(field, Declared);
                Assert.That(AttributeTypes(serialized), Is.EqualTo(new[] { typeof(TooltipAttribute), typeof(SerializeField) }), field);
                Assert.That(Tooltip(serialized), Is.EqualTo(text), field);
            }
        }

        // dump.cs TypeDefIndex 2169 / 2168 and the DummyDll rows (methods 0x06003100..0x06003109, nested fields 0x040020f5..0x040020f6).
        [Test]
        public void BonusZoneDeclarationsMatchTheOriginalMetadata()
        {
            AssertMembers(typeof(UIContainerBonusZone), 0x00100101,
                new (string, int)[]
                {
                    ("m_bonusZoneCategory", 0x0001), ("m_revealLevelDefinition", 0x0001), ("m_levelsWaitForReveal", 0x0001),
                    ("m_widgetToLevelPairs", 0x0001), ("m_zoneIcon", 0x0001), ("m_levelNumericalNameOffset", 0x0001), ("m_continueEvent", 0x0001),
                    ("m_revealAnimation", 0x0001), ("ShowReveal", 0x0026), ("m_app", 0x0001), ("m_levelManager", 0x0001), ("m_saveManager", 0x0001),
                    ("m_progressionManager", 0x0001), ("m_selectedLevelDefinition", 0x0001), ("m_revealIsDoneAnimating", 0x0001),
                },
                new (string, int)[]
                {
                    ("Start", 0x0081), ("Action_Continue", 0x0086), ("Action_FinishRevealAnimation", 0x0086), ("Continue", 0x0081),
                    ("SetupZone", 0x0081), ("SetupLevels", 0x0081), ("SelectLevelDefinition", 0x0081), (".ctor", 0x1886),
                });
            AssertIl2CppOptions(typeof(UIContainerBonusZone), 1, 2);
            Assert.That(typeof(UIContainerBonusZone).BaseType, Is.EqualTo(typeof(UIContainer)));
            AssertSerializeFieldOnly(typeof(UIContainerBonusZone), "m_bonusZoneCategory", "m_revealLevelDefinition", "m_levelsWaitForReveal",
                "m_widgetToLevelPairs", "m_zoneIcon", "m_levelNumericalNameOffset", "m_continueEvent", "m_revealAnimation");

            // The level widget listener closure: the compiler names it after the SetupLevels member ordinal (20).
            Type closure = typeof(UIContainerBonusZone).GetNestedType("<>c__DisplayClass20_0", BindingFlags.NonPublic);
            Assert.That(closure, Is.Not.Null);
            AssertMembers(closure, 0x00100103,
                new (string, int)[] { ("levelDefinition", 0x0006), ("<>4__this", 0x0006) },
                new (string, int)[] { (".ctor", 0x1886), ("<SetupLevels>b__0", 0x0083) });
        }

        // dump.cs TypeDefIndex 2369 and the DummyDll rows (methods 0x06003529..0x0600352b).
        [Test]
        public void SkipMissionIntroDeclarationsMatchTheOriginalMetadata()
        {
            AssertMembers(typeof(UIContainerSkipMissionIntro), 0x00100001,
                new (string, int)[] { ("m_levelManager", 0x0001) },
                new (string, int)[] { ("Start", 0x0081), ("Action_OnFullSkip", 0x0086), (".ctor", 0x1886) });
            AssertIl2CppOptions(typeof(UIContainerSkipMissionIntro), 2, 1);
            Assert.That(typeof(UIContainerSkipMissionIntro).BaseType, Is.EqualTo(typeof(UIContainer)));
            Assert.That(AttributeTypes(typeof(UIContainerSkipMissionIntro).GetField("m_levelManager", Declared)), Is.Empty);
        }

        // dump.cs TypeDefIndex 2124 and the DummyDll rows (a Serializable sealed class, methods 0x06003017..0x06003019).
        [Test]
        public void LevelWidgetPairDeclarationsMatchTheOriginalMetadata()
        {
            AssertMembers(typeof(UIWidgetForGameplayLevelDefinition), 0x00102101,
                new (string, int)[] { ("m_gameplayLevelDefinition", 0x0001), ("m_widgetSDTLevel", 0x0001) },
                new (string, int)[] { ("get_LevelDefinition", 0x0886), ("get_Widget", 0x0886), (".ctor", 0x1886) });
            AssertIl2CppOptions(typeof(UIWidgetForGameplayLevelDefinition), 1, 2);
            Assert.That(typeof(UIWidgetForGameplayLevelDefinition).BaseType, Is.EqualTo(typeof(object)));
            AssertSerializeFieldOnly(typeof(UIWidgetForGameplayLevelDefinition), "m_gameplayLevelDefinition", "m_widgetSDTLevel");
        }

        // dump.cs TypeDefIndex 2499 and the DummyDll rows (methods 0x06003831..0x0600384c).
        [Test]
        public void SdtLevelDeclarationsMatchTheOriginalMetadata()
        {
            AssertMembers(typeof(UIWidgetSDTLevel), 0x00100101,
                new (string, int)[]
                {
                    ("m_rewardType", 0x0001), ("m_toggle", 0x0001), ("m_fillImage", 0x0001), ("m_unlockAnimationClip", 0x0001),
                    ("m_bossPreviewAnimationClip", 0x0001), ("m_uiWidgetNavigation", 0x0001), ("m_useNumericalName", 0x0001),
                    ("m_rewardRequirementsParent", 0x0001), ("m_rewardRequirementUIContainerPrefab", 0x0001), ("m_canvasGroup", 0x0001),
                    ("m_onLevelHidden", 0x0001), ("LevelName", 0x0026), ("IsNextLevel", 0x0026), ("LevelOrbsCollected", 0x0026),
                    ("LevelTotalOrbs", 0x0026), ("FormatXOfY", 0x0026), ("ShowRewardsLeftToUnlockMessage", 0x0026),
                    ("RewardsUnlockMessage", 0x0026), ("RedStarRingsShowState", 0x0026), ("BlueCoinsShowState", 0x0026),
                    ("JukeboxShowState", 0x0026), ("RankShowState", 0x0026), ("IsNew", 0x0026), ("IsLocked", 0x0026), ("RewardIcon", 0x0026),
                    ("m_parameters", 0x0001), ("m_navigationManagerRef", 0x0021), ("m_dataManagerRef", 0x0021), ("m_missionManager", 0x0001),
                    ("m_rewardRestrictionInstances", 0x0021),
                },
                new (string, int)[]
                {
                    ("Start", 0x0081), ("OnDestroy", 0x0081), ("Setup", 0x00c6), ("OnEnable", 0x0081), ("OnDisable", 0x0081),
                    ("Refresh", 0x0081), ("UpdateBinders", 0x0081), ("GetMissionShowType", 0x0081), ("GetRankShowType", 0x0081),
                    ("ShowRewardRequirementUI", 0x0081), ("GetRewardRemainingCount", 0x0081), ("GetCollectableDefinition", 0x0081),
                    ("Lock", 0x0081), ("Unlock", 0x0081), ("OnUnlockComplete", 0x0081), ("Hide", 0x0081), ("HideBoss", 0x0081),
                    ("IsHiddenBoss", 0x0081), ("BossRankLevelsHidden", 0x0081), ("Select", 0x0086), ("Action_OnNavigateTo", 0x0086),
                    ("DeselectWithoutNotify", 0x0086), ("AddValueChangeListener", 0x0086), ("ToggleWidgetNavigation", 0x0086),
                    ("TryNavigateToWidget", 0x0081), ("GetProgressionPercent", 0x0081), (".ctor", 0x1886), ("<HideBoss>b__46_0", 0x0081),
                });
            AssertIl2CppOptions(typeof(UIWidgetSDTLevel), 1, 2);
            Assert.That(typeof(UIWidgetSDTLevel).BaseType, Is.EqualTo(typeof(UIWidget)));
            AssertSerializeFieldOnly(typeof(UIWidgetSDTLevel), "m_rewardType", "m_toggle", "m_fillImage", "m_unlockAnimationClip",
                "m_bossPreviewAnimationClip", "m_uiWidgetNavigation", "m_useNumericalName", "m_rewardRequirementsParent",
                "m_rewardRequirementUIContainerPrefab", "m_canvasGroup", "m_onLevelHidden");
            ParameterInfo select = typeof(UIWidgetSDTLevel).GetMethod("Select").GetParameters().Single();
            Assert.That(select.HasDefaultValue, Is.True);
            Assert.That(select.DefaultValue, Is.EqualTo(true));
        }

        // dump.cs TypeDefIndex 2515 and the DummyDll rows (methods 0x0600388c..0x06003896).
        [Test]
        public void ToggleMissionDeclarationsMatchTheOriginalMetadata()
        {
            AssertMembers(typeof(UIWidgetToggleMission), 0x00100101,
                new (string, int)[]
                {
                    ("m_unlockAnimationClip", 0x0001), ("Complete", 0x0026), ("IsNew", 0x0026), ("ShowRankIcon", 0x0026),
                    ("SubRewardsAnchorRight", 0x0026), ("RankIconBase", 0x0026), ("RankIconBackground", 0x0026), ("RankIconForeground", 0x0026),
                    ("RedStarRingsShowState", 0x0026), ("BlueCoinsShowState", 0x0026), ("JukeboxShowState", 0x0026), ("RewardImage", 0x0026),
                    ("m_parameters", 0x0001), ("m_dataManagerRef", 0x0021), ("m_baseAddressableAsset", 0x0001),
                    ("m_backgroundAddressableAsset", 0x0001), ("m_foregroundAddressableAsset", 0x0001),
                },
                new (string, int)[]
                {
                    ("Setup", 0x00c6), ("OnDisable", 0x0081), ("SetIsNew", 0x0086), ("Lock", 0x0081), ("IsReadyToUnlock", 0x0081),
                    ("AnimateUnlock", 0x0081), ("SetRankIconVisibility", 0x0086), ("SetRankIconBase", 0x0086),
                    ("SetRankIconBackground", 0x0086), ("SetRankIconForeground", 0x0086), (".ctor", 0x1886),
                });
            AssertIl2CppOptions(typeof(UIWidgetToggleMission), 2, 1);
            Assert.That(typeof(UIWidgetToggleMission).BaseType, Is.EqualTo(typeof(UIWidgetToggle)));
            AssertSerializeFieldOnly(typeof(UIWidgetToggleMission), "m_unlockAnimationClip");
        }

        // dump.cs TypeDefIndex 2516 and the DummyDll rows (nine read-only auto-properties, constructor 0x060038a0).
        [Test]
        public void ToggleMissionParametersDeclarationsMatchTheOriginalMetadata()
        {
            AssertMembers(typeof(UIWidgetToggleMissionParameters), 0x00100001,
                new (string, int)[]
                {
                    ("<Complete>k__BackingField", 0x0021), ("<Locked>k__BackingField", 0x0021), ("<UnlockSeen>k__BackingField", 0x0021),
                    ("<SubRewardsAnchorRight>k__BackingField", 0x0021), ("<IsNewState>k__BackingField", 0x0021),
                    ("<RedStarRingsState>k__BackingField", 0x0021), ("<BlueCoinsState>k__BackingField", 0x0021),
                    ("<JukeboxState>k__BackingField", 0x0021), ("<RewardType>k__BackingField", 0x0021),
                },
                new (string, int)[]
                {
                    ("get_Complete", 0x0886), ("get_Locked", 0x0886), ("get_UnlockSeen", 0x0886), ("get_SubRewardsAnchorRight", 0x0886),
                    ("get_IsNewState", 0x0886), ("get_RedStarRingsState", 0x0886), ("get_BlueCoinsState", 0x0886),
                    ("get_JukeboxState", 0x0886), ("get_RewardType", 0x0886), (".ctor", 0x1886),
                });
            AssertIl2CppOptions(typeof(UIWidgetToggleMissionParameters), 2, 1);
            Assert.That(typeof(UIWidgetToggleMissionParameters).BaseType, Is.EqualTo(typeof(UIWidgetToggleParameters)));
        }
    }
}
