using System.Collections.Generic;
using Hardlight;
using Hardlight.Enums;
using Hardlight.Utils;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Serialization;

namespace HardlightProject
{
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[CreateAssetMenu(fileName = "GameplayLevelCategory", menuName = "HardlightProject/DefinitionData/Definitions/GameplayLevelCategory")]
	public class GameplayLevelCategory : GameplayLevelUIEntry
	{
		[SerializeField]
		[HashEnum(typeof(Strings))]
		private Strings m_title = Strings.NONE;

		[SerializeField]
		[HashEnum(typeof(Strings))]
		private Strings m_subtitle = Strings.NONE;

		[SerializeField]
		[Tooltip("Does this category represent a zone containing gameplay levels?")]
		private bool m_isZone;

		[SerializeField]
		private List<GameplayLevelUIEntry> m_gameplayEntries;

		[SerializeField]
		private List<RequirementLevelCategoryBase> m_requirements;

		[FormerlySerializedAs("m_iconAsset_atlas")]
		[SerializeField]
		private AssetReferenceAtlasedSprite m_iconAsset;

		[SerializeField]
		private Color m_backgroundStartColour = Color.white;

		[SerializeField]
		private Color m_backgroundEndColour = Color.white;

		[Header("UI")]
		[SerializeField]
		private AssetReferenceT<Sprite> m_zoneImageAssetReference;

		[Tooltip("Widget to instantiate as part of the unlock screen.")]
		[SerializeField]
		private UIWidgetProgression m_progressionUnlockWidget;

		public ManagedAddressableAsset<Sprite> ZoneImageAsset;

		public ManagedAddressableAsset<Sprite> ZoneAccent;

		private readonly SystemRef<LevelManager> m_levelManagerRef = ProcessManager.GetSystemRef<LevelManager>(null, true);

        // Game.Runtime 0x06001826..0x0600182e: direct original field accessors.
        public List<GameplayLevelUIEntry> GameplayEntries => m_gameplayEntries;
        public bool IsZone => m_isZone;
        public Strings Title => m_title;
        public Strings Subtitle => m_subtitle;
        public List<RequirementLevelCategoryBase> Requirements => m_requirements;
        public AssetReferenceAtlasedSprite IconAsset => m_iconAsset;
        public Color BackgroundStartColour => m_backgroundStartColour;
        public Color BackgroundEndColour => m_backgroundEndColour;
        public UIWidgetProgression ProgressionUnlockWidget => m_progressionUnlockWidget;

        // Game.Runtime 0x0600182f: replace both wrappers, in this order, even for null references.
        protected void OnEnable()
        {
            ZoneImageAsset = new ManagedAddressableAsset<Sprite>(m_zoneImageAssetReference);
            ZoneAccent = new ManagedAddressableAsset<Sprite>(m_iconAsset);
        }

        // Game.Runtime 0x06001830: original HLLocalisation string-table lookup.
        public override string GetName() => Hardlight.Localisation.StringTable.GetString(m_title);
        // Game.Runtime 0x06001831: the supplied player body is RET.
        public override void SetData(string sceneName) { }

        // Game.Runtime 0x06001832: ordered foreach, first failure, normal enumeration disposal.
        public bool MeetsAllRequirements()
        {
            if (DebugUnlockLevels.AreAllLevelsUnlocked()) return true;
            foreach (var requirement in m_requirements)
                if (!requirement.RequirementsMet(this)) return false;
            return true;
        }

        // Game.Runtime 0x06001833: debug unlock exempts the actual always-hidden requirement type.
        public bool HasRequirement<T>() where T : RequirementLevelCategoryBase
        {
            if (DebugUnlockLevels.AreAllLevelsUnlocked()) return typeof(T) != typeof(RequirementCategoryAlwaysHidden);
            foreach (var requirement in m_requirements)
                if (requirement is T) return true;
            return false;
        }

        // Game.Runtime 0x06001834: ignore None levels; stop on the first inconsistent non-None reward.
        public CollectableType GetRewardType()
        {
            CollectableType rewardType = CollectableType.None;
            foreach (var level in m_levelManagerRef.Get().GameLevels.GetLevels(this))
            {
                CollectableType current = level.GetRewardType();
                if (current == CollectableType.None) continue;
                if (rewardType == CollectableType.None || rewardType == current)
                {
                    rewardType = current;
                    continue;
                }
                HLOutput.LogError(string.Concat(new[] {
                    "Levels in zones: ", name, " do not use a consistent reward type. Level ",
                    level.name, " is using ", current.GetString(), ", the rest seem to be ", rewardType.GetString()
                }), null);
                break;
            }
            return rewardType;
        }

        // Game.Runtime 0x06001835: only immediate entries; original GUID equality, including null semantics.
        public bool Contains(GameplayLevelDefinition levelDefinition)
        {
            foreach (var entry in m_gameplayEntries)
                if (entry == levelDefinition) return true;
            return false;
        }

        // Game.Runtime 0x06001836: initializers above precede the genuine GameplayLevelUIEntry base constructor.
        public GameplayLevelCategory() { }
    }
}
