using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Events;

namespace HardlightProject
{
    // Original Game.Runtime type 02000a95. A damageable that only raises an event when hit.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class PlayerDamageableObject : MonoBehaviour, IPlayerDamageable
    {
        [SerializeField]
        private UnityEvent m_onHitEvent = new UnityEvent();

        // 06003cf6
        public void ReceiveHit(Collider damagedCollider) => m_onHitEvent.Invoke();

        // 06003cf7; the native body is a bare return.
        public void RegisterHitPlayer(DamageAction damageAction, HazardType hazardType) { }

        // 06003cf8
        public PlayerDamageableObject() { }
    }
}
