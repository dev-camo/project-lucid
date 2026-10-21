using System;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0200021a and its arguments class 0200021b; bodies translated from the 1.10.1 x86-64 binary.
    // Sets the projectile count of the one-at-a-time projectile ability on entry.
    [Il2CppSetOption(Option.NullChecks, false)]
    [GraphNodeDefaultName("SetProjectileCount")]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [GraphNodeMenuFormat("Boss/Guardian/{0}")]
    public class BossState_SetProjectileCount : ActorState_Ability<ActorAbility>
    {
        [Il2CppSetOption(Option.NullChecks, false)]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        [Serializable]
        protected class ProjectileCountJSONCtorArgs : AbilityJSONCtorArgs
        {
            public int ProjectileCount;

            public ProjectileCountJSONCtorArgs()
            {
            }
        }

        private int m_projectileCount;

        protected BossState_SetProjectileCount(FiniteStateMachine fsm, FSMIdentifier stateId, ProjectileCountJSONCtorArgs ctorArgs) : base(fsm, stateId, ctorArgs)
        {
            m_projectileCount = ctorArgs.ProjectileCount;
        }

        public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
        {
            return new BossState_SetProjectileCount(fsm, transitionId, JsonUtility.FromJson<ProjectileCountJSONCtorArgs>(jsonCtorArgs));
        }

        protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
        {
            base.DoOnEnter(user, action);
            GetAbility<BossAbility_ProjectileSpawnAndShootOneAtATime>(user.GetAs<Boss>()).SetProjectileCount(m_projectileCount);
        }
    }
}
