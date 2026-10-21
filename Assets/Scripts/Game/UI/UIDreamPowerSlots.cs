using Hardlight;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 0x02000872; bodies translated from the 1.10.1 x86-64 binary.
    // Keeps the dream power slot widgets of the character select screen in step with the store manager's
    // equipped powers for one archetype.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public sealed class UIDreamPowerSlots
    {
        private readonly CharacterArchetype m_characterArchetype;
        private readonly Slot[] m_slots;
        private readonly DreamPowerStoreManager m_dreamPowerStoreManager;

        // 0x060030da: every slot starts unequipped.
        public UIDreamPowerSlots(CharacterArchetype characterArchetype, UIWidgetButtonDreamPowerSlot[] slots)
        {
            m_characterArchetype = characterArchetype;
            m_slots = new Slot[slots.Length];
            for (int i = 0; i < slots.Length; i++)
                m_slots[i] = new Slot(slots[i], false);
            m_dreamPowerStoreManager = ProcessManager.GetSystem<DreamPowerStoreManager>();
        }

        // 0x060030db
        public void Toggle(int slotIndex, DreamPowerDefinition definition)
        {
            Slot slot = m_slots[slotIndex];
            if (slot.IsEquipped)
            {
                slot.Unequip();
                m_dreamPowerStoreManager.Unequip(m_characterArchetype, slotIndex);
            }
            else
            {
                Equip(slotIndex, definition);
            }
        }

        // 0x060030dc: a power can only sit in one slot, so any other slot holding it is cleared.
        public void Equip(int slotIndex, DreamPowerDefinition definition)
        {
            if (!m_dreamPowerStoreManager.TryEquip(m_characterArchetype, slotIndex, definition))
                return;
            Slot slot = m_slots[slotIndex];
            slot.Widget.SetInteractable(true);
            slot.Equip(definition);
            for (int i = 0; i < m_slots.Length; i++)
            {
                if (i == slotIndex)
                    continue;
                if (m_slots[i].Definition == definition)
                {
                    m_slots[i].Unequip();
                    m_dreamPowerStoreManager.Unequip(m_characterArchetype, i);
                }
            }
        }

        // 0x060030dd
        private void Unequip(int slotIndex)
        {
            m_slots[slotIndex].Unequip();
            m_dreamPowerStoreManager.Unequip(m_characterArchetype, slotIndex);
        }

        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        [Il2CppSetOption(Option.NullChecks, false)]
        private class Slot
        {
            public bool IsEquipped { get; private set; }
            public UIWidgetButtonDreamPowerSlot Widget { get; }
            public DreamPowerDefinition Definition { get; private set; }

            // 0x060030e3
            public Slot(UIWidgetButtonDreamPowerSlot widget, bool isEquipped)
            {
                Widget = widget;
                IsEquipped = isEquipped;
                Definition = null;
            }

            // 0x060030e4: the widget is told before the definition is stored.
            public void Equip(DreamPowerDefinition definition)
            {
                IsEquipped = true;
                Widget.Equip(definition);
                Definition = definition;
            }

            // 0x060030e5
            public void Unequip()
            {
                IsEquipped = false;
                Widget.Unequip();
                Definition = null;
            }
        }
    }
}
