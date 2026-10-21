using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0200065b; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [GraphNodeMenuFormat("Enemy/Movement/{0}")]
    public class EnemyMovementTransition_AttackWindUpComplete : EnemyMovementTransition
    {
        // 06002229 (1.10.1 RVA 0x577EF0)
        protected EnemyMovementTransition_AttackWindUpComplete(FiniteStateMachine fsm, FSMIdentifier transitionId, BaseJSONCtorArgs ctorArgs) : base(fsm, transitionId, ctorArgs) { }

        // 0600222a (1.10.1 RVA 0x577F00)
        public static IFSMTransition ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
        {
            return new EnemyMovementTransition_AttackWindUpComplete(fsm, transitionId, JsonUtility.FromJson<BaseJSONCtorArgs>(jsonCtorArgs));
        }

        // 0600222b (1.10.1 RVA 0x577F90): the enemy reports its attack wind-up complete.
        protected override bool DoUpdate(IGraphUser user, FSMUpdateContext context)
        {
            return user.GetAs<Enemy>().WindUpAttackComplete;
        }
    }
}
