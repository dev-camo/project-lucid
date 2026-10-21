using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0200022c; bodies translated from the 1.10.1 x86-64 binary.
    // Turns the boss, at the movement ability's turn speed, until it is within the ability's face-angle tolerance of the offset
    // from the Fourmen's average position to the boss itself; the boss animator's bool is set while it is outside the tolerance.
    [GraphNodeMenuFormat("Boss/Fourmen/{0}")]
    [Il2CppSetOption(Option.NullChecks, false)]
    [GraphNodeDefaultName("Turn Towards Boss Manager Target")]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class BossState_TurnToBossManagerTarget : ActorState_Ability<ActorAbility>
    {
        protected BossState_TurnToBossManagerTarget(FiniteStateMachine fsm, FSMIdentifier stateId, AbilityJSONCtorArgs ctorArgs) : base(fsm, stateId, ctorArgs)
        {
        }

        public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
        {
            return new BossState_TurnToBossManagerTarget(fsm, transitionId, JsonUtility.FromJson<AbilityJSONCtorArgs>(jsonCtorArgs));
        }

        protected override void DoUpdate(IGraphUser user, FSMUpdateContext context)
        {
            base.DoUpdate(user, context);
            Boss boss = user.GetAs<Boss>();
            ActorAbility ability = GetAbility(boss);
            if (ability == null)
                return;
            IAbilityBossMovement movement = ability as IAbilityBossMovement;
            FourmenManager fourmen = ProcessManager.GetSystem<FourmenManager>();
            Vector3 fromAverage = boss.WorldPosition - fourmen.GetAveragePosition();
            float angle = Vector3.SignedAngle(boss.ForwardDirection, fromAverage, boss.UpDirection);
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
