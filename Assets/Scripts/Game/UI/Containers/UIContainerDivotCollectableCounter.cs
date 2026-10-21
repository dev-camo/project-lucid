using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x020008b3; bodies translated from the 1.10.1 x86-64 binary.
    // A collectable counter that also fills one slot widget per collected item.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public sealed class UIContainerDivotCollectableCounter : UIContainerCollectableCounter
    {
        [SerializeField] private UIWidgetSDTSlot[] m_slots;

        // 0x0600323f (0x62F700): the slots fill only while the collected count is below the slot count; the base update
        // (and with it the counter display) is skipped once the count reaches it.
        public override void OnCollectableStateUpdate(CollectableState collectableState, CollectableChangeMetadata metadata)
        {
            if (collectableState.Collected >= m_slots.Length)
                return;
            for (int i = 0; i < m_slots.Length && i < collectableState.Collected; i++)
                m_slots[i].Activate();
            base.OnCollectableStateUpdate(collectableState, metadata);
        }

        // 0x06003240 (0x62F7A0)
        public UIContainerDivotCollectableCounter() { }
    }
}
