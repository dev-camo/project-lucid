using System;
using System.Collections.Generic;
using System.Text;
using Hardlight;
using Hardlight.Analytics;
using Hardlight.Enums;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class PromptManager : MonoBehaviour, ISystem, ISaveGameListener
    {
        [SerializeField] private TimeSettings_SDT m_promptPauseTimeSetting;
        [SerializeField] private UIVisibilityGroupOverrider m_uiVisibilityGroupOverriderStandard;
        [SerializeField] private UIVisibilityGroupOverrider m_uiVisibilityGroupOverriderTimeStopped;
        [Tooltip("Disable inputs using this while any prompt is shown.")]
        [SerializeField] private GameInputDisabler m_gameInputDisabler;
        public const int PromptChoiceNone = -1;
        public const string DebugMenuRoot = "Prompt Manager";
        public Action OnInUsePromptSlotTypesUpdated;
        private readonly HashSet<string> m_seenPromptGuids = new HashSet<string>();
        private StackableDataHandle m_timeScalingHandle;
        private SaveDataGame m_saveDataGame;
        private SaveManager m_saveManager;
        private readonly Dictionary<PromptSlotType, PromptSlot> m_promptSlots =
            new Dictionary<PromptSlotType, PromptSlot>(HardlightEnumComparers.PromptSlotTypeComparer);
        private readonly List<ActivePromptInfo> m_activePrompts = new List<ActivePromptInfo>();
        private bool m_timeStopped;
        private readonly SystemRef<TimeManager> m_timeManagerRef = ProcessManager.GetSystemRef<TimeManager>(null, true);
        private HashSet<PromptSlotType> m_inUsePromptSlotTypes =
            new HashSet<PromptSlotType>(HardlightEnumComparers.PromptSlotTypeComparer);

        // Original 060036fb retains the discarded StringBuilder construction.
        private void OnEnable()
        {
            ProcessManager.RegisterSystem(this, null, false, false);
            ProcessManager.GetSystemRef<SaveManager>(null, true).InvokeOnValid(RegisterSaveManager);
            new StringBuilder();
        }

        // Original 060036fc writes the field before AddListener can call back.
        private void RegisterSaveManager(SaveManager saveManager)
        {
            m_saveManager = saveManager;
            saveManager.AddListener(this, true);
        }

        private void OnDisable()
        {
            ProcessManager.UnregisterSystem(this);
            HideAllActivePrompts();
            if (m_saveManager != null)
                m_saveManager.RemoveListener(this);
            m_saveManager = null;
        }

        public void HideAllActivePrompts()
        {
            while (m_activePrompts.Count > 0)
                EndActivePrompt(m_activePrompts[0], PromptChoiceNone);
        }

        // Original 060036ff and natural 0600371f/20 retain the real captures.
        public bool TryShowPrompt(PromptTrigger trigger, Action<int> onComplete)
        {
            if (!ShouldShowPrompt(trigger))
                return false;

            var promptDefinition = trigger.PromptDefinition;
            if (!m_promptSlots.TryGetValue(promptDefinition.PromptSlot, out var promptSlot) || promptSlot == null)
                return false;

            var existingPrompt = m_activePrompts.Find(prompt => prompt.Slot.SlotType == promptSlot.SlotType);
            if (existingPrompt != null)
                EndActivePrompt(existingPrompt, PromptChoiceNone);

            var promptContainer = Instantiate(promptDefinition.PromptPrefab, promptSlot.transform);
            var stopsTime = PromptStopsTime(promptDefinition);
            var activePromptInfo = new ActivePromptInfo(promptContainer, trigger, onComplete,
                promptDefinition, promptSlot, stopsTime);
            m_activePrompts.Add(activePromptInfo);

            promptContainer.Setup(new UIContainerPromptParameters
            {
                Text = promptDefinition.Text,
                Image = promptDefinition.Image,
                Target = trigger.Target,
                TapToContinue = promptDefinition.TapToContinue,
                MinimumContinueSeconds = promptDefinition.MinimumContinueSeconds,
                AutomaticContinueSeconds = promptDefinition.AutomaticContinueSeconds,
                CallbackOnComplete = (promptView, choiceIndex) =>
                {
                    if (promptView == activePromptInfo.Container)
                        EndActivePrompt(activePromptInfo, choiceIndex);
                    else
                        Destroy(promptView);
                },
                PromptChoices = promptDefinition.Choices,
                DisplayGameInputs = promptDefinition.DisplayGameInputs,
                DisplayGameInputsSeparator = promptDefinition.DisplayGameInputsSeparator,
                SlotDirection = promptSlot.SlotDirection
            });
            UpdateAfterActivePromptsChanged();
            return true;
        }

        private void UpdateAfterActivePromptsChanged()
        {
            UpdateTimeScaling();
            UpdateVisibilityOverrides();
            UpdateDisabledGameInputs();
            UpdateInUsePromptSlotTypes();
        }

        private void UpdateTimeScaling()
        {
            if (!m_timeManagerRef.IsValid())
                return;
            var promptStoppingTime = m_activePrompts.Find(prompt => prompt.StopsTime);
            if (promptStoppingTime != null)
            {
                if (m_timeScalingHandle == null)
                    m_timeScalingHandle = m_timeManagerRef.GetSafe().ApplyTimeSetting_SDT(m_promptPauseTimeSetting);
            }
            else if (m_timeScalingHandle != null)
            {
                m_timeManagerRef.GetSafe().RemoveTimeSetting(m_timeScalingHandle);
                m_timeScalingHandle = null;
            }
        }

        private void UpdateVisibilityOverrides()
        {
            var promptStoppingTime = m_activePrompts.Find(prompt => prompt.StopsTime);
            if (promptStoppingTime != null)
            {
                m_uiVisibilityGroupOverriderStandard.DeactivateOverrides();
                m_uiVisibilityGroupOverriderTimeStopped.ActivateOverrides();
            }
            else
            {
                // The count precedes Deactivate and is retained across its callbacks.
                var activePromptCount = m_activePrompts.Count;
                m_uiVisibilityGroupOverriderTimeStopped.DeactivateOverrides();
                if (activePromptCount > 0)
                    m_uiVisibilityGroupOverriderStandard.ActivateOverrides();
                else
                    m_uiVisibilityGroupOverriderStandard.DeactivateOverrides();
            }
        }

        private void UpdateDisabledGameInputs()
        {
            if (m_activePrompts.Count > 0)
                m_gameInputDisabler.Action_DisableGameInputs();
            else
                m_gameInputDisabler.Action_ReenableGameInputs();
        }

        private bool PromptStopsTime(PromptDefinition definition)
        {
            return PromptActionAllowed(definition.PauseOccurence, definition.GetGUID());
        }

        // Original 06003705 removes before every subsequent callback/fault.
        private void EndActivePrompt(ActivePromptInfo prompt, int promptChoiceIndex, bool isAcknowledged = false)
        {
            if (!m_activePrompts.Remove(prompt))
                return;
            TryRecordPromptSeen(prompt, isAcknowledged);
            Destroy(prompt.Container.gameObject);
            var definition = prompt.Definition;
            if (definition.SendFTUEAnalytics)
            {
                var userInput = promptChoiceIndex == PromptChoiceNone
                    ? string.Empty
                    : definition.Choices[promptChoiceIndex].Text.GetString();
                AnalyticsEventCollector.FTUEEvent(definition.name, definition.FTUEStepID, userInput);
            }
            UpdateAfterActivePromptsChanged();
            prompt.OnComplete?.Invoke(promptChoiceIndex);
        }

        private void TryRecordPromptSeen(ActivePromptInfo activePromptInfo, bool isAcknowledged)
        {
            var definition = activePromptInfo.Definition;
            var displayOccurence = definition.DisplayOccurence;
            if (displayOccurence == PromptDefinition.PromptOccurenceType.UntilAcknowledged && !isAcknowledged)
                return;
            var pauseOccurence = definition.PauseOccurence;
            if (displayOccurence == PromptDefinition.PromptOccurenceType.FirstTimeLevel ||
                displayOccurence == PromptDefinition.PromptOccurenceType.FirstTimeSave ||
                displayOccurence == PromptDefinition.PromptOccurenceType.UntilAcknowledged ||
                pauseOccurence == PromptDefinition.PromptOccurenceType.FirstTimeLevel ||
                pauseOccurence == PromptDefinition.PromptOccurenceType.FirstTimeSave)
            {
                var guid = definition.GetGUID();
                if (m_seenPromptGuids.Add(guid) &&
                    ((displayOccurence == PromptDefinition.PromptOccurenceType.UntilAcknowledged && isAcknowledged) ||
                     displayOccurence == PromptDefinition.PromptOccurenceType.FirstTimeSave ||
                     pauseOccurence == PromptDefinition.PromptOccurenceType.FirstTimeSave) &&
                    m_saveDataGame != null)
                {
                    m_saveDataGame.AddSeenPromptGuid(guid);
                    m_saveManager.RequestSave();
                }
            }
        }

        private bool ShouldShowPrompt(PromptTrigger trigger)
        {
            var promptDefinition = trigger.PromptDefinition;
            if (!PromptActionAllowed(promptDefinition.DisplayOccurence, promptDefinition.GetGUID()))
                return false;
            if (promptDefinition.IsExclusive)
            {
                if (m_activePrompts.Count > 0)
                    return false;
            }
            else if (m_activePrompts.Find(prompt => prompt.Definition.IsExclusive) != null)
            {
                return false;
            }
            var existingPrompt = m_activePrompts.Find(prompt => prompt.Slot.SlotType == promptDefinition.PromptSlot);
            if (existingPrompt != null)
                return promptDefinition.Priority > existingPrompt.Definition.Priority;
            return true;
        }

        private bool PromptActionAllowed(PromptDefinition.PromptOccurenceType occurenceType, string guid)
        {
            switch (occurenceType)
            {
                case PromptDefinition.PromptOccurenceType.Never:
                    return false;
                case PromptDefinition.PromptOccurenceType.FirstTimeLevel:
                case PromptDefinition.PromptOccurenceType.FirstTimeSave:
                case PromptDefinition.PromptOccurenceType.UntilAcknowledged:
                    return !m_seenPromptGuids.Contains(guid);
                default:
                    return true;
            }
        }

        public void TryHidePrompt(PromptTrigger promptTrigger, bool isAcknowledged = false)
        {
            var activePrompt = m_activePrompts.Find(prompt => prompt.Trigger == promptTrigger);
            if (activePrompt != null)
                EndActivePrompt(activePrompt, PromptChoiceNone, isAcknowledged);
        }

        public void OnSaveGameOpen(SaveDataGame saveDataGame)
        {
            m_saveDataGame = saveDataGame;
            m_seenPromptGuids.Clear();
            m_seenPromptGuids.AddRange(saveDataGame.SeenPromptGuids);
        }

        public void OnSaveGameClose(SaveDataGame saveDataGame)
        {
            if (m_saveDataGame == saveDataGame)
            {
                m_seenPromptGuids.Clear();
                m_saveDataGame = null;
            }
        }

        private void ResetSeenPrompts()
        {
            m_seenPromptGuids.Clear();
            if (m_saveDataGame != null)
                m_saveDataGame.ClearAllSeenPromptGuids();
            if (m_saveManager != null)
                m_saveManager.RequestSave();
        }

        public void RegisterPromptSlot(PromptSlot promptSlot)
        {
            var slotType = promptSlot.SlotType;
            if (m_promptSlots.ContainsKey(slotType))
                m_promptSlots.Remove(slotType);
            m_promptSlots.Add(slotType, promptSlot);
        }

        public void UnregisterPromptSlot(PromptSlot promptSlot)
        {
            var slotType = promptSlot.SlotType;
            if (!m_promptSlots.TryGetValue(slotType, out var registeredSlot) || registeredSlot != promptSlot)
                return;
            m_promptSlots.Remove(slotType);
            var promptsToEnd = m_activePrompts.FindAll(new Predicate<ActivePromptInfo>(prompt => prompt.Slot == promptSlot));
            foreach (var activePrompt in promptsToEnd)
                EndActivePrompt(activePrompt, PromptChoiceNone);
        }

        private void UpdateInUsePromptSlotTypes()
        {
            m_inUsePromptSlotTypes.Clear();
            foreach (var prompt in m_activePrompts)
                m_inUsePromptSlotTypes.Add(prompt.Slot.SlotType);
            OnInUsePromptSlotTypesUpdated?.Invoke();
        }

        public bool IsSlotTypeInUse(PromptSlotType promptSlotType)
        {
            return m_inUsePromptSlotTypes.Contains(promptSlotType);
        }

        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        [Il2CppSetOption(Option.NullChecks, false)]
        private class ActivePromptInfo
        {
            public UIContainerPrompt Container { get; }
            public PromptTrigger Trigger { get; }
            public Action<int> OnComplete { get; }
            public PromptDefinition Definition { get; }
            public PromptSlot Slot { get; }
            public bool StopsTime { get; }

            public ActivePromptInfo(UIContainerPrompt container, PromptTrigger trigger, Action<int> onComplete,
                PromptDefinition definition, PromptSlot slot, bool stopsTime)
            {
                Container = container;
                Trigger = trigger;
                OnComplete = onComplete;
                Definition = definition;
                Slot = slot;
                StopsTime = stopsTime;
            }
        }
    }
}
