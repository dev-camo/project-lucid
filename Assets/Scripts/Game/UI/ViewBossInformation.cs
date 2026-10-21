using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000603; bodies translated from the 1.10.1 x86-64 binary, 0x563D40..0x563DC0.
    // The actor information overlay for the boss found in the scene.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class ViewBossInformation : ViewActorInformation
    {
        // 06002054 (1.10.1 RVA 0x563D40)
        protected override void OnEnable()
        {
            m_actor = FindObjectOfType<Boss>();
            base.OnEnable();
        }

        // 06002055 (1.10.1 RVA 0x563DC0)
        public ViewBossInformation()
        {
        }
    }
}
