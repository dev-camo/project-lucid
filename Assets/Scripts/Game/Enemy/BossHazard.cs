using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Complete original Game.Runtime 0200062f, two methods.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class BossHazard : Hazard
    {
        [SerializeField] private Boss m_damageableBoss;

        // Original 0600210e: base registration precedes both stores. Even null
        // marks the lookup complete; RegisterPlayerDamageableInterface would
        // change that behavior and is not the original call here.
        protected override void Start()
        {
            base.Start();
            m_playerDamageable = m_damageableBoss;
            m_playerDamageableSet = true;
        }

        // Original 0600210f: implicit constructor calls only Hazard base.
    }
}
