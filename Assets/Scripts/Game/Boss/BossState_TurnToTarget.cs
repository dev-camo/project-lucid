using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0200022e; bodies translated from the 1.10.1 x86-64 binary.
    // Turns the boss, at the movement ability's turn speed, towards the ability's target offset until it is within the
    // ability's face-angle tolerance; the boss animator's bool is set while it is still outside the tolerance.
    [GraphNodeMenuFormat("Boss/{0}")]
    [GraphNodeDefaultName("Turn Towards Target")]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class BossState_TurnToTarget : ActorState_Ability<ActorAbility>
    {
        protected BossState_TurnToTarget(FiniteStateMachine fsm, FSMIdentifier stateId, AbilityJSONCtorArgs ctorArgs) : base(fsm, stateId, ctorArgs)
        {
        }

        public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
        {
            return new BossState_TurnToTarget(fsm, transitionId, JsonUtility.FromJson<AbilityJSONCtorArgs>(jsonCtorArgs));
        }

        protected override void DoUpdate(IGraphUser user, FSMUpdateContext context)
        {
            base.DoUpdate(user, context);
            Boss boss = user.GetAs<Boss>();
            ActorAbility ability = GetAbility(boss);
            if (ability == null)
                return;
            IAbilityBossMovement movement = ability as IAbilityBossMovement;
            float angle = Vector3.SignedAngle(boss.ForwardDirection, movement.CurrentTargetPosition, boss.UpDirection);
            float absoluteAngle = Mathf.Abs(angle);
            float tolerance = movement.FaceAngleTolerance;
            float direction = 0f;
            if (absoluteAngle > tolerance)
                direction = angle > 0f ? 1f : -1f;
            SetAnimationBool(boss.Animator, ActorAnimationType.BoolSet, absoluteAngle > tolerance);
            TurnToward(boss, movement.TurnSpeed * direction * context.DeltaTime);
        }

        private void TurnToward(Boss boss, float turnAmount)
        {
            boss.SetWorldRotation(Quaternion.AngleAxis(turnAmount, boss.UpDirection) * boss.WorldRotation);
        }
    }
}
