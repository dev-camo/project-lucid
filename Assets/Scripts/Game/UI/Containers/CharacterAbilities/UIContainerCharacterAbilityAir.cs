using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000862; bodies 0x611D30..0x611FF0 (1.10.1 x86-64).
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public sealed class UIContainerCharacterAbilityAir : UIContainerCharacterAbilityStamina<CharacterAbility_Air>
    {
        // 06003091: the bar shows while the air ability is active or stamina is below full, and the
        // stamina value is only written while the ability is active.
        private void Update()
        {
            if (!TryGetCharacter(out _))
                return;
            float staminaRemaining = Ability.GetStaminaRemaining();
            bool airActive = Ability.AirActive;
            UpdatePositionAndBinders(airActive || 1f > staminaRemaining, airActive, staminaRemaining, Time.deltaTime);
        }

        // 06003092
        public UIContainerCharacterAbilityAir() { }
    }
}
