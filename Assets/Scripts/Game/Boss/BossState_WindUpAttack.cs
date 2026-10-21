using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000233; bodies translated from the 1.10.1 x86-64 binary.
    // Publishes whether the boss's offensive ability has finished winding up as the AttackWoundUp flag.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [GraphNodeDefaultName("WindingUpAttack")]
    [GraphNodeMenuFormat("Boss/{0}")]
    public class BossState_WindUpAttack : ActorState_Ability<ActorAbility>
    {
        // 06000c46 (1.10.1 RVA 0x70DCF0)
        protected BossState_WindUpAttack(FiniteStateMachine fsm, FSMIdentifier stateId, AbilityJSONCtorArgs ctorArgs) : base(fsm, stateId, ctorArgs)
        {
        }

        // 06000c47 (1.10.1 RVA 0x70DD60)
        public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
        {
            return new BossState_WindUpAttack(fsm, transitionId, JsonUtility.FromJson<AbilityJSONCtorArgs>(jsonCtorArgs));
        }

        // 06000c48 (1.10.1 RVA 0x70DE20)
        protected override void DoUpdate(IGraphUser user, FSMUpdateContext context)
        {
            base.DoUpdate(user, context);
            Boss boss = user.GetAs<Boss>();
            if (GetAbility(boss) is IOffensiveAbility offensiveAbility)
                boss.Storage.SetValue(BossFSMKeys.AttackWoundUp, offensiveAbility.WoundUp);
        }
    }
}
