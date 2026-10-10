using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Hardlight;
using UnityEngine;
using GameVersion = Hardlight.Utils.Version;

namespace HardlightProject
{
    // Native Game.Runtime DataManager0200052c: complete64-field original contract.
    // Private recovery candidate: genuine dependency graph and ordinary compiler identity remain unverified.
    public class DataManager : MonoBehaviour, ISystem
    {
        [SerializeField]
        private DataDefinitions m_dataDefinitions;
        [SerializeField]
        private StateMachinesGroup m_applicationStateMachines;
        [SerializeField]
        private StateMachinesGroup m_bossStateMachines;
        [SerializeField]
        private StateMachinesGroup m_characterStateMachines;
        [SerializeField]
        private StateMachinesGroup m_enemyStateMachines;

        public float Progress { get; private set; }
        public Dictionary<CharacterId, CharacterDefinition> Characters = new Dictionary<CharacterId, CharacterDefinition>(HardlightEnumComparers.CharacterIdComparer);
        public Dictionary<CharacterArchetype, CharacterArchetypeDefinition> CharacterArchetypes = new Dictionary<CharacterArchetype, CharacterArchetypeDefinition>(HardlightEnumComparers.CharacterArchetypeComparer);
        public Dictionary<CollectableType, CollectableDefinition> CollectableDefinitions = new Dictionary<CollectableType, CollectableDefinition>(HardlightEnumComparers.CollectableTypeComparer);
        public Dictionary<RailType, RailDefinition> RailDefinitions = new Dictionary<RailType, RailDefinition>(HardlightEnumComparers.RailTypeComparer);
        public Dictionary<TransporterType, TransporterDefinition> TransporterDefinitions = new Dictionary<TransporterType, TransporterDefinition>(HardlightEnumComparers.TransporterTypeComparer);
        public Dictionary<LightspeedDashType, LightspeedDashDefinition> LightspeedDashDefinitions = new Dictionary<LightspeedDashType, LightspeedDashDefinition>(HardlightEnumComparers.LightspeedDashTypeComparer);
        public Dictionary<HalfPipeType, HalfPipeDefinition> HalfPipeDefinitions = new Dictionary<HalfPipeType, HalfPipeDefinition>(HardlightEnumComparers.HalfPipeTypeComparer);
        public Dictionary<string, ActorAnimationDefinition> ActorStateAnimationDefinitions = new Dictionary<string, ActorAnimationDefinition>();
        public Dictionary<string, AbilityDefinition> AbilityDefinitions = new Dictionary<string, AbilityDefinition>();
        public Dictionary<string, FiniteStateMachineScriptableObject> ApplicationStateMachines = new Dictionary<string, FiniteStateMachineScriptableObject>();
        public Dictionary<string, FiniteStateMachineScriptableObject> BossStateMachines = new Dictionary<string, FiniteStateMachineScriptableObject>();
        public Dictionary<string, FiniteStateMachineScriptableObject> CharacterStateMachines = new Dictionary<string, FiniteStateMachineScriptableObject>();
        public Dictionary<string, FiniteStateMachineScriptableObject> EnemyStateMachines = new Dictionary<string, FiniteStateMachineScriptableObject>();
        public Dictionary<string, UIModernEvent> UIModernEvents = new Dictionary<string, UIModernEvent>();
        public Dictionary<RuntimePlatform, DevicePerformance> DevicePerformanceMatches = new Dictionary<RuntimePlatform, DevicePerformance>();
        public Dictionary<TerrainMetadataType, TerrainMetadataDefinition> TerrainDefinitions = new Dictionary<TerrainMetadataType, TerrainMetadataDefinition>(HardlightEnumComparers.TerrainMetadataTypeComparer);
        public Dictionary<LevelSetupTypes, LevelSetupDefinition> LevelSetupDefinitions = new Dictionary<LevelSetupTypes, LevelSetupDefinition>(HardlightEnumComparers.LevelSetupTypesComparer);
        public Dictionary<HazardType, HazardDefinition> HazardDefinitions = new Dictionary<HazardType, HazardDefinition>(HardlightEnumComparers.HazardTypeComparer);
        public Dictionary<CascadeObjectType, CascadeObjectDefinition> CascadeObjectDefinitions = new Dictionary<CascadeObjectType, CascadeObjectDefinition>(HardlightEnumComparers.CascadeObjectTypeComparer);
        public Dictionary<TerrainEffectType, ActorTerrainEffectsDefinition> ActorTerrainEffectsDefinitions = new Dictionary<TerrainEffectType, ActorTerrainEffectsDefinition>(HardlightEnumComparers.TerrainEffectTypeComparer);
        public Dictionary<GlobalConstantType, GlobalConstantDefinition> GlobalConstantDefinitions = new Dictionary<GlobalConstantType, GlobalConstantDefinition>(HardlightEnumComparers.GlobalConstantTypeComparer);
        public Dictionary<TerrainTrackerType, TerrainTrackerDefinition> TerrainTrackerDefinitions = new Dictionary<TerrainTrackerType, TerrainTrackerDefinition>(HardlightEnumComparers.TerrainTrackerTypeComparer);
        public Dictionary<string, ApplicationStateEvent> ApplicationStateEvents = new Dictionary<string, ApplicationStateEvent>();
        public Dictionary<HLAudioClipIdentifier, HLAudioClipDefinition> AudioClips = new Dictionary<HLAudioClipIdentifier, HLAudioClipDefinition>(HardlightEnumComparers.HLAudioClipIdentifierComparer);
        public Dictionary<HLAudioMixerIdentifier, HLAudioMixerDefinition> AudioMixers = new Dictionary<HLAudioMixerIdentifier, HLAudioMixerDefinition>(HardlightEnumComparers.HLAudioMixerIdentifierComparer);
        public Dictionary<ParticleEffectType, ParticleEffectDefinition> ParticleEffectDefinitions = new Dictionary<ParticleEffectType, ParticleEffectDefinition>(HardlightEnumComparers.ParticleEffectTypeComparer);
        public Dictionary<string, UIContainerIdentifier> Containers = new Dictionary<string, UIContainerIdentifier>();
        public Dictionary<string, TimeScaleDefinition> TimeScaleDefinitions = new Dictionary<string, TimeScaleDefinition>();
        public Dictionary<FullscreenShaderParametersType, FullscreenShaderParametersDefinition> FullscreenShaderParametersDefinitions = new Dictionary<FullscreenShaderParametersType, FullscreenShaderParametersDefinition>(HardlightEnumComparers.FullscreenShaderParametersTypeComparer);
        public Dictionary<HLCutsceneIdentifier, CutsceneDefinition> CutsceneDefinitions = new Dictionary<HLCutsceneIdentifier, CutsceneDefinition>(HardlightEnumComparers.HLCutsceneIdentifierComparer);
        public Dictionary<string, GameplayIslandDefinition> GameplayIslandDefinitions = new Dictionary<string, GameplayIslandDefinition>();
        public Dictionary<ProjectileType, ProjectileDefinition> ProjectileDefinitions = new Dictionary<ProjectileType, ProjectileDefinition>(HardlightEnumComparers.ProjectileTypeComparer);
        public Dictionary<WaypointType, WaypointDefinition> WaypointDefinitions = new Dictionary<WaypointType, WaypointDefinition>(HardlightEnumComparers.WaypointTypeComparer);
        public Dictionary<HomingTargetType, TargetingTypePriorityDefinition> TargetingTypePriorityDefinitions = new Dictionary<HomingTargetType, TargetingTypePriorityDefinition>(HardlightEnumComparers.HomingTargetTypeComparer);
        public Dictionary<OrnamentIdentifier, OrnamentDefinition> OrnamentDefinitions = new Dictionary<OrnamentIdentifier, OrnamentDefinition>(HardlightEnumComparers.OrnamentIdentifierComparer);
        public Dictionary<EnemyType, EnemyDefinition> EnemyDefinitions = new Dictionary<EnemyType, EnemyDefinition>(HardlightEnumComparers.EnemyTypeComparer);
        public Dictionary<IntroSequenceIdentifier, IntroSequenceDefinition> IntroSequenceDefinitions = new Dictionary<IntroSequenceIdentifier, IntroSequenceDefinition>(HardlightEnumComparers.IntroSequenceIdentifierComparer);
        public Dictionary<OutroSequenceIdentifier, OutroSequenceDefinition> OutroSequenceDefinitions = new Dictionary<OutroSequenceIdentifier, OutroSequenceDefinition>();
        public Dictionary<PlayerProgressionTypes, MetaGameUnlockDefinition> MetaGameUnlockDefinitions = new Dictionary<PlayerProgressionTypes, MetaGameUnlockDefinition>();
        public Dictionary<string, CameraProxyTargetSettingsData> CameraProxyTargetSettingsDefinitions = new Dictionary<string, CameraProxyTargetSettingsData>();
        public Dictionary<CharacterAbilityUIType, CharacterAbilityUIDefinition> CharacterAbilityUIDefinitions = new Dictionary<CharacterAbilityUIType, CharacterAbilityUIDefinition>();
        public Dictionary<FadeTransitionType, FadeTransitionDefinition> FadeTransitionDefinitions = new Dictionary<FadeTransitionType, FadeTransitionDefinition>();
        public Dictionary<string, AppRatingRequesterBase> AppRatingRequesters = new Dictionary<string, AppRatingRequesterBase>();
        public Dictionary<string, CharacterStickyControlsDefinition> CharacterStickyControlsDefinitions = new Dictionary<string, CharacterStickyControlsDefinition>();
        public SubtitleCharacterDefinitionGroup SubtitleDefGroup;
        public Dictionary<string, ApplicationVersionDefinition> ApplicationVersionDefinitions = new Dictionary<string, ApplicationVersionDefinition>();
        public readonly Dictionary<MissionDefinition, GameplayLevelDefinition> MissionDefinitionLevelLookup = new Dictionary<MissionDefinition, GameplayLevelDefinition>();
        public Dictionary<RankType, RankDefinition> RankDefinitions = new Dictionary<RankType, RankDefinition>();
        public Dictionary<HLAudioClipIdentifier, MusicTrackDefinition> MusicTrackDefinitions = new Dictionary<HLAudioClipIdentifier, MusicTrackDefinition>();
        public Dictionary<InputType, GameActionGlyphMap> GameActionGlyphMapDefinitions = new Dictionary<InputType, GameActionGlyphMap>();
        public Dictionary<string, DreamPowerDefinition> DreamPowerDefinitions = new Dictionary<string, DreamPowerDefinition>();
        public Dictionary<DreamPowerStoreBandType, DreamPowerStoreBandDefinition> DreamPowerStoreBandDefinitions = new Dictionary<DreamPowerStoreBandType, DreamPowerStoreBandDefinition>();
        public Dictionary<ChallengeZoneIdentifier, ChallengeZoneMaterialDefinition> ChallengeZoneMaterialDefinitions = new Dictionary<ChallengeZoneIdentifier, ChallengeZoneMaterialDefinition>();
        public Dictionary<AchievementIdentifier, AchievementDefinition> AchievementDefinitions = new Dictionary<AchievementIdentifier, AchievementDefinition>();
        public Dictionary<MissionScorerStreakIdentifier, MissionScorerStreakDefinition> MissionScorerStreakDefinitions = new Dictionary<MissionScorerStreakIdentifier, MissionScorerStreakDefinition>();
        public Dictionary<RewardTrackType, RewardTrackDefinition> RewardTrackDefinitions = new Dictionary<RewardTrackType, RewardTrackDefinition>();

