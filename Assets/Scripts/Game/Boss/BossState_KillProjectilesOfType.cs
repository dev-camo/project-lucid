using System;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0200020b and its arguments class 0200020c; bodies translated from the 1.10.1 x86-64 binary.
    // Kills every projectile of one type when the state is entered and/or left.
    [GraphNodeMenuFormat("Boss/Guardian/{0}")]
    [GraphNodeDefaultName("KillProjectilesOfType")]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class BossState_KillProjectilesOfType : ActorState_Effect
    {
        [Il2CppSetOption(Option.NullChecks, false)]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        [FiniteStateMachineJsonCtorArgs]
        [Serializable]
        protected class ProjectileJSONCtorArgs : BaseJSONCtorArgs
        {
            public bool KillAllOnEntry;

            public bool KillAllOnLeave;

            public ProjectileType ProjectileType;

            public ProjectileJSONCtorArgs()
            {
            }
        }

        private bool m_killAllOnEntry;
        private bool m_killAllOnLeave;
        private ProjectileType m_projectileType;

        protected BossState_KillProjectilesOfType(FiniteStateMachine fsm, FSMIdentifier stateId, ProjectileJSONCtorArgs ctorArgs) : base(fsm, stateId, ctorArgs)
        {
            m_killAllOnEntry = ctorArgs.KillAllOnEntry;
            m_killAllOnLeave = ctorArgs.KillAllOnLeave;
            m_projectileType = ctorArgs.ProjectileType;
        }

        public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
        {
            return new BossState_KillProjectilesOfType(fsm, transitionId, JsonUtility.FromJson<ProjectileJSONCtorArgs>(jsonCtorArgs));
        }

        protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
        {
            base.DoOnEnter(user, action);
            if (m_killAllOnEntry)
                ProcessManager.GetSystem<ProjectileManager>().KillAllOfType(m_projectileType);
        }

        protected override void DoOnLeave(IGraphUser user, FSMStateChangeAction action)
        {
            base.DoOnLeave(user, action);
            if (m_killAllOnLeave)
                ProcessManager.GetSystem<ProjectileManager>().KillAllOfType(m_projectileType);
        }
    }
}
