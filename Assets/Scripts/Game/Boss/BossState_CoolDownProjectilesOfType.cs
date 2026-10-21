using System;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000202 and its arguments class 02000203; bodies translated from the 1.10.1 x86-64 binary.
    // Forces the projectiles of one type into their cool-down when the state is entered and/or left.
    [GraphNodeMenuFormat("Boss/Guardian/{0}")]
    [GraphNodeDefaultName("CoolDownProjectilesOfType")]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class BossState_CoolDownProjectilesOfType : ActorState_Effect
    {
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        [Il2CppSetOption(Option.NullChecks, false)]
        [FiniteStateMachineJsonCtorArgs]
        [Serializable]
        protected class ProjectileJSONCtorArgs : BaseJSONCtorArgs
        {
            public bool CoolDownAllOnEntry;

            public bool CoolDownAllOnLeave;

            public bool AllowExceptions;

            public ProjectileType ProjectileType;

            public ProjectileJSONCtorArgs()
            {
            }
        }

        private bool m_cooldownAllOnEntry;
        private bool m_cooldownAllOnLeave;
        private bool m_allowExceptions;
        private ProjectileType m_projectileType;

        protected BossState_CoolDownProjectilesOfType(FiniteStateMachine fsm, FSMIdentifier stateId, ProjectileJSONCtorArgs ctorArgs) : base(fsm, stateId, ctorArgs)
        {
            m_cooldownAllOnEntry = ctorArgs.CoolDownAllOnEntry;
            m_cooldownAllOnLeave = ctorArgs.CoolDownAllOnLeave;
            m_projectileType = ctorArgs.ProjectileType;
            m_allowExceptions = ctorArgs.AllowExceptions;
        }

        public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
        {
            return new BossState_CoolDownProjectilesOfType(fsm, transitionId, JsonUtility.FromJson<ProjectileJSONCtorArgs>(jsonCtorArgs));
        }

        protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
        {
            base.DoOnEnter(user, action);
            if (m_cooldownAllOnEntry)
                ProcessManager.GetSystem<ProjectileManager>().ForceCoolDownOfType(m_projectileType, m_allowExceptions);
        }

        protected override void DoOnLeave(IGraphUser user, FSMStateChangeAction action)
        {
            base.DoOnLeave(user, action);
            if (m_cooldownAllOnLeave)
                ProcessManager.GetSystem<ProjectileManager>().ForceCoolDownOfType(m_projectileType, m_allowExceptions);
        }
    }
}
