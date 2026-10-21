using System;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Playables;

namespace HardlightProject
{
    // Original Game.Runtime 02000805 (+ enums 02000806/02000807); native 0x5FC360.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Serializable]
    public class TimeMachineBehaviour : PlayableBehaviour
    {
        public enum TimeMachineCondition
        {
            Always = 0,
            Input = 1,
            InputHeld = 2
        }

        public enum TimeMachineAction
        {
            Marker = 0,
            JumpToTime = 1,
            JumpToMarker = 2,
            JumpToEnd = 3,
            Pause = 4,
            DoNothing = 5
        }

        [SerializeField] protected TimeMachineAction m_action;
        [SerializeField] protected string m_markerLabel;
        [SerializeField] protected TimeMachineCondition m_condition;
        [HideIf("m_condition", TimeMachineCondition.Always)]
        [SerializeField]
        protected GameInput m_conditionInput;
        [ShowIf("m_action", TimeMachineAction.JumpToMarker)]
        [SerializeField]
        protected string m_markerToJumpTo;
        [ShowIf("m_action", TimeMachineAction.JumpToTime)]
        [SerializeField]
        protected float m_timeToJumpTo;
        [ShowIf("m_action", TimeMachineAction.JumpToEnd)]
        [SerializeField]
        protected float m_endSoftenTime = 0.1f;
        [HideInInspector] public bool ClipExecuted;
        protected bool m_inputPressOccurred;
        protected bool m_inputHoldOccurred;

        // Originals 06002e72..06002e78: plain field getters.
        public TimeMachineAction Action => m_action;
        public string MarkerLabel => m_markerLabel;
        public TimeMachineCondition Condition => m_condition;
        public GameInput ConditionInput => m_conditionInput;
        public string MarkerToJumpTo => m_markerToJumpTo;
        public float TimeToJumpTo => m_timeToJumpTo;
        public float EndSoftenTime => m_endSoftenTime;

        // Original06002e79: listens for the release of the condition input from the moment the graph starts.
        public override void OnGraphStart(Playable playable)
        {
            ControlMapping.Subscribe(m_conditionInput, InputPressed, InputTrigger.Up, -1);
        }

        // Original06002e7a: the float value is ignored.
        private void InputPressed(float obj) { m_inputPressOccurred = true; }

        // Original06002e7b: an input condition reports and consumes its own pending flag; an undefined one never fires.
        public bool ConditionMet()
        {
            switch (m_condition)
            {
                case TimeMachineCondition.Always:
                    return true;
                case TimeMachineCondition.Input:
                {
                    bool occurred = m_inputPressOccurred;
                    m_inputPressOccurred = false;
                    return occurred;
                }
                case TimeMachineCondition.InputHeld:
                {
                    bool occurred = m_inputHoldOccurred;
                    m_inputHoldOccurred = false;
                    return occurred;
                }
                default:
                    return false;
            }
        }

        // Implicit original06002e7c: end soften time defaults to 0.1 seconds.
    }
}
