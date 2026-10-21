using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000606; bodies translated from the 1.10.1 x86-64 binary, 0x564500..0x5645C0.
    // The actor information overlay for the enemy assigned in the scene.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class ViewEnemyInformation : ViewActorInformation
    {
        [SerializeField]
        private Enemy m_enemy;

        // 0600205c (1.10.1 RVA 0x564500)
        protected override void OnEnable()
        {
            m_actor = m_enemy;
            base.OnEnable();
        }

        // 0600205d (1.10.1 RVA 0x564530): replaces the base update. Only the state text is published, from the builder
        // as it stands: nothing in the retail build fills it first and it is not cleared.
        protected override void Update()
        {
            if (m_actor == null)
                return;
            StateInfo.Value = m_stringInfo.ToString();
        }

        // 0600205e (1.10.1 RVA 0x5645C0)
        public ViewEnemyInformation()
        {
        }
    }
}
