using System.Collections.Generic;
using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [CreateAssetMenu(fileName = "LevelSetupDefinition", menuName = "HardlightProject/DefinitionData/Definitions/LevelSetupDefinition")]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class LevelSetupDefinition : ScriptableObject
    {
        [SerializeField] private LevelSetupTypes m_levelSetupType;
        [SerializeField] private Character m_characterPrefab;
        [SerializeField] private CinemachineCameraManager m_cinemachineCameraManager;
        [SerializeField] private List<HLAudioSourceRegisterComponent> m_audioSourcePrefabs;
        [SerializeField] private PrefabPoolManager m_prefabPoolManager;
        [SerializeField] private InstancedObjectPoolManager m_instancedObjectPoolManager;
        [SerializeField] private TerrainEffectType m_defaultTerrainEffectTypeGround;
        [SerializeField] private TerrainEffectType m_defaultTerrainEffectTypeClimbingWall;

        public LevelSetupTypes LevelSetupType { get { return m_levelSetupType; } }
        public Character CharacterPrefab { get { return m_characterPrefab; } }
        public CinemachineCameraManager CinemachineCameraManager { get { return m_cinemachineCameraManager; } }
        public List<HLAudioSourceRegisterComponent> AudioSourcePrefabs { get { return m_audioSourcePrefabs; } }
        public PrefabPoolManager PrefabPoolManager { get { return m_prefabPoolManager; } }
        public InstancedObjectPoolManager InstancedObjectPoolManager { get { return m_instancedObjectPoolManager; } }
        public TerrainEffectType DefaultTerrainEffectTypeGround { get { return m_defaultTerrainEffectTypeGround; } }
        public TerrainEffectType DefaultTerrainEffectTypeClimbingWall { get { return m_defaultTerrainEffectTypeClimbingWall; } }

        public LevelSetupDefinition() { }
    }
}
