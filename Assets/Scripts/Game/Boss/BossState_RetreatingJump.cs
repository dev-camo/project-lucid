using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000210; bodies translated from the 1.10.1 x86-64 binary.
    // While the crab's retreating jump is enabled, turns the boss towards the character (at the ability's turn speed, until it
    // is within the face-angle tolerance), moves it to the ability's jump position and, once the jump has completed, clears
    // the animator's bool.
    [Il2CppSetOption(Option.NullChecks, false)]
    [GraphNodeDefaultName("RetreatingJump")]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [GraphNodeMenuFormat("Boss/Crab/{0}")]
    public class BossState_RetreatingJump : ActorState_Ability<ActorAbility>
    {
        private readonly AbilityDefinition m_abilityDefinition;

        protected BossState_RetreatingJump(FiniteStateMachine fsm, FSMIdentifier stateId, AbilityJSONCtorArgs ctorArgs) : base(fsm, stateId, ctorArgs)
        {
        }

        public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
        {
            return new BossState_RetreatingJump(fsm, transitionId, JsonUtility.FromJson<AbilityJSONCtorArgs>(jsonCtorArgs));
        }

        protected override void DoUpdate(IGraphUser user, FSMUpdateContext context)
        {
            base.DoUpdate(user, context);
            Boss boss = user.GetAs<Boss>();
            ActorAbility ability = GetAbility(boss);
            if (ability == null || !ability.Enabled)
                return;
            BossAbility_RetreatingJump jump = ability as BossAbility_RetreatingJump;
            Vector3 destination = jump.CurrentTargetPosition;
            if (!ProcessManager.GetSystem<CharacterManager>().TryGetCurrentCharacter(out Character character))
                return;
            float angle = Vector3.SignedAngle(boss.ForwardDirection, character.WorldPosition - boss.WorldPosition, boss.UpDirection);
            float direction = 0f;
            if (Mathf.Abs(angle) > jump.Definition.FaceAngleTolerance)
                direction = angle > 0f ? 1f : -1f;
            MoveToward(boss, destination, direction * jump.Definition.TurnSpeed * context.DeltaTime);
            if (jump.JumpProgress >= 1f)
                SetAnimationBool(boss.Animator, ActorAnimationType.BoolSet, false);
        }

        private void MoveToward(Boss boss, Vector3 destination, float turnAmount)
        {
            boss.SetWorldRotation(Quaternion.AngleAxis(turnAmount, boss.UpDirection) * boss.WorldRotation);
            boss.SetWorldPosition(destination);
        }

        protected override void DoOnLeave(IGraphUser user, FSMStateChangeAction action)
        {
            base.DoOnLeave(user, action);
        }
    }
}
