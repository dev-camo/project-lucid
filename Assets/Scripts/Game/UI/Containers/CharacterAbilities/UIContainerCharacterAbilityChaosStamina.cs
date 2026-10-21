using Hardlight;
using Hardlight.UI.Binding;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000865; bodies 0x612940..0x612C30 (1.10.1 x86-64).
    // Same behaviour as the boost bar, reading the character's chaos stamina instead.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public sealed class UIContainerCharacterAbilityChaosStamina : UIContainerCharacterAbilityStamina
    {
        [Tooltip("Period of time the recharge UI remains active once stamina has been recharged from a collectable.")]
        [Min(0f)]
        [SerializeField]
        private float m_rechargeUIRemainTime = 0.1f;
        public readonly Bindable<bool> CanTrigger = new Bindable<bool>();
        public readonly Bindable<bool> RechargeActive = new Bindable<bool>();
        private float m_rechargeUITimer;

        // 06003099
        private void Update()
        {
            if (!TryGetCharacter(out Character character))
                return;
            float deltaTime = Time.deltaTime;
            CharacterChaosStamina stamina = character.ChaosStamina;
            float current = stamina.Stamina;
            bool rechargedFromCollectable = stamina.StaminaRechargeFromCollectable;
            bool shouldShow = stamina.Active || rechargedFromCollectable || 1f > current;
            if (character.Storage.TryGetValue(ActorFSMKeys.StaminaUIDisabled, out bool uiDisabled) && uiDisabled)
            {
                ResetTimer();
                shouldShow = false;
            }
            if (character.ChaosStamina.ImmediateResetApplied)
                ResetTimer();
            UpdatePositionAndBinders(shouldShow, 1f >= current, current, deltaTime);
            m_rechargeUITimer = rechargedFromCollectable
                ? m_rechargeUIRemainTime
                : Mathf.Max(m_rechargeUITimer - deltaTime, 0f);
            RechargeActive.Value = m_rechargeUITimer > 0f;
            CanTrigger.Value = character.ChaosStamina.ValidStamina();
        }

        // 0600309a
        public UIContainerCharacterAbilityChaosStamina() { }
    }
}