        public TouchLayoutConfiguration TouchLayoutConfiguration { get; private set; }

        public AnalyticsConfiguration AnalyticsConfiguration { get; private set; }

        // 06001c32; ARM64 51ef98: lookups precede normal process registration.
        private void Awake()
        {
            InitialiseDataLookups();
            ProcessManager.RegisterSystem(this, null, false, false);
        }

        // 06001c33; ARM64 51f010: preserve native assignment and callback order.
        private void InitialiseDataLookups()
        {
            DataDefinitions allDefinitions = m_dataDefinitions;
            Characters = allDefinitions.Get<CharacterId, CharacterDefinition>();
            CharacterArchetypes = allDefinitions.Get<CharacterArchetype, CharacterArchetypeDefinition>();
            CollectableDefinitions = allDefinitions.Get<CollectableType, CollectableDefinition>();
            RailDefinitions = allDefinitions.Get<RailType, RailDefinition>();
            TransporterDefinitions = allDefinitions.Get<TransporterType, TransporterDefinition>();
            LightspeedDashDefinitions = allDefinitions.Get<LightspeedDashType, LightspeedDashDefinition>();
            HalfPipeDefinitions = allDefinitions.Get<HalfPipeType, HalfPipeDefinition>();
            ActorStateAnimationDefinitions = allDefinitions.Get<string, ActorAnimationDefinition>();
            AbilityDefinitions = allDefinitions.Get<string, AbilityDefinition>();
            ApplicationStateMachines = m_applicationStateMachines.GetData();
            BossStateMachines = m_bossStateMachines.GetData();
            CharacterStateMachines = m_characterStateMachines.GetData();
            EnemyStateMachines = m_enemyStateMachines.GetData();
            UIModernEvents = allDefinitions.Get<string, UIModernEvent>();
            DevicePerformanceMatches = allDefinitions.Get<RuntimePlatform, DevicePerformance>();
            TerrainDefinitions = allDefinitions.Get<TerrainMetadataType, TerrainMetadataDefinition>();
            LevelSetupDefinitions = allDefinitions.Get<LevelSetupTypes, LevelSetupDefinition>();
            HazardDefinitions = allDefinitions.Get<HazardType, HazardDefinition>();
            CascadeObjectDefinitions = allDefinitions.Get<CascadeObjectType, CascadeObjectDefinition>();
            ActorTerrainEffectsDefinitions = allDefinitions.Get<TerrainEffectType, ActorTerrainEffectsDefinition>();
            GlobalConstantDefinitions = allDefinitions.Get<GlobalConstantType, GlobalConstantDefinition>();
            TerrainTrackerDefinitions = allDefinitions.Get<TerrainTrackerType, TerrainTrackerDefinition>();
            ApplicationStateEvents = allDefinitions.Get<string, ApplicationStateEvent>();
            AudioClips = allDefinitions.Get<HLAudioClipIdentifier, HLAudioClipDefinition>();
            AudioMixers = allDefinitions.Get<HLAudioMixerIdentifier, HLAudioMixerDefinition>();
            ParticleEffectDefinitions = allDefinitions.Get<ParticleEffectType, ParticleEffectDefinition>();
            Containers = allDefinitions.Get<string, UIContainerIdentifier>();
            TimeScaleDefinitions = allDefinitions.Get<string, TimeScaleDefinition>();
            FullscreenShaderParametersDefinitions = allDefinitions.Get<FullscreenShaderParametersType, FullscreenShaderParametersDefinition>();
            CutsceneDefinitions = allDefinitions.Get<HLCutsceneIdentifier, CutsceneDefinition>();
            GameplayIslandDefinitions = allDefinitions.Get<string, GameplayIslandDefinition>();
            ProjectileDefinitions = allDefinitions.Get<ProjectileType, ProjectileDefinition>();
            WaypointDefinitions = allDefinitions.Get<WaypointType, WaypointDefinition>();
            TargetingTypePriorityDefinitions = allDefinitions.Get<HomingTargetType, TargetingTypePriorityDefinition>();
            OrnamentDefinitions = allDefinitions.Get<OrnamentIdentifier, OrnamentDefinition>();
            EnemyDefinitions = allDefinitions.Get<EnemyType, EnemyDefinition>();
            IntroSequenceDefinitions = allDefinitions.Get<IntroSequenceIdentifier, IntroSequenceDefinition>();
            OutroSequenceDefinitions = allDefinitions.Get<OutroSequenceIdentifier, OutroSequenceDefinition>();
            MetaGameUnlockDefinitions = allDefinitions.Get<PlayerProgressionTypes, MetaGameUnlockDefinition>();
            CameraProxyTargetSettingsDefinitions = allDefinitions.Get<string, CameraProxyTargetSettingsData>();
            CharacterAbilityUIDefinitions = allDefinitions.Get<CharacterAbilityUIType, CharacterAbilityUIDefinition>();
            FadeTransitionDefinitions = allDefinitions.Get<FadeTransitionType, FadeTransitionDefinition>();
            AppRatingRequesters = allDefinitions.Get<string, AppRatingRequesterBase>();
            CharacterStickyControlsDefinitions = allDefinitions.Get<string, CharacterStickyControlsDefinition>();
            SubtitleDefGroup = allDefinitions.GetGroup<SubtitleCharacterDefinitionGroup>();
            SubtitleDefGroup.InitialiseCache();
            ApplicationVersionDefinitions = allDefinitions.Get<string, ApplicationVersionDefinition>();
            RankDefinitions = allDefinitions.Get<RankType, RankDefinition>();
            MusicTrackDefinitions = allDefinitions.Get<HLAudioClipIdentifier, MusicTrackDefinition>();
            GameActionGlyphMapDefinitions = allDefinitions.Get<InputType, GameActionGlyphMap>();
            DreamPowerDefinitions = allDefinitions.Get<string, DreamPowerDefinition>();
            DreamPowerStoreBandDefinitions = allDefinitions.Get<DreamPowerStoreBandType, DreamPowerStoreBandDefinition>();
            ChallengeZoneMaterialDefinitions = allDefinitions.Get<ChallengeZoneIdentifier, ChallengeZoneMaterialDefinition>();
            AchievementDefinitions = allDefinitions.Get<AchievementIdentifier, AchievementDefinition>();
            MissionScorerStreakDefinitions = allDefinitions.Get<MissionScorerStreakIdentifier, MissionScorerStreakDefinition>();
            RewardTrackDefinitions = allDefinitions.Get<RewardTrackType, RewardTrackDefinition>();
            GetAnalyticsConfiguration(allDefinitions);
            CacheLevelDefinitionsForMissions();
            CacheGameActionGlyphs();
        }

