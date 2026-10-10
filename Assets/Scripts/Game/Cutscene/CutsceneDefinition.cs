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
    // Original Game.Runtime 02000432 and all four original nested types.
    // Theatre validity is authored metadata; it does not validate images or titles.
    [CreateAssetMenu(fileName = "CutsceneDefinition", menuName = "HardlightProject/DefinitionData/Definitions/CutsceneDefinition")]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class CutsceneDefinition : ScriptableObjectWithGuid
    {
        [Serializable]
        public struct CutsceneRequirements
        {
            public GameplayLevelDefinition LevelDefinition;
            public CutsceneTriggerType TriggerType;
            public RequirementGameplayLevelBase[] LevelRequirements;
        }

        public enum CutsceneRepetitionRule
        {
            PlayOnceEver = 0,
            PlayOncePerLevel = 1,
            PlayOncePerSession = 2,
            PlayEveryTime = 3,
        }

        public enum MusicBehaviour
        {
            DoNotRestoreMusic = 0,
            RestoreCurrentMusicOnComplete = 1,
            KeepCurrentMusic = 2,
        }

        [Serializable]
        public struct CharacterPrefabMetadata
        {
            public bool AllComicCutscene;
            public bool AllFull3DCutscene;
        }

        [HashEnum((Type)null)]
        [SerializeField()]
        private HLCutsceneIdentifier m_identifier;

        [SerializeField()]
        private SerializableDictionary<CharacterId,AssetReferenceT<GameObject>> m_prefabsForCharacter = new SerializableDictionary<CharacterId, AssetReferenceT<GameObject>>(HardlightEnumComparers.CharacterIdComparer);

        [SerializeField()]
        private CharacterPrefabMetadata m_metadataFromCharacterPrefabs;

        [HashEnum((Type)null)]
        [SerializeField()]
        private CharacterId m_characterToSwitchTo;

        [SerializeField()]
        private CharacterId[] m_characterChoice;

        [SerializeField()]
        private CutsceneDefinition m_triggerNextCutscene;

        [SerializeField()]
        private CutsceneRequirements m_triggerRequirements;

        [HashEnum((Type)null)]
        [SerializeField()]
        private CutsceneRepetitionRule m_repetitionRule;

        [SerializeField()]
        private bool m_canBeSkipped;

        [SerializeField()]
        [Tooltip("Only fill this in if this cutscene takes place outside of a playable level (e.g. the reverie or dream machine chamber)")]
        private string m_nonGameplaySceneName;

        [SerializeField()]
        private bool m_playEveryTimeInEditor;

        [SerializeField()]
        private List<TimeCategory> m_timeCategoriesToPause;

        [SerializeField()]
        private UIContainerIdentifier m_skipUIContainer;

        [SerializeField()]
        private bool m_islandCullingTracksCamera;

        [SerializeField()]
        private bool m_characterInvulnerable = true;

        [HashEnum((Type)null)]
        [SerializeField()]
        private HLAudioClipIdentifier m_backgroundMusic;

        [SerializeField()]
        private MusicBehaviour m_musicBehaviour;

        [SerializeField()]
        private bool m_duckMusicVolume;

        [ShowIf("m_duckMusicVolume", (string)null)]
        [Tooltip("Multiplied by the current music volume set by the player.")]
        [SerializeField()]
        [Range(0f, 1f)]
        private float m_duckMusicVolumeFractionReduction = 1f;

        [ShowIf("m_duckMusicVolume", (string)null)]
        [SerializeField()]
        private float m_musicVolumeTransitionTime = 0.5f;

        [SerializeField()]
        private bool m_requiresMaxResolutionScaling;

        [SerializeField()]
        private bool m_disableGameplayCameraForGood;

        [Tooltip("Prevents enabling the UI camera on complete. Not needed for implicit trigger types such as OnLevelEntered or BossPhaseChanged")]
        [SerializeField()]
        private bool m_leadsIntoGameplay;

        [Tooltip("Alternative level definition to use for the mission context. This affects which level the mission progress is saved under in the save data.")]
        [SerializeField()]
        [ShowIf("m_leadsIntoGameplay", (string)null)]
        private GameplayLevelDefinition m_levelDefinitionForMissionContext;

        [Tooltip("Set to > 0 to skip some of the start of the scene.")]
        [SerializeField()]
        private float m_startTime;

        [Tooltip("Will play a transition when the cutscene stops, waiting for its midpoint before cleaning up the cutscene.")]
        [SerializeField()]
        private bool m_playTransitionOnStopped;

        [SerializeField()]
        [Tooltip("Will display as the image of the cutscene in the theatre.")]
        [Header("Theatre")]
        private AssetReferenceT<Texture> m_theatreImage;

        [SerializeField()]
        [Tooltip("Will display as the name of the cutscene in the theatre.")]
        [HashEnum((Type)null)]
        private Strings m_theatreName = Strings.NONE;

        // Original 0x060018b0: direct original field 0x04000e3e.
        public HLCutsceneIdentifier Identifier => m_identifier;
        // Original 0x060018b1: direct original field 0x04000e3f.
        public SerializableDictionary<CharacterId,AssetReferenceT<GameObject>> PrefabsForCharacter => m_prefabsForCharacter;
        // Original 0x060018b2: direct original field 0x04000e40.
        public CharacterPrefabMetadata MetadataFromCharacterPrefabs => m_metadataFromCharacterPrefabs;
        // Original 0x060018b3: direct original field 0x04000e41.
        public CharacterId CharacterToSwitchTo => m_characterToSwitchTo;
        // Original 0x060018b4: direct original field 0x04000e42.
        public CharacterId[] CharacterChoice => m_characterChoice;
        // Original 0x060018b5: direct original field 0x04000e43.
        public CutsceneDefinition TriggerNextCutscene => m_triggerNextCutscene;
        // Original 0x060018b6: direct original field 0x04000e44.
        public CutsceneRequirements TriggerRequirements => m_triggerRequirements;
        // Original 0x060018b7: direct original field 0x04000e45.
        public CutsceneRepetitionRule RepetitionRule => m_repetitionRule;
        // Original 0x060018b8: direct original field 0x04000e46.
        public bool CanBeSkipped => m_canBeSkipped;
        // Original 0x060018b9: direct original field 0x04000e47.
        public string NonGameplaySceneName => m_nonGameplaySceneName;
        // Original 0x060018ba: direct original field 0x04000e48.
        public bool PlayEveryTimeInEditor => m_playEveryTimeInEditor;
        // Original 0x060018bb: direct original field 0x04000e49.
        public List<TimeCategory> TimeCategoriesToPause => m_timeCategoriesToPause;
        // Original 0x060018bc: direct original field 0x04000e4a.
        public UIContainerIdentifier SkipUIContainer => m_skipUIContainer;
        // Original 0x060018bd: direct original field 0x04000e4d.
        public HLAudioClipIdentifier BackgroundMusic => m_backgroundMusic;
        // Original 0x060018be: direct original field 0x04000e4e.
        public MusicBehaviour BackgroundMusicBehaviour => m_musicBehaviour;
        // Original 0x060018bf: direct original field 0x04000e4b.
        public bool IslandCullingTracksCamera => m_islandCullingTracksCamera;
        // Original 0x060018c0: direct original field 0x04000e4c.
        public bool CharacterInvulnerable => m_characterInvulnerable;
        // Original 0x060018c1: direct original field 0x04000e52.
        public bool RequiresMaxResolutionScaling => m_requiresMaxResolutionScaling;
        // Original 0x060018c2: direct original field 0x04000e53.
        public bool DisableGameplayCameraForGood => m_disableGameplayCameraForGood;
        // Original 0x060018c3: direct original field 0x04000e54.
        public bool ExplicitlyLeadsIntoGameplay => m_leadsIntoGameplay;
        // Original 0x060018c4: direct original field 0x04000e4f.
        public bool DuckMusicVolume => m_duckMusicVolume;
        // Original 0x060018c5: direct original field 0x04000e50.
        public float DuckMusicVolumeFractionReduction => m_duckMusicVolumeFractionReduction;
        // Original 0x060018c6: direct original field 0x04000e51.
        public float MusicVolumeTransitionTime => m_musicVolumeTransitionTime;
        // Original 0x060018c7: direct original field 0x04000e56.
        public float StartTime => m_startTime;
        // Original 0x060018c8: direct original field 0x04000e58.
        public AssetReferenceT<Texture> TheatreImage => m_theatreImage;
        // Original 0x060018c9: direct original field 0x04000e59.
        public Strings TheatreName => m_theatreName;
        // Original 0x060018ca: direct original field 0x04000e57.
        public bool PlayTransitionOnStopped => m_playTransitionOnStopped;
        // Original 0x060018cb: direct original field 0x04000e55.
        public GameplayLevelDefinition LevelDefinitionForMissionContext => m_levelDefinitionForMissionContext;

        // 060018cc / ARM 778640 and x86 79f440: only AllComicCutscene.
        public bool IsValidInTheatre() => m_metadataFromCharacterPrefabs.AllComicCutscene;

        // 060018cd / ARM 77864c: original comparer dictionary, invulnerability,
        // music reduction 1 and transition 0.5, then Strings.NONE, then GUID base.
        public CutsceneDefinition() { }
    }
}
