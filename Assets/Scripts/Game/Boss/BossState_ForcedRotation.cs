using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000209; bodies translated from the 1.10.1 x86-64 binary.
    // Sets the boss's rotation to the spin-on-the-spot ability's current angle about the boss's up direction.
    [GraphNodeMenuFormat("Boss/{0}")]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [GraphNodeDefaultName("ForceRotation")]
    public class BossState_ForcedRotation : ActorState_Ability<ActorAbility>
    {
        protected BossState_ForcedRotation(FiniteStateMachine fsm, FSMIdentifier stateId, AbilityJSONCtorArgs ctorArgs) : base(fsm, stateId, ctorArgs)
        {
        }

        public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
        {
            return new BossState_ForcedRotation(fsm, transitionId, JsonUtility.FromJson<AbilityJSONCtorArgs>(jsonCtorArgs));
        }

        protected override void DoUpdate(IGraphUser user, FSMUpdateContext context)
        {
            base.DoUpdate(user, context);
            Boss boss = user.GetAs<Boss>();
            TurnToward(boss, GetAbility<BossAbility_SpinOnSpot>(boss).RotationInDegrees);
        }

        private void TurnToward(Boss boss, float turnAmount)
        {
            boss.SetWorldRotation(Quaternion.AngleAxis(turnAmount, boss.UpDirection));
        }
    }
}