        // 06001c34; ARM6451fb08 is exactly RET in the supplied player.
        private void GetTouchControlConfiguration(DataDefinitions allDefinitions)
        {
        }

        // 06001c35; ARM6451fb0c. Both CLR-null group dereference and null default are retained.
        private void GetAnalyticsConfiguration(DataDefinitions allDefinitions)
        {
            AnalyticsConfiguration = allDefinitions.GetGroup<AnalyticsConfigurationGroup>().Default;
        }

        // 06001c36; ARM64520820, wrapper inlines allocation and ignores host return.
        public void LoadDefinitionData()
        {
            Hardlight.Utils.CoroutineUtils.RunCoroutine(LoadDefinitionData_Internal());
        }

        // 06001c37 + original <LoadDefinitionData_Internal>d__78/06001c3f..44.
        // ARM64522184: state0 resets state, stores float1 and immediately returns false.
        // The compiler's generated identity must be compared after the genuine graph compiles.
        private IEnumerator LoadDefinitionData_Internal()
        {
            Progress = 1.0f;
            yield break;
        }

        // 06001c38; ARM6451fb80. Registered save callbacks may change DefinitionData
        // before SubMissions is read. No lookup clear, recursion or additional validation.
        private void CacheLevelDefinitionsForMissions()
        {
            foreach (GameplayLevelDefinition level in SystemConfiguration.GetConfig<GameLevels>().GetLevels())
            {
                if (level.MissionList == null)
                    continue;
                foreach (MissionGroup group in level.MissionList.Groups)
                {
                    foreach (MissionDefinition mission in group.Definitions)
                    {
                        MissionDefinitionLevelLookup[mission] = level;
                        mission.InitialiseDefinition();
                        foreach (MissionDefinition subMission in mission.SubMissions)
                        {
                            MissionDefinitionLevelLookup[subMission] = level;
                            subMission.InitialiseDefinition();
                        }
                    }
                }
            }
        }

