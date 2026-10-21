using System;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Playables;

namespace HardlightProject
{
    // Original Game.Runtime 0200080a; native 0x5FC980.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Serializable]
    public class TimeMachineSkipBehaviour : TimeMachineBehaviour
    {
        public bool SkipEnabledState;
        private CutsceneManager m_cutsceneManager;

        // Original06002e85.
        public TimeMachineSkipBehaviour() { }

        // Original06002e86: copies the authored action, label, condition, input, marker, time and soften time.
        public TimeMachineSkipBehaviour(TimeMachineBehaviour behaviour, bool skipEnabledState)
        {
            m_action = behaviour.Action;
            m_markerLabel = behaviour.MarkerLabel;
            m_condition = behaviour.Condition;
            m_conditionInput = behaviour.ConditionInput;
            m_markerToJumpTo = behaviour.MarkerToJumpTo;
            m_timeToJumpTo = behaviour.TimeToJumpTo;
            m_endSoftenTime = behaviour.EndSoftenTime;
            SkipEnabledState = skipEnabledState;
        }

        // Original06002e87: entering the clip, while playing, switches the cutscene skip UI on or off.
        public override void OnBehaviourPlay(Playable playable, FrameData info)
        {
            if (!Application.isPlaying) return;
            m_cutsceneManager = ProcessManager.GetSystem<CutsceneManager>();
            if (SkipEnabledState) m_cutsceneManager.EnableCutsceneSkip();
            else m_cutsceneManager.DisableCutsceneSkip();
        }

        // Original06002e88: while playing, the tap or hold condition consumes the cutscene manager's pending skip event.
        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            if (!Application.isPlaying) return;
            switch (m_condition)
            {
                case TimeMachineCondition.InputHeld:
                    m_inputHoldOccurred = m_cutsceneManager.ConsumeSkipHoldEvent();
                    break;
                case TimeMachineCondition.Input:
                    m_inputPressOccurred = m_cutsceneManager.ConsumeSkipTapEvent();
                    break;
            }
        }
    }
}
