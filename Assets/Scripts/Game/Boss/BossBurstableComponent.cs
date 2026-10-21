using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 020001f3; 0x7008D0..0x700900.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class BossBurstableComponent : MonoBehaviour, IPlayerDamageable
    {
        [SerializeField] private Boss m_boss;
        [SerializeField] private BossHomingTarget m_bossHomingTarget;

        // 06000b5e: the collider is ignored; the boss is hit through the homing target.
        public void ReceiveHit(Collider otherCollider) => m_boss.ReceiveHit(m_bossHomingTarget);

        // 06000b5f is a shipped RET body.
        public void RegisterHitPlayer(DamageAction damageAction, HazardType hazardType) { }
    }
}
