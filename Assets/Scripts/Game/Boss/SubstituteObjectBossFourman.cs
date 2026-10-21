using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000446; bodies translated from the 1.10.1 x86-64 binary, 0x7A5820..0x7A5940.
    // A cutscene stand-in for the Fourman boss: it replaces the boss's visual proxy rather than the boss itself, when
    // the boss and its visual proxy exist.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class SubstituteObjectBossFourman : SubstituteObject
    {
        [SerializeField] protected BossDefinition m_bossDefinition;

        // 06001946 (1.10.1 RVA 0x7A5820)
        protected override void OnEnable()
        {
            Boss boss = ProcessManager.GetSystem<BossManager>(null, true).GetBossByDefinition(m_bossDefinition);
            if (boss != null && boss.VisualProxy != null)
            {
                m_replacedObject = boss.VisualProxy.gameObject;
                m_realAnimationRoot = boss.VisualProxy.AnimationRoot;
            }
            base.OnEnable();
        }

        // 06001947 (1.10.1 RVA 0x7A5940) is the base-only constructor.
        public SubstituteObjectBossFourman()
        {
        }
    }
}
