using System.Collections;
using System.Collections.Generic;
using Hardlight;
using Hardlight.Utils;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public abstract class CharacterAbility_BurstAttackChaos<TAbilityDef> : CharacterAbility_BurstAttack<TAbilityDef>, IAbilityBurstAttackChaos, IAbilityBurstAttack<CharacterAbilityDefinition_BurstAttackChaos>, IAbility
        where TAbilityDef : CharacterAbilityDefinition_BurstAttackChaos
    {
        private readonly SystemRef<TimeManager> m_timeManagerRef = ProcessManager.GetSystemRef<TimeManager>();
        private Coroutine m_coroutine;
        private GraphStorageKey m_fsmVarName;
        private StackableDataHandle m_timeSettingHandle;
        private int m_timeIndex;
        private readonly List<FullscreenShaderManager.ParametersHandle> m_fullScreenEffectHandleInstances = new List<FullscreenShaderManager.ParametersHandle>();

        private int TimeDilationCount => Definition.TimeDilations.Count;
        public CharacterAbilityDefinition_BurstAttackChaos BurstAttackDefinition => Definition;

        protected override void DoOnEnter()
        {
            base.DoOnEnter();
            CharacterStamina chaosStamina = m_character.ChaosStamina;
            chaosStamina.UpdateFromAbility(Definition.AbilityType, Definition.ChaosStaminaCostDefinition, false);
        }

        public void TriggerTimeDilation(int startIndex = 0)
        {
            if (m_coroutine != null)
            {
                OnTimeDilationEnd();
                CoroutineUtils.StopUtilCoroutine(ref m_coroutine);
                m_character.ActivateAudioFilter(false, false);
            }
            m_coroutine = CoroutineUtils.RunCoroutine(ScaleTime(startIndex));
        }

        private IEnumerator ScaleTime(int startIndex)
        {
            m_character.ActivateAudioFilter(true, false);
            for (m_timeIndex = startIndex; m_timeIndex < TimeDilationCount; m_timeIndex++)
                yield return UpdateTimeDilation();
            m_coroutine = null;
            UpdateTriggerMessage(true, false);
            m_character.ActivateAudioFilter(false, false);
        }

        private void TryUpdateChaosStaminaFromAbility()
        {
            CharacterStamina chaosStamina = m_character.ChaosStamina;
            if (chaosStamina.ActivationQueued && chaosStamina.Stamina > 0f)
                chaosStamina.UpdateFromAbility(Definition.AbilityType, Definition.ChaosStaminaCostDefinition, true);
        }

        // Original 060010ad yields once before retaining stamina and active-state values.
        // Neither original iterator has disposal cleanup or a surrounding finally.
        private IEnumerator UpdateTimeDilation()
        {
            TimeManager timeManager = m_timeManagerRef.Get();
            WaitForFixedUpdate waitForFixedUpdate = new WaitForFixedUpdate();
            float deltaTime = Time.fixedDeltaTime;
            TimeSetting timeSetting = OnTimeDilationStart();
            CharacterAbilityDefinition_BurstAttackChaos.ChaosTimeDilation timeDilation = Definition.TimeDilations[m_timeIndex];
            float elapsedTime = 0f;
            float endTime = timeDilation.TimeScaleAnimation.TotalTime();
            EvaluateTimeDilation(timeManager, timeSetting, timeDilation.TimeScaleAnimation, elapsedTime);
            yield return waitForFixedUpdate;
            TryUpdateChaosStaminaFromAbility();
            CharacterStamina chaosStamina = m_character.ChaosStamina;
            bool staminaRecharging = chaosStamina.Stamina < 1f;
            bool isActive = m_timeIndex <= timeDilation.RechargeIndex + 1;
            bool interrupt;
            while (elapsedTime <= endTime)
            {
                UpdateTriggerMessage(true, isActive);
                EvaluateTimeDilation(timeManager, timeSetting, timeDilation.TimeScaleAnimation, elapsedTime);
                yield return waitForFixedUpdate;
                while (m_character.Storage.TryGetValue(ActorFSMKeys.ChaosControlActive, out bool chaosControlActive) && chaosControlActive)
                {
                    chaosStamina.UpdateFromAbility(0, null, false);
                    yield return waitForFixedUpdate;
                }
                TryUpdateChaosStaminaFromAbility();
                if (timeDilation.RechargeIndex == m_timeIndex)
                    elapsedTime = endTime - chaosStamina.RemainingTime();
                elapsedTime += deltaTime;
                if (ShouldTriggerTimeDilationExit())
                {
                    interrupt = true;
                    goto Finish;
                }
                if (staminaRecharging && Mathf.Approximately(chaosStamina.Stamina, 1f))
                    break;
            }
            interrupt = false;
        Finish:
            if (timeDilation.RechargeIndex == m_timeIndex)
                ResetStaminaRecharge();
            if (interrupt)
                TryStopTimeDilation(true);
            else
                OnTimeDilationEnd();
        }

        private void EvaluateTimeDilation(TimeManager timeManager, TimeSetting timeSetting, AnimationCurve timeScaleAnimation, float elapsedTime)
        {
            float timeScale = timeScaleAnimation.Evaluate(elapsedTime);
            foreach (TimeCategory category in Definition.TimeCategories)
                timeSetting.SetCategory(category, timeScale);
            timeManager.UpdateTimeSetting(m_timeSettingHandle, timeSetting);
        }

        public void OnStaminaRecharge(bool triggerAbility)
        {
            if (triggerAbility && m_timeSettingHandle == null)
                TriggerTimeDilation();
            else if (m_timeSettingHandle != null)
            {
                int startIndex = 0;
                for (; startIndex < TimeDilationCount; startIndex++)
                    if (startIndex == Definition.TimeDilations[startIndex].RechargeIndex)
                        break;
                TriggerTimeDilation(startIndex);
            }
        }

        private TimeSetting OnTimeDilationStart()
        {
            CharacterAbilityDefinition_BurstAttackChaos.ChaosTimeDilation timeDilation = Definition.TimeDilations[m_timeIndex];
            m_fsmVarName = new GraphStorageKey(timeDilation.OnTriggerFSMName, 0, 0);
            m_character.Storage.SetValue(m_fsmVarName, true);
            m_character.TriggerAnimationEnter(timeDilation.AnimationDefinition, m_fullScreenEffectHandleInstances);
            TimeSetting timeSetting = TimeSetting.GetDefault(1f);
            m_timeSettingHandle = m_timeManagerRef.Get().ApplyTimeSetting(timeSetting);
            return timeSetting;
        }

        private void OnTimeDilationEnd()
        {
            CharacterAbilityDefinition_BurstAttackChaos.ChaosTimeDilation timeDilation = Definition.TimeDilations[m_timeIndex];
            m_character.Storage.RemoveValue<bool>(m_fsmVarName);
            m_fsmVarName = default;
            m_character.TriggerAnimationLeave(timeDilation.AnimationDefinition, m_fullScreenEffectHandleInstances);
            m_timeManagerRef.Get().RemoveTimeSetting(m_timeSettingHandle);
            m_timeSettingHandle = null;
        }

        public override void Close()
        {
            base.Close();
            if (m_coroutine != null)
            {
                OnTimeDilationEnd();
                CoroutineUtils.StopUtilCoroutine(ref m_coroutine);
                m_character.ActivateAudioFilter(false, false);
            }
        }

        private bool ShouldTriggerTimeDilationExit()
        {
            return m_character == null || m_character.DyingIsInProgress() || m_character.TeleportIsInProgress() || m_character.IsExitingLevel();
        }

        public bool TryStopTimeDilation(bool interrupt)
        {
            if (m_coroutine == null)
                return true;
            if (!interrupt && m_timeIndex < TimeDilationCount - 1)
            {
                CharacterAbilityDefinition_BurstAttackChaos.ChaosTimeDilation timeDilation = Definition.TimeDilations[m_timeIndex];
                if (timeDilation.RechargeIndex >= 0)
                {
                    if (timeDilation.RechargeIndex == m_timeIndex)
                        m_character.TriggerChaosDeactivation();
                    return false;
                }
            }
            OnTimeDilationEnd();
            CoroutineUtils.StopUtilCoroutine(ref m_coroutine);
            m_character.ActivateAudioFilter(false, false);
            return true;
        }

        public bool IsTimeDilationActive() => m_timeSettingHandle != null;
        protected CharacterAbility_BurstAttackChaos() { }
    }
}
