using Hardlight.UI.Binding;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000979; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class OnScreenTargetingButton : MonoBehaviour
    {
        [SerializeField] private CharacterAbilityTypeGroup m_validAbilityTypeGroup;
        public readonly Bindable<bool> IsTargetingActive = new Bindable<bool>();

        // 060036a9 (1.10.1 RVA 0x692160)
        private void OnEnable()
        {
            IsTargetingActive.Value = false;
        }

        // 060036aa (RVA 0x692180): targeting is active when the target is valid and any of its homing abilities
        // is in the valid ability group. Every ability is checked; there is no early exit.
        public void SetTargetingActive(HomingTargetUpdateMessage homingTargetUpdateData)
        {
            bool targetingActive = false;
            if (homingTargetUpdateData.IsValid)
            {
                foreach (IHomingAbility ability in homingTargetUpdateData.TargetValue.Abilities)
                {
                    ActorAbilityType abilityType = ability.TargetingDefinition.AbilityType;
                    targetingActive |= m_validAbilityTypeGroup.Abilities.Exists(definition => definition.Type == abilityType);
                }
            }
            IsTargetingActive.Value = targetingActive;
        }

        // 060036ab (RVA 0x692440)
        public OnScreenTargetingButton()
        {
        }
    }
}