        // 06001c39; ARM64520a34: actual TryGetValue, ignored result and output.
        // No native call from the supplied player's mission-cache path.
        [Conditional("UNITY_EDITOR")]
        private void ValidateMissionLevelAssignment(MissionDefinition missionDefinition, GameplayLevelDefinition definition)
        {
            MissionDefinitionLevelLookup.TryGetValue(missionDefinition, out GameplayLevelDefinition _);
        }

        // 06001c3a; ARM64520aac: original direct challenge field/property dereferences.
        public void CacheLevelDefinitionsForChallenges(IReadOnlyList<ChallengeState> challengeStates)
        {
            foreach (ChallengeState state in challengeStates)
            {
                ChallengeDefinition definition = state.ChallengeDefinition;
                MissionDefinitionLevelLookup[definition.MissionDefinition] = definition.GameplayLevelDefinition;
            }
        }

        // 06001c3b; ARM6452069c: genuine KeyValuePair.Deconstruct then map.Initialise.
        private void CacheGameActionGlyphs()
        {
            foreach (KeyValuePair<InputType, GameActionGlyphMap> entry in GameActionGlyphMapDefinitions)
            {
                entry.Deconstruct(out InputType _, out GameActionGlyphMap map);
                map.Initialise();
            }
        }

        // 06001c3c; ARM64520db4: dictionary value order retained, reverse scan,
        // ordinal3 and last-entry fallback. Empty list throws at index-1; no false result.
        public bool TryGetSubversionOfOrLast(string plainVersion, out ApplicationVersionDefinition versionDefinition)
        {
            GameVersion version = new GameVersion(plainVersion);
            List<ApplicationVersionDefinition> definitions = new List<ApplicationVersionDefinition>(ApplicationVersionDefinitions.Values);
            for (int i = definitions.Count - 1; i >= 0; --i)
            {
                ApplicationVersionDefinition candidate = definitions[i];
                if (candidate.Version.SubversionOf(version, 3))
                {
                    versionDefinition = candidate;
                    return true;
                }
            }
            versionDefinition = definitions[definitions.Count - 1];
            return true;
        }

        // 06001c3d; original shared ARM648dce84 and metadata TGroup:ScriptableObject.
        public TGroup GetGroup<TGroup>() where TGroup : ScriptableObject
        {
            return m_dataDefinitions.GetGroup<TGroup>();
        }
        // 06001c3e; ARM64520f98:55 field initializers precede MonoBehaviour base ctor.
    }
}
