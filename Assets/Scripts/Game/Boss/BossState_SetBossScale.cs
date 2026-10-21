using System;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000218 and its arguments class 02000219; bodies translated from the 1.10.1 x86-64 binary.
    // Sets the local scale of the boss's smoothed rotator object on entry and/or on leaving.
    [Il2CppSetOption(Option.NullChecks, false)]
    [GraphNodeMenuFormat("Boss/Guardian/{0}")]
    [GraphNodeDefaultName("SetScale")]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class BossState_SetBossScale : BossState_SetValue
    {
        [FiniteStateMachineJsonCtorArgs]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        [Il2CppSetOption(Option.NullChecks, false)]
        [Serializable]
        protected class RotationJSONCtorArgs : SettableJSONCtorArgs
        {
            public Vector3 Scale;

            public RotationJSONCtorArgs()
            {
            }
        }

        private Vector3 m_scale;

        protected BossState_SetBossScale(FiniteStateMachine fsm, FSMIdentifier stateId, RotationJSONCtorArgs ctorArgs) : base(fsm, stateId, ctorArgs)
        {
            m_scale = ctorArgs.Scale;
        }

        public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
        {
            return new BossState_SetBossScale(fsm, transitionId, JsonUtility.FromJson<RotationJSONCtorArgs>(jsonCtorArgs));
        }

        protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
        {
            base.DoOnEnter(user, action);
            if (m_setOnEntry)
                user.GetAs<Boss>().SmoothedTransformRotator.gameObject.transform.localScale = m_scale;
        }

        protected override void DoOnLeave(IGraphUser user, FSMStateChangeAction action)
        {
            base.DoOnLeave(user, action);
            if (m_setOnLeave)
                user.GetAs<Boss>().SmoothedTransformRotator.gameObject.transform.localScale = m_scale;
        }
    }
}
