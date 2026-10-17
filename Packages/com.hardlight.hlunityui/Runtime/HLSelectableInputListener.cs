// Original HLUnityUI.Runtime Hardlight.HLSelectableInputListener complete family reconstruction.
// C# spelling and local names are inferred; source/native fault equivalence and current emission remain held.
// All thirty concrete ARM64/x86_64 spans were personally read; the text-provider interface has two genuine zero definition pointers.
// Setup subscribes shutdown before SystemRef.IsNull. ClearUp deselects before checking/removing controller subscriptions; no idempotence/rollback added.
// ButtonSelected appends per-index closures without clearing the delegate list. Callback supplier and ProcessTriggers entries are reread, including after Press/event reentry.
// ProcessTriggers deliberately ignores value; matching suppliers dispatch Press before UnityEvent. No synthetic threshold, string lookup or event allocation.
// SelectedButtonInput Serializable comes from raw TypeDef bit0x2000; Text returns provider.Text or null and SetProvider only writes an empty provider.
// Hardlight.HLSelectableInputListener+SelectedButtonInput 0x0600047b get_Text | arm64 0x1b67e70..0x1b67f30; x86_64 0x1b5f510..0x1b5f5c0
// Hardlight.HLSelectableInputListener+SelectedButtonInput 0x0600047c SetProvider | arm64 0x1b67f30..0x1b67f44; x86_64 0x1b5f5c0..0x1b5f5e0
// Hardlight.HLSelectableInputListener+SelectedButtonInput 0x0600047d .ctor | arm64 0x1b67f44..0x1b67f4c; x86_64 0x1b5f5e0..0x1b5f5f0
// Hardlight.HLSelectableInputListener+ISelectedButtonInputTextProvider 0x0600047e get_Text | abstract declaration; zero dual definition cells
// Hardlight.HLSelectableInputListener+<>c__DisplayClass12_0 0x0600047f .ctor | arm64 0x1b67cc0..0x1b67cc8; x86_64 0x1b5f390..0x1b5f3a0
// Hardlight.HLSelectableInputListener+<>c__DisplayClass12_0 0x06000480 <ButtonSelected>b__0 | arm64 0x1b67f4c..0x1b67f98; x86_64 0x1b5f5f0..0x1b5f630
// Hardlight.HLSelectableInputListener 0x06000471 Awake | arm64 0x1b6725c..0x1b67388; x86_64 0x1b5ea20..0x1b5eb10
// Hardlight.HLSelectableInputListener 0x06000472 OnEnable | arm64 0x1b67388..0x1b6738c; x86_64 0x1b5eb10..0x1b5eb20
// Hardlight.HLSelectableInputListener 0x06000473 OnDisable | arm64 0x1b6764c..0x1b67650; x86_64 0x1b5edc0..0x1b5edd0
// Hardlight.HLSelectableInputListener 0x06000474 Setup | arm64 0x1b6738c..0x1b6764c; x86_64 0x1b5eb20..0x1b5edc0
// Hardlight.HLSelectableInputListener 0x06000475 ClearUp | arm64 0x1b67650..0x1b67888; x86_64 0x1b5edd0..0x1b5f000
// Hardlight.HLSelectableInputListener 0x06000476 ButtonSelected | arm64 0x1b67888..0x1b67af4; x86_64 0x1b5f000..0x1b5f220
// Hardlight.HLSelectableInputListener 0x06000477 ProcessTriggers | arm64 0x1b67cc8..0x1b67ddc; x86_64 0x1b5f3a0..0x1b5f480
// Hardlight.HLSelectableInputListener 0x06000478 ButtonDeselected | arm64 0x1b67af4..0x1b67cc0; x86_64 0x1b5f220..0x1b5f390
// Hardlight.HLSelectableInputListener 0x06000479 GetButtonInputData | arm64 0x1b67ddc..0x1b67de4; x86_64 0x1b5f480..0x1b5f490
// Hardlight.HLSelectableInputListener 0x0600047a .ctor | arm64 0x1b67de4..0x1b67e70; x86_64 0x1b5f490..0x1b5f510

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Unity.IL2CPP.CompilerServices;
using Hardlight.Enums;
using Hardlight.Utils;

