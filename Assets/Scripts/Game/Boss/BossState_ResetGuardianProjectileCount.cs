using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0200020e; bodies translated from the 1.10.1 x86-64 binary.
    // Resets the offensive ability's projectile group counter on entry.
    [GraphNodeDefaultName("ResetGuardianProjectileCount")]
    [GraphNodeMenuFormat("Boss/Guardian/{0}")]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class BossState_ResetGuardianProjectileCount : ActorState_Ability<ActorAbility>
    {
        protected BossState_ResetGuardianProjectileCount(FiniteStateMachine fsm, FSMIdentifier stateId, AbilityJSONCtorArgs ctorArgs) : base(fsm, stateId, ctorArgs)
        {
        }

        public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
        {
            return new BossState_ResetGuardianProjectileCount(fsm, transitionId, JsonUtility.FromJson<AbilityJSONCtorArgs>(jsonCtorArgs));
        }

        protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
        {
            base.DoOnEnter(user, action);
            ActorAbility ability = GetAbility(user.GetAs<Boss>());
            if (ability == null)
                return;
            (ability as IOffensiveAbility).ResetProjectileGroupCounter();
        }
    }
}
