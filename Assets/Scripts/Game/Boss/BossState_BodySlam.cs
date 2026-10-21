using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 020001fe; bodies translated from the 1.10.1 x86-64 binary.
    // While the crab's body slam is wound up, turns the boss towards the character (at the ability's turn speed, until it is
    // within the face-angle tolerance) and moves it to the ability's jump target.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [GraphNodeMenuFormat("Boss/Crab/{0}")]
    [Il2CppSetOption(Option.NullChecks, false)]
    [GraphNodeDefaultName("BodySlam")]
    public class BossState_BodySlam : ActorState_Ability<ActorAbility>
    {
        private readonly AbilityDefinition m_abilityDefinition;

        protected BossState_BodySlam(FiniteStateMachine fsm, FSMIdentifier stateId, AbilityJSONCtorArgs ctorArgs) : base(fsm, stateId, ctorArgs)
        {
        }

        public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
        {
            return new BossState_BodySlam(fsm, transitionId, JsonUtility.FromJson<AbilityJSONCtorArgs>(jsonCtorArgs));
        }

        protected override void DoUpdate(IGraphUser user, FSMUpdateContext context)
        {
            base.DoUpdate(user, context);
            Boss boss = user.GetAs<Boss>();
            ActorAbility ability = GetAbility(boss);
            if (ability == null)
                return;
            BossAbility_BodySlam bodySlam = ability as BossAbility_BodySlam;
            if (!bodySlam.WoundUp)
                return;
            HandleMovement(boss, bodySlam, context);
        }

        // The turn is a full turn-speed step towards the character's side while the angle to the character exceeds the
        // tolerance (the first slam may use its own tolerance), and none within it.
        private void HandleMovement(Boss boss, BossAbility_BodySlam ability, FSMUpdateContext context)
        {
            Character character = ProcessManager.GetSystem<CharacterManager>().GetCurrentCharacterUnsafe();
            Vector3 toCharacter = character.WorldPosition - boss.WorldPosition;
            float angle = Vector3.SignedAngle(boss.ForwardDirection, toCharacter, boss.UpDirection);
            float tolerance = ability.Definition.FaceAngleTolerance;
            if (ability.AttackIndex == 0 && ability.Definition.CustomInitialFaceAngleTolerance)
                tolerance = ability.Definition.InitialFaceAngleTolerance;
            float turn = 0f;
            if (Mathf.Abs(angle) > tolerance)
                turn = angle > 0f ? 1f : -1f;
            MoveToward(boss, ability.CurrentTargetPosition, turn * ability.Definition.TurnSpeed * context.DeltaTime);
        }

        private void MoveToward(Boss boss, Vector3 destination, float turnAmount)
        {
            boss.SetWorldRotation(Quaternion.AngleAxis(turnAmount, boss.UpDirection) * boss.WorldRotation);
            boss.SetWorldPosition(destination);
        }
    }
}
