using System;
using System.Collections.Generic;
using Hardlight;
using Hardlight.Enums;
using Hardlight.Utils;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace HardlightProject
{
	[CreateAssetMenu(fileName = "GameplayLevelDefinition", menuName = "HardlightProject/DefinitionData/Definitions/GameplayLevelDefinition")]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class GameplayLevelDefinition : GameplayLevelUIEntry
	{
		[SerializeField]
		[HashEnum(typeof(Strings))]
		private Strings m_displayName = Strings.NONE;

		[HashEnum(typeof(Strings))]
		[SerializeField]
		private Strings m_actName = Strings.NONE;

		[SerializeField]
		private LevelSetupTypes m_levelSetupType = LevelSetupTypes.Standard;

		[SerializeField]
		private bool m_showInLevelSelect = true;

		[SerializeField]
		private MissionList m_missionList;

		[SerializeField]
		private bool m_loadAllMissions;

		[HashEnum(typeof(HLAudioClipIdentifier))]
		[SerializeField]
		private HLAudioClipIdentifier m_backgroundAudioClip;

		[SerializeField]
		private List<AssetLabelReference> m_preloadAssetLabels = new List<AssetLabelReference>();

		[SerializeField]
		private string m_sceneName;

		[SerializeField]
		private bool m_isBossLevel;

		[SerializeField]
		[Tooltip("If true, this level will be ignored when determining what the \"previous\" level is from another,or when considering whether any mission/act in a zone is complete.")]
		private bool m_excludeFromLevelOrdering;

		[Tooltip("Collected rewards needed to unlock this act.")]
		[Header("Requirements")]
		[SerializeField]
		private RewardRequirement[] m_rewardRequirements;

		[SerializeField]
		private List<RequirementGameplayLevelBase> m_requirements;

		[SerializeField]
		private List<RequirementGameplayLevelBase> m_requirementsToShowInLevelSelect;

		[SerializeField]
		[Tooltip("Widget to instantiate as part of the unlocked screen.")]
		private UIWidgetProgression m_progressionUnlockWidget;

		[Tooltip("Which characters can enter this level? When empty, any character can enter.")]
		[SerializeField]
		private List<CharacterDefinition> m_exclusiveCharacters;

		[SerializeField]
		private bool m_alwaysUnlocked;

		[Header("UI")]
		[SerializeField]
		private AssetReferenceT<Sprite> m_loadingImageReference;

		[SerializeField]
		private ZoneThemeOverride m_zoneThemeOverride;

		[SerializeField]
		[Header("Lighting")]
		private bool m_shouldOverrideShadowDistance;

		[SerializeField]
		[ShowIf("m_shouldOverrideShadowDistance", null)]
		private float m_shadowDistance;

		private static CoreGameConfiguration m_coreGameConfiguration;

		private ManagedAddressableAsset<Sprite> m_managedLoadingImage;

        // Game.Runtime 0x06001837..0x06001848: original direct accessors; both name keys use the same field.
        public Strings DisplayName => m_displayName;
        public Strings DisplayNameStringKey => m_displayName;
        public Strings ActName => m_actName;
        public LevelSetupTypes LevelSetupType => m_levelSetupType;
        public bool ShowInLevelSelect => m_showInLevelSelect;
        public bool IsBossLevel => m_isBossLevel;
        public bool LoadAllMissions => m_loadAllMissions;
        public MissionList MissionList => m_missionList;
        public HLAudioClipIdentifier BackgroundAudioClip => m_backgroundAudioClip;
        public List<AssetLabelReference> PreloadAssetLabels => m_preloadAssetLabels;
        public RewardRequirement[] RewardRequirements => m_rewardRequirements;
        public List<CharacterDefinition> ExclusiveCharacters => m_exclusiveCharacters;
        public bool ShouldOverrideShadowDistance => m_shouldOverrideShadowDistance;
        public float ShadowDistance => m_shadowDistance;
        public UIWidgetProgression ProgressionUnlockWidget => m_progressionUnlockWidget;
        public bool AlwaysUnlocked => m_alwaysUnlocked;
        public ZoneThemeOverride ZoneThemeOverride => m_zoneThemeOverride;
        public bool ExcludeFromLevelOrdering => m_excludeFromLevelOrdering;

        // Game.Runtime 0x06001849: Unity null/destroyed equality and exact cached original configuration.
        public string SceneName
        {
            get
            {
                if (m_coreGameConfiguration == null)
                    m_coreGameConfiguration = SystemConfiguration.GetConfig<CoreGameConfiguration>();
                return m_coreGameConfiguration.UseExportedScenes ? m_sceneName + "_exported" : m_sceneName;
            }
        }

        // Game.Runtime 0x0600184a/0x0600184b: genuine RET bodies, not runtime mutation.
        public override void SetData(string sceneName) { }
        public void SetData(string sceneName, bool showInLevelSelect) { }
        // Game.Runtime 0x0600184c: tailcall to the original SceneName accessor.
        public override string GetName() => SceneName;

        // Game.Runtime 0x0600184d: repeated original Groups reads, and output written only at the end.
        public bool TryGetMissionGroups(out IReadOnlyList<MissionGroup> groups)
        {
            if (m_missionList == null || m_missionList.Groups == null || m_missionList.Groups.Count < 1)
            {
                groups = null;
                return false;
            }
            groups = m_missionList.Groups;
            return true;
        }

        // Game.Runtime 0x0600184e: local override precedes debug-unlock lookup; first failing requirement stops.
        public bool MeetsAllRequirements()
        {
            if (m_alwaysUnlocked || DebugUnlockLevels.AreAllLevelsUnlocked()) return true;
            foreach (var requirement in m_requirements)
                if (!requirement.RequirementsMet(this)) return false;
            return true;
        }

        // Game.Runtime 0x0600184f: hidden flag wins even under local/debug unlock.
        public bool ShouldShowInLevelSelect()
        {
            if (!m_showInLevelSelect) return false;
            if (m_alwaysUnlocked || DebugUnlockLevels.AreAllLevelsUnlocked()) return true;
            foreach (var requirement in m_requirementsToShowInLevelSelect)
                if (!requirement.RequirementsMet(this)) return false;
            return true;
        }

        // Game.Runtime 0x06001850: only the debug override bypasses this search.
        public bool HasRequirement<T>() where T : RequirementGameplayLevelBase
        {
            if (DebugUnlockLevels.AreAllLevelsUnlocked()) return false;
            foreach (var requirement in m_requirements)
                if (requirement is T) return true;
            return false;
        }

        // Game.Runtime 0x06001851: evaluate every matching requirement even after one fails.
        public bool LockedByRequirement<T>() where T : RequirementGameplayLevelBase
        {
            if (DebugUnlockLevels.AreAllLevelsUnlocked()) return false;
            bool locked = false;
            foreach (var requirement in m_requirements)
                if (requirement is T) locked |= !requirement.RequirementsMet(this);
            return locked;
        }

        // Game.Runtime 0x06001852: create the wrapper lazily, but Load occurs only for a non-null callback.
        public void LoadLevelImage(Action<Sprite> callback)
        {
            if (m_loadingImageReference.RuntimeKeyIsValid())
            {
                if (m_managedLoadingImage == null)
                    m_managedLoadingImage = new ManagedAddressableAsset<Sprite>(m_loadingImageReference);
                if (callback != null) callback(m_managedLoadingImage.Load());
            }
            else if (callback != null) callback(null);
        }

        // Game.Runtime 0x06001853: retained wrapper, no field reset after release.
        public void UnloadLevelImage()
        {
            if (m_managedLoadingImage != null) m_managedLoadingImage.Unload();
        }

        // Game.Runtime 0x06001854: Unlike the zone aggregate, a None following a non-None mission is inconsistent.
        public CollectableType GetRewardType()
        {
            if (m_missionList == null) return CollectableType.None;
            CollectableType rewardType = CollectableType.None;
            foreach (var mission in m_missionList.GetMissions(false))
            {
                CollectableType current = mission.RewardType;
                if (rewardType == CollectableType.None || rewardType == current)
                {
                    rewardType = current;
                    continue;
                }
                HLOutput.LogError(string.Concat(new[] {
                    "Missions for level: ", name, " do not use a consistent reward type. Mission ",
                    mission.name, " is using ", mission.RewardType.GetString(), ", the rest seem to be ", rewardType.GetString()
                }), null);
                break;
            }
            return rewardType;
        }

        // Game.Runtime 0x06001855: original initializers run before the genuine GUID base constructor.
        public GameplayLevelDefinition() { }
    }
}
