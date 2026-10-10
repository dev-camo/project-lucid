using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public abstract class CharacterAbility_BurstAttack<T> : CharacterAbility<T>, IAbilityTriggerable, IAbilityTrigger
        where T : CharacterAbilityDefinition_BurstAttack
    {
        public bool IsTriggered { get; private set; }
        private bool m_inCooldown;
        private float m_cooldownTimerSeconds;
        private bool m_waitForTriggerRelease;
        private bool m_wasTriggered;
        private readonly SystemRef<MessageManager> m_messageManagerRef = ProcessManager.GetSystemRef<MessageManager>();

        protected override void DoOnEnter()
        {
            base.DoOnEnter();
            UpdateAttachPointPosition();
            m_wasTriggered = false;
        }

        protected override void DoOnUpdate(CharacterBrain brain, float deltaTime)
        {
            base.DoOnUpdate(brain, deltaTime);
            UpdateTriggerMessage();
            if (m_inCooldown)
            {
                m_cooldownTimerSeconds -= deltaTime;
                m_inCooldown = m_cooldownTimerSeconds > 0f;
                return;
            }
            if (Definition.DeactivateOnRelease && TryGetStamina(out CharacterStamina stamina) && stamina.ActivationQueued)
            {
                if (m_character.HasControllerMovement || m_character.AnySwapInProgress())
                    return;
                if (m_waitForTriggerRelease)
                {
                    if (!brain.BurstAttack)
                    {
                        IsTriggered = true;
                        m_waitForTriggerRelease = false;
                    }
                }
                else
                {
                    IsTriggered = false;
                    m_waitForTriggerRelease = brain.BurstAttack;
                }
                return;
            }
            IsTriggered = brain.BurstAttack && CanTrigger(true);
            m_waitForTriggerRelease = false;
            if (!Definition.DeactivateOnRelease)
                brain.EndAction(GameAction.CharacterBurstAttack);
        }

        private bool CanTrigger(bool checkStamina)
        {
            if (m_character.HasControllerMovement || m_character.AnySwapInProgress())
                return false;
            return !checkStamina || EnoughStamina();
        }

        private bool TryGetStamina(out CharacterStamina stamina)
        {
            if (Definition.CostsBoostStamina)
                stamina = m_character.BoostStamina;
            else if (Definition.CostsChaosStamina)
                stamina = m_character.ChaosStamina;
            else
                stamina = null;
            return stamina != null;
        }

        // Original 06001081 deliberately falls through after an invalid boost check.
        private bool EnoughStamina()
        {
            if (!Definition.CostsBoostStamina || m_character.BoostStamina.ValidStamina())
                return true;
            if (Definition.CostsChaosStamina)
                return m_character.ChaosStamina.ValidStamina();
            return true;
        }

        private bool EnoughBoostStamina()
        {
            return !Definition.CostsBoostStamina || m_character.BoostStamina.ValidStamina();
        }

        public bool EnoughChaosStamina()
        {
            return !Definition.CostsChaosStamina || m_character.ChaosStamina.ValidStamina();
        }

        protected override void DoOnLeave()
        {
            base.DoOnLeave();
            IsTriggered = false;
            UpdateTriggerMessage(true);
        }

        public override void OnEnableUI()
        {
            base.OnEnableUI();
            m_wasTriggered = false;
        }

        public void DoTrigger()
        {
            if (m_character.DyingIsInProgress())
                return;
            IsTriggered = true;
            m_inCooldown = false;
        }

        public void ClearTrigger() { IsTriggered = false; }

        public void TryStartCooldown()
        {
            if (Definition.CooldownSeconds == 0f)
                return;
            m_inCooldown = true;
            m_cooldownTimerSeconds = Definition.CooldownSeconds;
            IsTriggered = false;
        }

        public Vector3 GetTargetPosition()
        {
            return m_character.transform.TransformPoint(Definition.Offset);
        }

        private void UpdateAttachPointPosition()
        {
            if (m_character.ActorAttachPoints.TryGetNodeTransform(ActorAttachPointType.BurstAttack, out Transform node))
                node.localPosition = Definition.Offset;
        }

        protected void UpdateTriggerMessage(bool forceUpdate = false, bool overrideActive = false)
        {
            bool canTrigger = Enabled && !m_character.HasControllerMovement;
            bool isActive = IsTriggered || overrideActive;
            if (canTrigger)
            {
                if (forceUpdate || !m_wasTriggered)
                    PublishMessage(true, Definition.CostsChaosStamina, isActive);
                m_wasTriggered = true;
            }
            else
            {
                if (forceUpdate || m_wasTriggered)
                    PublishMessage(false, false, isActive);
                m_wasTriggered = false;
            }
        }

        private void PublishMessage(bool canTrigger, bool isChaosControl, bool isActive)
        {
            BurstAttackUpdateMessage message = new BurstAttackUpdateMessage
            {
                CanTrigger = canTrigger, IsChaosControl = isChaosControl, IsActive = isActive
            };
            MessageManager manager = m_messageManagerRef.GetSafe();
            if (manager == null)
                return;
            Component component = m_character;
            manager.ComponentMessagesWithCompletion.PublishMessage(in component, in message, () => { }, -1);
        }

        public override bool OnDrawGizmosSelected()
        {
            bool result = base.OnDrawGizmosSelected();
            if (result)
                Gizmos.DrawWireSphere(GetTargetPosition(), Definition.RangeRadius);
            return result;
        }

        protected CharacterAbility_BurstAttack() { }
    }
}
