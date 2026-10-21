using System;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000224 and its arguments class 02000225; bodies translated from the 1.10.1 x86-64 binary.
    // Snaps the boss's smoothed rotator to a local rotation on entry and/or on leaving, with its smoothing off for the snap.
    [GraphNodeMenuFormat("Boss/Guardian/{0}")]
    [GraphNodeDefaultName("SnapBodyToRotation")]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class BossState_SnapBodyToRotation : BossState_SetValue
    {
        [FiniteStateMachineJsonCtorArgs]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        [Il2CppSetOption(Option.NullChecks, false)]
        [Serializable]
        protected class RotationJSONCtorArgs : SettableJSONCtorArgs
        {
            public Vector3 Rotation;

            public RotationJSONCtorArgs()
            {
            }
        }

        private Vector3 m_rotation;

        protected BossState_SnapBodyToRotation(FiniteStateMachine fsm, FSMIdentifier stateId, RotationJSONCtorArgs ctorArgs) : base(fsm, stateId, ctorArgs)
        {
            m_rotation = ctorArgs.Rotation;
        }

        public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
        {
            return new BossState_SnapBodyToRotation(fsm, transitionId, JsonUtility.FromJson<RotationJSONCtorArgs>(jsonCtorArgs));
        }

        protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
        {
            base.DoOnEnter(user, action);
            if (m_setOnEntry)
            {
                Boss boss = user.GetAs<Boss>();
                boss.SmoothedTransformRotator.ToggleSmoothing(false);
                boss.SmoothedTransformRotator.UpdateLocalRotation(m_rotation, 0f);
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
                boss.SmoothedTransformRotator.UpdateLocalRotation(m_rotation, 0f);
                boss.SmoothedTransformRotator.ToggleSmoothing(true);
            }
        }
    }
}
