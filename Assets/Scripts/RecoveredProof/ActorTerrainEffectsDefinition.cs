using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [CreateAssetMenu(fileName = "ActorTerrainEffectsDefinition", menuName = "HardlightProject/DefinitionData/Definitions/ActorTerrainEffectsDefinition")]
    public class ActorTerrainEffectsDefinition : ScriptableObject, ISerializationCallbackReceiver
    {
        [HideInInspector]
        public string Name;
        [SerializeField]
        private TerrainEffectType m_terrainEffectType;
        [SerializeField]
        private ActorAnimationDefinition m_actorAnimationDefinition;
        [SerializeField]
        private ActorAnimationDefinition m_impactAnimationDefinition;
        [SerializeField]
        [Tooltip("Minimum impact speed against surface normal required for impact animation to trigger")]
        private float m_minimumImpactSpeed;

        // Game.Runtime.dll:0x060019a2; ARM64 0x781e9c.
        public TerrainEffectType TerrainEffectType => m_terrainEffectType;
        // Game.Runtime.dll:0x060019a3; ARM64 0x781ea4.
        public ActorAnimationDefinition ActorAnimationDefinition => m_actorAnimationDefinition;
        // Game.Runtime.dll:0x060019a4; ARM64 0x781eac.
        public ActorAnimationDefinition ImpactAnimationDefinition => m_impactAnimationDefinition;
        // Game.Runtime.dll:0x060019a5/0x060019a6; ARM64 0x781eb4/0x781ebc.
        public float MinimumImpactSpeedSqr { get; private set; }

        // Game.Runtime.dll:0x060019a7; ARM64 0x781ec4. Serialization updates the
        // authored name through the original mutable enum string registry.
        public void OnBeforeSerialize() => Name = m_terrainEffectType.GetString();
        // Game.Runtime.dll:0x060019a8; ARM64 0x781f3c. The squared-speed cache is
        // left as it stands by this original callback.
        public void OnAfterDeserialize() => Name = m_terrainEffectType.GetString();

        // Game.Runtime.dll:0x060019a9; ARM64 0x781fb4. Native Single multiplication.
        private void UpdateCachedValues() => MinimumImpactSpeedSqr = m_minimumImpactSpeed * m_minimumImpactSpeed;
        // Game.Runtime.dll:0x060019aa; ARM64 0x781fc4, same inlined square/store.
        private void OnValidate() => UpdateCachedValues();
        // Game.Runtime.dll:0x060019ab; ARM64 0x781fd4, same inlined square/store.
        private void Awake() => UpdateCachedValues();
        // Game.Runtime.dll:0x060019ac; ARM64 0x781fe4. ScriptableObject base only.
        public ActorTerrainEffectsDefinition() { }
    }
}
