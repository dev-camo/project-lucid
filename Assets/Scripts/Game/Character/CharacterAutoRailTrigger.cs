using Hardlight;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime type 02000735. Holds the AutoRail modifier override on the character
    // while it is inside the volume.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class CharacterAutoRailTrigger : CharacterTrigger
    {
        private StackableDataHandle m_overrideHandle;

        // 060028a7; an override left by an earlier entry is released before the new one is added.
        protected override void DoTriggerEnter(Character character)
        {
            base.DoTriggerEnter(character);
            if (m_overrideHandle != null)
                RemoveOverride(character);
            m_overrideHandle = character.AddModifierOverride((int)GameplayModifierType.AutoRail, true);
        }

        // 060028a8
        protected override void DoTriggerExit(Character character)
        {
            base.DoTriggerExit(character);
            if (m_overrideHandle != null)
                RemoveOverride(character);
        }

        // 060028a9
        private void RemoveOverride(Character character)
        {
            character.RemoveModifierOverrides(m_overrideHandle);
            m_overrideHandle = null;
        }

        // 060028aa
        public CharacterAutoRailTrigger() { }
    }
}
