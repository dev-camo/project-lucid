using Hardlight.Utils;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original authored projectile parameters; the genuine Projectile provider
    //is still required. No controller or substitute projectile is introduced.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [CreateAssetMenu(fileName = "ProjectileDefinition", menuName = "HardlightProject/DefinitionData/Definitions/ProjectileDefinition")]
    public class ProjectileDefinition : ScriptableObject
    {
        [SerializeField] private ProjectileType m_projectileType;
        [SerializeField] private Projectile m_projectilePrefab;
        [SerializeField, Min(0f)] private float m_velocity;
        [SerializeField, Min(0f)] private float m_maxLifespanSeconds;
        [SerializeField] private LayerMask m_collisionLayerMask;
        [SerializeField, HashEnum(typeof(HazardType))] private HazardType m_hazardType;

        public ProjectileType ProjectileType => m_projectileType;
        public Projectile Projectile => m_projectilePrefab;
        public float Velocity => m_velocity;
        public float MaxLifespanSeconds => m_maxLifespanSeconds;
        public LayerMask CollisionLayerMask => m_collisionLayerMask;
        public HazardType HazardType => m_hazardType;

        //06001ea6: base-only constructor; no nonzero defaults are inferred.
        public ProjectileDefinition() { }
    }
}
