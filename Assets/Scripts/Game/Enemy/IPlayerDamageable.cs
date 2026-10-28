using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0200066a. These are the entire two-method abstract
    // contract; both original native pointers are zero.
    public interface IPlayerDamageable
    {
        // Original 060022a9, slot 0.
        void ReceiveHit(Collider damagedCollider);

        // Original 060022aa, slot 1.
        void RegisterHitPlayer(DamageAction damageAction, HazardType hazardType);
    }
}
