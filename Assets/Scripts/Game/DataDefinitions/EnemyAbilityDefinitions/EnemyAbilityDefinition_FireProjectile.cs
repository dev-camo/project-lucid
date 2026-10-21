using System;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 0200053a; bodies translated from the 1.10.1 x86-64 binary.
    [CreateAssetMenu(fileName = "EnemyAbility_FireProjectile", menuName = "HardlightProject/DefinitionData/AbilityDef/EnemyAbility_FireProjectile", order = 1)]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class EnemyAbilityDefinition_FireProjectile : EnemyAbilityDefinition_Movement
    {
        [SerializeField]
        private ProjectileType m_projectileType = ProjectileType.BuzzBomberProjectile;
        [Min(0)]
        [SerializeField]
        private float m_windUpTime = 1f;
        [SerializeField]
        [Min(0)]
        private float m_windDownTime = 1f;
        [SerializeField]
        private ActorAttachPointType m_shootFromOnEnemy = ActorAttachPointType.Body;
        [SerializeField]
        private ActorAttachPointType m_aimForOnTarget = ActorAttachPointType.Body;
        [Tooltip("Will only select a target closer than this. This is NOT how far the projectile can travel, which is determined in the ProjectileDefinition.")]
        [SerializeField]
        private float m_range = 20f;
        [SerializeField]
        [Tooltip("Maximum metres deviation from the actual target position. E.g. 0 = completely accurate, 5 = may target random location up to 5 metres radius from target")]
        private float m_accuracyMetres;
        [SerializeField]
        [Range(-90, 90)]
        private float m_verticalAngleMaxDegrees = 60f;
        [SerializeField]
        [Range(-90, 90)]
        private float m_verticalAngleMinDegrees = -60f;

        // 06001c76 (1.10.1 RVA 0x54BDA0)
        public override Type ScriptType => typeof(EnemyAbility_FireProjectile);
        // 06001c77..06001c82 (1.10.1 RVA 0x54BE00..0x54BEB0): the getters and auto-property accessors below.
        public ProjectileType ProjectileType => m_projectileType;
        public float WindUpTime => m_windUpTime;
        public float WindDownTime => m_windDownTime;
        public ActorAttachPointType ShootFromOnEnemy => m_shootFromOnEnemy;
        public ActorAttachPointType AimForOnTarget => m_aimForOnTarget;
        public float AccuracyMetres => m_accuracyMetres;
        public float RangeSquared { get; private set; }
        public float VerticalAngleMaxSine { get; private set; }
        public float VerticalAngleMinSine { get; private set; }

        // 06001c83 (1.10.1 RVA 0x54BEC0). The native body multiplies both vertical
        // angles by Deg2Rad in one packed operation, then calls sinf for each lane.
        public override void CalculateCachedValues()
        {
            RangeSquared = m_range * m_range;
            VerticalAngleMaxSine = Mathf.Sin(m_verticalAngleMaxDegrees * Mathf.Deg2Rad);
            VerticalAngleMinSine = Mathf.Sin(m_verticalAngleMinDegrees * Mathf.Deg2Rad);
        }

        // 06001c84 (1.10.1 RVA 0x54BF20)
        protected override void OnValidate() => base.OnValidate();

        // 06001c85 (1.10.1 RVA 0x54BF30)
        public EnemyAbilityDefinition_FireProjectile() { }
    }
}
