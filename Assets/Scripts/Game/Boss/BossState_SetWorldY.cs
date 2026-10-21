using System;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000221 and its arguments class 02000222; bodies translated from the 1.10.1 x86-64 binary.
    // Snaps the boss's world height to a value on entry and/or on leaving, with the rotator's smoothing off for the move.
    [GraphNodeMenuFormat("Boss/Guardian/{0}")]
    [GraphNodeDefaultName("SetWorldY")]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class BossState_SetWorldY : BossState_SetValue
    {
        [Il2CppSetOption(Option.NullChecks, false)]
        [FiniteStateMachineJsonCtorArgs]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        [Serializable]
        protected class SetYJSONCtorArgs : SettableJSONCtorArgs
        {
            public float YValue;

            public SetYJSONCtorArgs()
            {
            }
        }

        private float m_yValue;

        protected BossState_SetWorldY(FiniteStateMachine fsm, FSMIdentifier stateId, SetYJSONCtorArgs ctorArgs) : base(fsm, stateId, ctorArgs)
        {
            m_yValue = ctorArgs.YValue;
        }

        public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
        {
            return new BossState_SetWorldY(fsm, transitionId, JsonUtility.FromJson<SetYJSONCtorArgs>(jsonCtorArgs));
        }

        protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
        {
            base.DoOnEnter(user, action);
            if (m_setOnEntry)
            {
                Boss boss = user.GetAs<Boss>();
                boss.SmoothedTransformRotator.ToggleSmoothing(false);
                Vector3 position = boss.WorldPosition;
                boss.SetWorldPosition(new Vector3(position.x, m_yValue, position.z));
                boss.SmoothedTransformRotator.ToggleSmoothing(true);
            }
        }

        protected override void DoOnLeave(IGraphUser user, FSMStateChangeAction action)
        {
            base.DoOnLeave(user, action);
            if (m_setOnLeave)
            {
                Boss boss = user.GetAs<Boss>();
                boss.SmoothedTransformRotator.ToggleSmoothing(false);
                Vector3 position = boss.WorldPosition;
                boss.SetWorldPosition(new Vector3(position.x, m_yValue, position.z));
                boss.SmoothedTransformRotator.ToggleSmoothing(true);
            }
        }
    }
}
