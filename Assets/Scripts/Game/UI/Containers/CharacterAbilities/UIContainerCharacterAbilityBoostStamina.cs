using Hardlight;
using Hardlight.UI.Binding;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000863; bodies 0x612030..0x612280 (1.10.1 x86-64).
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public sealed class UIContainerCharacterAbilityBoostStamina : UIContainerCharacterAbilityStamina
    {
        [Tooltip("Period of time the recharge UI remains active once stamina has been recharged from a collectable.")]
        [Min(0f)]
        [SerializeField]
        private float m_rechargeUIRemainTime = 0.1f;
        public readonly Bindable<bool> CanTrigger = new Bindable<bool>();
        public readonly Bindable<bool> RechargeActive = new Bindable<bool>();
        private float m_rechargeUITimer;

        // 06003093: the bar shows while the boost is active, was recharged from a collectable or
        // stamina is below full, unless the character's storage disables the stamina UI. Disabling the
        // UI or an immediate stamina reset marks the remain period as elapsed, so a bar that is not
        // shown hides at once.
        private void Update()
        {
            if (!TryGetCharacter(out Character character))
                return;
            float deltaTime = Time.deltaTime;
            CharacterBoostStamina stamina = character.BoostStamina;
            float current = stamina.Stamina;
            bool rechargedFromCollectable = stamina.StaminaRechargeFromCollectable;
            bool shouldShow = stamina.Active || rechargedFromCollectable || 1f > current;
            if (character.Storage.TryGetValue(ActorFSMKeys.StaminaUIDisabled, out bool uiDisabled) && uiDisabled)
            {
                ResetTimer();
                shouldShow = false;
            }
            if (character.BoostStamina.ImmediateResetApplied)
                ResetTimer();
            UpdatePositionAndBinders(shouldShow, 1f >= current, current, deltaTime);
            m_rechargeUITimer = rechargedFromCollectable
                ? m_rechargeUIRemainTime
                : Mathf.Max(m_rechargeUITimer - deltaTime, 0f);
            RechargeActive.Value = m_rechargeUITimer > 0f;
            CanTrigger.Value = character.BoostStamina.ValidStamina();
        }

        // 06003094
        public UIContainerCharacterAbilityBoostStamina() { }
    }
}
