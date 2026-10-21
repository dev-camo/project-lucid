using Hardlight;
using Hardlight.UI.Binding;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000868; bodies 0x611E20, 0x612270, 0x612330 and 0x612E80 (1.10.1 x86-64).
    // Shared stamina bar behaviour: the bar sits at half the character's bounds height and stays
    // visible for m_staminaUIRemainTime seconds after the stamina was last shown.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public abstract class UIContainerCharacterAbilityStamina : UIContainerCharacterAbility
    {
        [Min(0f)]
        [Tooltip("Period of time the UI remains active once stamina has replenished back to full.")]
        [SerializeField]
        private float m_staminaUIRemainTime = 1f;
        public readonly Bindable<bool> ShouldShow = new Bindable<bool>();
        public readonly Bindable<float> StaminaValue = new Bindable<float>();
        public readonly Bindable<float> TimeoutPercentageRemaining = new Bindable<float>();
        private float m_staminaUIRemainTimer;

        // 060030a1
        public override void Setup(IUIContainerParameters parameters)
        {
            base.Setup(parameters);
            ShouldShow.Value = false;
        }

        // 060030a2
        protected void ResetTimer()
        {
            m_staminaUIRemainTimer = m_staminaUIRemainTime;
        }

        // 060030a3: nothing happens until the parameters name a live character. While shown the
        // timer restarts; once hidden the remaining time drains the bar from full, and a timer at or
        // past the remain time hides it. The drain branch needs a positive remain time; with a non-positive
        // one the bar hides as soon as it is not shown.
        protected void UpdatePositionAndBinders(bool shouldShow, bool shouldUpdate, float currentStamina, float deltaTime)
        {
            if (!TryGetCharacter(out Character character))
                return;
            OrientateToCamera(transform);
            Vector3 localPosition = new Vector3(0f, character.Bounds.size.y * 0.5f, 0f);
            transform.localPosition = localPosition;
            TimeoutPercentageRemaining.Value = 1f;
            m_staminaUIRemainTimer += deltaTime;
            if (shouldShow)
            {
                if (shouldUpdate)
                    StaminaValue.Value = currentStamina;
                m_staminaUIRemainTimer = 0f;
                ShouldShow.Value = true;
            }
            else if (m_staminaUIRemainTimer >= m_staminaUIRemainTime)
            {
                ShouldShow.Value = false;
            }
            else if (m_staminaUIRemainTime > 0f)
            {
                StaminaValue.Value = 1f;
                TimeoutPercentageRemaining.Value = 1f - m_staminaUIRemainTimer / m_staminaUIRemainTime;
            }
        }

        // 060030a4: field initialisers precede the base constructor, which holds the camera reference.
        protected UIContainerCharacterAbilityStamina() { }
    }
}
