using System;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0200021c and its arguments class 0200021d; bodies translated from the 1.10.1 x86-64 binary.
    // Moves the projectile spawn point of one attach point to a local position on entry and/or on leaving.
    [GraphNodeDefaultName("SetProjectileSpawnPoint")]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    [GraphNodeMenuFormat("Boss/Guardian/{0}")]
    public class BossState_SetProjectileSpawnPoint : BossState_SetValue
    {
        [Il2CppSetOption(Option.NullChecks, false)]
        [FiniteStateMachineJsonCtorArgs]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        [Serializable]
        protected class PositionJSONCtorArgs : SettableJSONCtorArgs
        {
            public Vector3 Position;

            public ActorAttachPointType AttachPoint;

            public PositionJSONCtorArgs()
            {
            }
        }

        private Vector3 m_position;
        private ActorAttachPointType m_attachPoint;

        protected BossState_SetProjectileSpawnPoint(FiniteStateMachine fsm, FSMIdentifier stateId, PositionJSONCtorArgs ctorArgs) : base(fsm, stateId, ctorArgs)
        {
            m_position = ctorArgs.Position;
            m_attachPoint = ctorArgs.AttachPoint;
        }

        public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
        {
            return new BossState_SetProjectileSpawnPoint(fsm, transitionId, JsonUtility.FromJson<PositionJSONCtorArgs>(jsonCtorArgs));
        }

        protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
        {
            base.DoOnEnter(user, action);
            if (m_setOnEntry)
                user.GetAs<Boss>().GetSpawnPoint<Projectile>(m_attachPoint).localPosition = m_position;
        }

        protected override void DoOnLeave(IGraphUser user, FSMStateChangeAction action)
        {
            base.DoOnLeave(user, action);
            if (m_setOnLeave)
                user.GetAs<Boss>().GetSpawnPoint<Projectile>(m_attachPoint).localPosition = m_position;
        }
    }
}
