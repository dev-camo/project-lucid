using System;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [CreateAssetMenu(menuName = "Hardlight/HLInput/Modifier Trigger Input On Criteria")]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class ModifierTriggerInputOnCriteria : InputModifier, IInputButton
    {
        private enum CriteriaRule
        {
            GreaterThan = 0,
            LessThan = 1,
            GreaterThanOrEqual = 2,
            LessThanOrEqual = 3,
            EqualTo = 4,
            NotEqualTo = 5
        }

        [SerializeField] private ControlMapping m_controlMapping;
        [SerializeField] private GameInput m_gameInputToTrigger;
        [SerializeField] private CriteriaRule m_criteriaRule;
        [SerializeField] private float m_criteriaValue;
        [SerializeField] private bool m_heldButton;
        [SerializeField] private int m_joystickIndex;
        [SerializeField] private float m_valueToTriggerWith;
        [SerializeField] private InputType m_inputType = InputType.DefaultController;

        public event ButtonHandler.OnHeld ButtonHandlers; // 0600016a/16b: genuine CAS accessor pair.
        public event ButtonHandler.OnDown ButtonDownHandlers; // 0600016c/16d
        public event ButtonHandler.OnUp ButtonUpHandlers; // 0600016e/16f

        [NonSerialized] private bool m_providerRegistered;
        [NonSerialized] private bool m_inputIsDown;

        public override float Modify(float value, float deltaTime) // 06000170
        {
            if (!m_providerRegistered)
            {
                m_controlMapping.RegisterButtonProvider(this);
                m_providerRegistered = true;
            }

            bool meetsCriteria;
            switch (m_criteriaRule)
            {
                case CriteriaRule.GreaterThan: meetsCriteria = value > m_criteriaValue; break;
                case CriteriaRule.LessThan: meetsCriteria = value < m_criteriaValue; break;
                case CriteriaRule.GreaterThanOrEqual: meetsCriteria = value >= m_criteriaValue; break;
                case CriteriaRule.LessThanOrEqual: meetsCriteria = value <= m_criteriaValue; break;
                case CriteriaRule.EqualTo: meetsCriteria = Mathf.Approximately(value, m_criteriaValue); break;
                case CriteriaRule.NotEqualTo: meetsCriteria = !Mathf.Approximately(value, m_criteriaValue); break;
                default: meetsCriteria = false; break;
            }

            // Original invokes delegates without a null guard. Callback faults
            // occur before the following state store. Down is not edge-gated.
            if (meetsCriteria)
            {
                if (m_heldButton)
                    ButtonHandlers(m_joystickIndex, m_gameInputToTrigger, m_valueToTriggerWith, m_inputType);
                else
                {
                    ButtonDownHandlers(m_joystickIndex, m_gameInputToTrigger, m_valueToTriggerWith, m_inputType);
                    m_inputIsDown = true;
                }
            }
            else if (m_inputIsDown)
            {
                ButtonUpHandlers(m_joystickIndex, m_gameInputToTrigger, m_valueToTriggerWith, m_inputType);
                m_inputIsDown = false;
            }
            return value;
        }

        public override void Reset() // 06000171: no provider unregistration or registration flag reset.
        {
            if (m_inputIsDown)
            {
                ButtonUpHandlers(m_joystickIndex, m_gameInputToTrigger, m_valueToTriggerWith, m_inputType);
                m_inputIsDown = false;
            }
        }

        public ModifierTriggerInputOnCriteria() { } // 06000172: original InputType literal0x35821dfb before base.
    }
}
