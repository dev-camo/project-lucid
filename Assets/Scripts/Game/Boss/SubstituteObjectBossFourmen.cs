using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000447; bodies translated from the 1.10.1 x86-64 binary, 0x7A5950..0x7A59E0.
    // A cutscene stand-in for one of the Fourmen: the replaced boss is the one at the authored index among the bosses
    // with the definition.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class SubstituteObjectBossFourmen : SubstituteObjectBoss
    {
        [SerializeField] private int m_fourmenIndex;

        // 06001948 (1.10.1 RVA 0x7A5950)
        protected override void OnEnable()
        {
            m_replacedBoss = ProcessManager.GetSystem<BossManager>(null, true)
                .GetBossByDefinition(m_bossDefinition, m_fourmenIndex);
            base.OnEnable();
        }

        // 06001949 (1.10.1 RVA 0x7A59E0) is the base-only constructor.
        public SubstituteObjectBossFourmen()
        {
        }
    }
}
