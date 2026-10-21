using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000866; bodies 0x612C30..0x612E80 (1.10.1 x86-64).
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public sealed class UIContainerCharacterAbilitySpinDash : UIContainerCharacterAbilityStamina<CharacterAbility_MovementGroundSpinDashCharge>
    {
        // The original never reads m_arrowUILocation; it is serialized data only.
        [SerializeField]
        [Tooltip("Transform to set position and rotation for the arrow world space UI.")]
        private Transform m_arrowUILocation;
        [Tooltip("Transform to set scale for the arrow world space UI.")]
        [SerializeField]
        private Transform m_arrowUIScaler;
        [Tooltip("Arrow world space UI scale for no progress to lerp from.")]
        [SerializeField]
        private Vector3 m_scaleMin = Vector3.one;
        [SerializeField]
        [Tooltip("Arrow world space UI scale for full progress to lerp to.")]
        private Vector3 m_scaleMax = Vector3.one;

        // 0600309b: once the charge has been held for the definition's UI delay the arrow is shown
        // and scaled by launch progress; below the delay it hides again (without touching the
        // bindable when it is already hidden).
        private void Update()
        {
            if (!TryGetCharacter(out Character character))
                return;
            float chargeTime = character.SpinDashRoll.ChargeTime;
            if (chargeTime >= Ability.Definition.UIDelayTime)
            {
                ShouldShow.Value = true;
                float progress = Ability.GetLaunchProgress(chargeTime);
                m_arrowUIScaler.localScale = Vector3.Lerp(m_scaleMin, m_scaleMax, progress);
            }
            else if (ShouldShow.Value)
            {
                ShouldShow.Value = false;
            }
        }

        // 0600309c
        public UIContainerCharacterAbilitySpinDash() { }
    }
}
