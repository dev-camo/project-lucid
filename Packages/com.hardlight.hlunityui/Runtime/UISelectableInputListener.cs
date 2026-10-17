// Original HLUnityUI.Runtime Hardlight.UISelectableInputListener (TypeDef 0x02000082), restored from
// the x86-64 bodies 0x1b59c60..0x1b5a870. It is the UISelectable/UIButton twin of HLSelectableInputListener.
// Nested types are declared first so that ButtonSelected is source member 12 (two nested types, five fields,
// five methods before it), which yields the shipped <>c__DisplayClass12_0; their exact position before
// ButtonSelected is inferred. No shipped prefab, scene or asset references the exported script GUID
// 53c048f87f9851e4716ec54557d28003, so the script has a fresh GUID and no binding rule.
// Attribute order follows the shipped custom-attribute rows.

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Unity.IL2CPP.CompilerServices;
using Hardlight.Enums;
using Hardlight.Utils;

namespace Hardlight
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class UISelectableInputListener : MonoBehaviour
    {
        [Il2CppSetOption(Option.NullChecks, false)]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        [Serializable]
        public class SelectedButtonInput
        {
            public bool m_triggerButton;
            [Tooltip("Will trigger an interactable button")]
            public UIButton m_buttonToTrigger;
            public bool m_triggerEvent;
            [Tooltip("Any extras can be setup here, will trigger on input after the button")]
            public UnityEvent m_eventToTrigger;
            [Tooltip("Add your desired input here, when the button is selected and this input happens, the button will trigger and event will fire.")]
            public GameInput m_inputToListenFor;
            public InputSupplier m_inputSupplierToListenFor;
            [Tooltip("String to be shown in the overlay.")]
            [HashEnum(typeof(Strings))]
            public Strings m_stringID;
            private ISelectedButtonInputTextProvider m_provider;

            // 0x06000412: no provider means no text; m_stringID is not consulted.
            public string Text => m_provider != null ? m_provider.Text : null;

            // 0x06000413: the first provider wins; later calls (including null) are ignored.
            public void SetProvider(ISelectedButtonInputTextProvider provider)
            {
                if (m_provider == null)
                {
                    m_provider = provider;
                }
            }
        }

        public interface ISelectedButtonInputTextProvider
        {
            string Text { get; }
        }

        [Tooltip("The selectable to watch, can be the same as the button to be triggered by needs to be defined separately")]
        [SerializeField]
        private UISelectable m_selectableToWatch;
        [SerializeField]
        [Tooltip("Buttons to trigger, events to fire and inputs to listen to are all defined here")]
        private SelectedButtonInput[] m_buttonInputData;
        private SystemRef<UISelectableController> m_selectableController;
        private List<Action<float>> m_delegates = new List<Action<float>>();
        private InputBridge m_inputBridge;

        // 0x06000408: GetSystemRef(null, true) and GetSystemSafe(null, true) at 0x1b59ccc/0x1b59cf2, then
        // Object != null; get_InputBridge (0x060000bf) is inlined as the +0x40 load at 0x1b59d29.
        private void Awake()
        {
            m_selectableController = ProcessManager.GetSystemRef<UISelectableController>(null);
            UIRuntimeConfiguration configuration = ProcessManager.GetSystemSafe<UIRuntimeConfiguration>(null);
            if (configuration != null)
            {
                m_inputBridge = configuration.InputBridge;
            }
        }

        private void OnEnable()
        {
            Setup();
        }

        private void OnDisable()
        {
            ClearUp();
        }

        // 0x0600040b: the shutdown hook is added before the controller reference is checked.
        // IsCurrentSelection (0x060003ea) is inlined at 0x1b59f81..0x1b59fc5: watched is loaded first and
        // its own s_Il2CppMethodInitialized flag (0x33FD2ED, UnityEngine.Object TypeInfo) is checked.
        private void Setup()
        {
            InputBridge.OnShutdown += ClearUp;
            if (!m_selectableController.IsNull())
            {
                m_selectableController.Get().OnSelect += ButtonSelected;
                m_selectableController.Get().OnDeselect += ButtonDeselected;
                if (m_selectableController.Get().IsCurrentSelection(m_selectableToWatch))
                {
                    ButtonSelected(m_selectableToWatch);
                }
            }
        }

        // 0x0600040c: deselects (unregistering any input listeners) before the controller check.
        private void ClearUp()
        {
            InputBridge.OnShutdown -= ClearUp;
            ButtonDeselected(m_selectableToWatch);
            if (!m_selectableController.IsNull())
            {
                m_selectableController.Get().OnSelect -= ButtonSelected;
                m_selectableController.Get().OnDeselect -= ButtonDeselected;
            }
        }

        // 0x0600040d: appends one callback per entry without clearing m_delegates first,
        // then registers the callback at the same index.
        private void ButtonSelected(UISelectable selected)
        {
            if (m_selectableToWatch != selected)
            {
                return;
            }

            for (int i = 0; i < m_buttonInputData.Length; i++)
            {
                int gameInputIndex = i;
                m_delegates.Add(value => ProcessTriggers(value, m_buttonInputData[gameInputIndex].m_inputSupplierToListenFor));
                if (m_inputBridge != null)
                {
                    m_inputBridge.RegisterInputListenerOnDown(m_buttonInputData[i].m_inputSupplierToListenFor, m_delegates[i]);
                }
            }
        }

        // 0x0600040e: value is unused; every entry with the same supplier fires, button before event.
        private void ProcessTriggers(float value, InputSupplier inputSupplier)
        {
            for (int i = 0; i < m_buttonInputData.Length; i++)
            {
                if (inputSupplier != m_buttonInputData[i].m_inputSupplierToListenFor)
                {
                    continue;
                }

                if (m_buttonInputData[i].m_triggerButton)
                {
                    m_buttonInputData[i].m_buttonToTrigger.Press();
                }

                if (m_buttonInputData[i].m_triggerEvent)
                {
                    m_buttonInputData[i].m_eventToTrigger.Invoke();
                }
            }
        }

        // 0x0600040f: unregisters only the first m_buttonInputData.Length callbacks, then clears them all.
        private void ButtonDeselected(UISelectable selected)
        {
            if (m_selectableToWatch != selected)
            {
                return;
            }

            for (int i = 0; i < m_buttonInputData.Length && i < m_delegates.Count; i++)
            {
                if (m_inputBridge != null)
                {
                    m_inputBridge.UnregisterInputListener(m_buttonInputData[i].m_inputSupplierToListenFor, m_delegates[i]);
                }
            }

            m_delegates.Clear();
        }

        public SelectedButtonInput[] GetButtonInputData()
        {
            return m_buttonInputData;
        }
    }
}
