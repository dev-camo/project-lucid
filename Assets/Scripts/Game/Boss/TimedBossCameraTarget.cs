using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0200067a; bodies translated from the 1.10.1 x86-64 binary, 0x583430..0x5835A0.
    // A timed object that the boss's camera looks at for as long as it lives.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class TimedBossCameraTarget : TimedLifetimeObject
    {
        private Boss m_boss;

        // 06002311 (1.10.1 RVA 0x583430): the damageable must be a boss; it takes this object as a camera target before
        // the lifetime starts.
        public override void Initialise(IPlayerDamageable playerDamageable, float lifetime)
        {
            m_boss = playerDamageable as Boss;
            m_boss.AddCameraLookAtTarget(transform);
            base.Initialise(playerDamageable, lifetime);
        }

        // 06002312 (1.10.1 RVA 0x583520)
        protected override void OnDisable()
        {
            base.OnDisable();
            if (m_boss == null)
                return;
            m_boss.RemoveCameraLookAtTarget(transform);
        }

        // 06002313 (1.10.1 RVA 0x5835A0) is the base-only constructor.
        public TimedBossCameraTarget()
        {
        }
    }
}
