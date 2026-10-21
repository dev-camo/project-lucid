using System;
using System.Collections;
using Hardlight;
using Hardlight.Enums;
using Hardlight.Localisation;
using Hardlight.Utils;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace HardlightProject
{
    // Original Game.Runtime 02000967 and its iterators <ProcessHeldTouch>d__24 (02000969)
    // and <FadeOutPrompt>d__28 (02000968); bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public sealed class HoldToSkipInput : MonoBehaviour
    {
        [SerializeField] private float m_maxTapDuration = 0.3f;
        [SerializeField] private float m_requiredHoldTime = 1f;
        [SerializeField] private UIWidgetButton m_button;
        [SerializeField] private Slider m_progress;
        [SerializeField] private CanvasGroup m_visualPrompt;
        [SerializeField] private float m_promptFadeoutTime;
        [SerializeField, HashEnum(null)] private GameInput m_gameInput;
        [SerializeField] private UnityEvent m_onSkipOnceEvent;
        [SerializeField] private UnityEvent m_onFullSkipEvent;
        [SerializeField] private LocalisedUIString m_labelString;
        private readonly SystemRef<App> m_appRef = ProcessManager.GetSystemRef<App>();
        private readonly SystemRef<DataManager> m_dataManagerRef = ProcessManager.GetSystemRef<DataManager>();
        private float m_inputHeldSeconds;
        private float m_lastHeldInputValue;
        private bool m_holdingInput;
        private TimeManager m_timeManager;
        private Coroutine m_heldTouchCoroutine;
        private Coroutine m_promptFadeCoroutine;
        private CoreGameConfiguration m_coreGameConfiguration;
        private ModifierDelayUntil m_inputModifier;

        // 0x06003619 (0x68AAF0): the bound input and the cursor release input are
        // unsubscribed before subscribing so repeated starts never double up.
        private void Start()
        {
            m_timeManager = ProcessManager.GetSystem<TimeManager>();
            m_coreGameConfiguration = SystemConfiguration.GetConfig<CoreGameConfiguration>();
            ControlMapping.Unsubscribe(m_gameInput, new Action<float>(SupplyHeldInput));
            ControlMapping.Subscribe(m_gameInput, new Action<float>(SupplyHeldInput), InputTrigger.Held);
            ControlMapping.Unsubscribe(m_gameInput, new Action<float>(EndHeldInput));
            ControlMapping.Subscribe(m_gameInput, new Action<float>(EndHeldInput), InputTrigger.Up);
            ControlMapping.Unsubscribe(GameInput.ReleaseCursor, new Action<float>(ForceReleaseCursor));
            ControlMapping.Subscribe(GameInput.ReleaseCursor, new Action<float>(ForceReleaseCursor), InputTrigger.Down);
            m_button.AddPointerDownListener(new UnityAction(OnButtonHoldBegin));
            m_button.AddPointerUpListener(new UnityAction(OnButtonHoldEnd));
            m_inputHeldSeconds = 0f;
            m_progress.value = 0f;
            m_visualPrompt.alpha = 0f;
            GlobalConstantDefinition_Input inputConstants = m_dataManagerRef.Get().GlobalConstantDefinitions[GlobalConstantType.Input] as GlobalConstantDefinition_Input;
            m_inputModifier = inputConstants != null ? inputConstants.DelayUntilModifier : null;
        }

        // 0x0600361a (0x68AFF0)
        public void SetLabelString(Strings theatreSkipString)
        {
            m_labelString.Identifier = theatreSkipString;
        }

        // 0x0600361b (0x68B000): touch holds are polled by a coroutine.
        private void OnButtonHoldBegin()
        {
            m_holdingInput = true;
            m_heldTouchCoroutine = CoroutineUtils.RunCoroutine(ProcessHeldTouch());
        }

        // 0x0600361c (0x68B100)
        private void OnButtonHoldEnd()
        {
            m_holdingInput = false;
            CoroutineUtils.StopUtilCoroutine(ref m_heldTouchCoroutine);
            EndHeldInput();
        }

        // 0x0600361d, MoveNext 0x0600362d (0x68B7D0). Without the delay-until modifier a
        // held touch counts at full strength.
        private IEnumerator ProcessHeldTouch()
        {
            while (m_holdingInput)
            {
                float inputValue = m_inputModifier == null ? 1f : m_inputModifier.Modify(1f, Time.deltaTime, m_gameInput);
                SupplyHeldInput(inputValue);
                yield return null;
            }
        }

        // 0x0600361e (0x68B220): a zero input only records itself. Otherwise the held time
        // accumulates, the full-skip event fires past the required time (on every
        // update until released) and the progress bar and prompt follow.
        private void SupplyHeldInput(float inputValue = 0f)
        {
            m_lastHeldInputValue = inputValue;
            if (inputValue == 0f)
                return;
            m_inputHeldSeconds += m_timeManager.GetDeltaTime(TimeCategory.System);
            if (m_inputHeldSeconds > m_requiredHoldTime)
                m_onFullSkipEvent?.Invoke();
            m_progress.value = Mathf.Clamp01(m_inputHeldSeconds / m_requiredHoldTime);
            m_visualPrompt.alpha = 1f;
        }

        // 0x0600361f (0x68B130): a release shorter than the tap duration skips once, if
        // tap-to-skip is enabled. The prompt fades only if input was actually held.
        private void EndHeldInput(float inputValue = 0f)
        {
            float lastHeldInputValue = m_lastHeldInputValue;
            m_lastHeldInputValue = 0f;
            if (m_inputModifier != null)
                m_inputModifier.Reset();
            if (m_maxTapDuration > m_inputHeldSeconds && m_coreGameConfiguration.TapToSkipEnabled)
                m_onSkipOnceEvent?.Invoke();
            ResetAndSyncHeldTimer(lastHeldInputValue != 0f);
        }

        // 0x06003620 (0x68AF20)
        private void ResetAndSyncHeldTimer(bool fadePrompt)
        {
            m_inputHeldSeconds = 0f;
            m_progress.value = 0f;
            if (fadePrompt)
            {
                CoroutineUtils.StopUtilCoroutine(ref m_promptFadeCoroutine);
                m_promptFadeCoroutine = CoroutineUtils.RunCoroutine(FadeOutPrompt());
            }
        }

        // 0x06003621, MoveNext 0x06003627 (0x68B670)
        private IEnumerator FadeOutPrompt()
        {
            float fadeTimer = 0f;
            while (fadeTimer < m_promptFadeoutTime)
            {
                fadeTimer += m_timeManager.GetDeltaTime(TimeCategory.System);
                m_visualPrompt.alpha = 1f - fadeTimer / m_promptFadeoutTime;
                yield return null;
            }
            m_visualPrompt.alpha = 0f;
        }

        // 0x06003622 (0x68B370)
        private void ForceReleaseCursor(float inputValue)
        {
            m_appRef.Get().OnForceReleaseCursor(true);
        }

        // 0x06003623 (0x68B3D0): clears the bar and coroutines, drops every subscription
        // and releases the forced cursor if the app is still around.
        private void OnDestroy()
        {
            m_inputHeldSeconds = 0f;
            m_progress.value = 0f;
            CoroutineUtils.StopUtilCoroutine(ref m_promptFadeCoroutine);
            CoroutineUtils.StopUtilCoroutine(ref m_heldTouchCoroutine);
            ControlMapping.Unsubscribe(m_gameInput, new Action<float>(SupplyHeldInput));
            ControlMapping.Unsubscribe(m_gameInput, new Action<float>(EndHeldInput));
            ControlMapping.Unsubscribe(GameInput.ReleaseCursor, new Action<float>(ForceReleaseCursor));
            m_button.ClearPointerDownListeners();
            m_button.ClearPointerUpListeners();
            if (m_appRef.TryGet(out App app))
                app.OnForceReleaseCursor(false);
        }
    }
}