namespace Hardlight
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class HLSelectableInputListener : MonoBehaviour
    {
        // Both nested types precede the fields: the shipping closure is
        // <>c__DisplayClass12_0 (0x02000092), so ButtonSelected is member ordinal 12.
        [Serializable]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        [Il2CppSetOption(Option.NullChecks, false)]
        public class SelectedButtonInput
        {
            public bool m_triggerButton;
            [Tooltip("Will trigger an interactable button")]
            public HLButton m_buttonToTrigger;
            public bool m_triggerEvent;
            [Tooltip("Any extras can be setup here, will trigger on input after the button")]
            public UnityEvent m_eventToTrigger;
            [Tooltip("Add your desired input here, when the button is selected and this input happens, the button will trigger and event will fire.")]
            public GameInput m_inputToListenFor;
            public InputSupplier m_inputSupplierToListenFor;
            [HashEnum(typeof(Strings))]
            [Tooltip("String to be shown in the overlay.")]
            public Strings m_stringID;
            private ISelectedButtonInputTextProvider m_provider;

            public string Text { get { return m_provider != null ? m_provider.Text : null; } }
            public void SetProvider(ISelectedButtonInputTextProvider provider)
            {
                if (m_provider == null) m_provider = provider;
            }
            public SelectedButtonInput() { }
        }

        public interface ISelectedButtonInputTextProvider
        {
            string Text { get; }
        }

        [Tooltip("The selectable to watch, can be the same as the button to be triggered by needs to be defined separately")]
        [SerializeField] private HLSelectable m_selectableToWatch;
        [Tooltip("Buttons to trigger, events to fire and inputs to listen to are all defined here")]
        [SerializeField] private SelectedButtonInput[] m_buttonInputData;
        private SystemRef<HLSelectableController> m_hlSelectableController;
        private List<Action<float>> m_delegates = new List<Action<float>>();
        private InputBridge m_inputBridge;

        private void Awake()
        {
            m_hlSelectableController = ProcessManager.GetSystemRef<HLSelectableController>();
            UIRuntimeConfiguration configuration = ProcessManager.GetSystemSafe<UIRuntimeConfiguration>();
            if (configuration != null) m_inputBridge = configuration.InputBridge;
        }

        private void OnEnable() { Setup(); }
        private void OnDisable() { ClearUp(); }

        private void Setup()
        {
            InputBridge.OnShutdown += ClearUp;
            if (!m_hlSelectableController.IsNull())
            {
                m_hlSelectableController.Get().OnSelect += ButtonSelected;
                m_hlSelectableController.Get().OnDeselect += ButtonDeselected;
                if (m_hlSelectableController.Get().IsCurrentSelection(m_selectableToWatch))
                    ButtonSelected(m_selectableToWatch);
            }
        }

        private void ClearUp()
        {
            InputBridge.OnShutdown -= ClearUp;
            ButtonDeselected(m_selectableToWatch);
            if (!m_hlSelectableController.IsNull())
            {
                m_hlSelectableController.Get().OnSelect -= ButtonSelected;
                m_hlSelectableController.Get().OnDeselect -= ButtonDeselected;
            }
        }

        private void ButtonSelected(HLSelectable selected)
        {
            if (m_selectableToWatch != selected) return;
            for (int i = 0; i < m_buttonInputData.Length; ++i)
            {
                int gameInputIndex = i;
                m_delegates.Add(value => ProcessTriggers(value, m_buttonInputData[gameInputIndex].m_inputSupplierToListenFor));
                if (m_inputBridge != null)
                    m_inputBridge.RegisterInputListenerOnDown(m_buttonInputData[i].m_inputSupplierToListenFor, m_delegates[i], -1);
            }
        }

        private void ProcessTriggers(float value, InputSupplier inputSupplier)
        {
            for (int i = 0; i < m_buttonInputData.Length; ++i)
            {
                if (inputSupplier != m_buttonInputData[i].m_inputSupplierToListenFor) continue;
                if (m_buttonInputData[i].m_triggerButton) m_buttonInputData[i].m_buttonToTrigger.Press();
                if (m_buttonInputData[i].m_triggerEvent) m_buttonInputData[i].m_eventToTrigger.Invoke();
            }
        }

        private void ButtonDeselected(HLSelectable selected)
        {
            if (m_selectableToWatch != selected) return;
            for (int i = 0; i < m_buttonInputData.Length && i < m_delegates.Count; ++i)
            {
                if (m_inputBridge != null)
                    m_inputBridge.UnregisterInputListener(m_buttonInputData[i].m_inputSupplierToListenFor, m_delegates[i], -1);
            }
            m_delegates.Clear();
        }

        public SelectedButtonInput[] GetButtonInputData() { return m_buttonInputData; }
        public HLSelectableInputListener() { }
    }
}
