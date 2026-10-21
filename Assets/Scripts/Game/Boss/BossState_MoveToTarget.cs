using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0200020d; bodies translated from the 1.10.1 x86-64 binary.
    // While the Fourmen movement ability is enabled, drives the boss to the ability's target position (or, for a momentum
    // move, treats that vector as its horizontal velocity) and feeds the movement into the boss animator.
    [GraphNodeMenuFormat("Boss/Fourmen/{0}")]
    [GraphNodeDefaultName("MoveToTarget")]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class BossState_MoveToTarget : ActorState_Ability<ActorAbility>
    {
        private const float RaycastStartHeightBoost = 20f;
        private const float RaycastDistanceMultiplier = 2f;

        protected BossState_MoveToTarget(FiniteStateMachine fsm, FSMIdentifier stateId, AbilityJSONCtorArgs ctorArgs) : base(fsm, stateId, ctorArgs)
        {
        }

        public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
        {
            return new BossState_MoveToTarget(fsm, transitionId, JsonUtility.FromJson<AbilityJSONCtorArgs>(jsonCtorArgs));
        }

        // Nothing happens while the target equals (Vector3 equality) the boss's position.
        protected override void DoUpdate(IGraphUser user, FSMUpdateContext context)
        {
            base.DoUpdate(user, context);
            Boss boss = user.GetAs<Boss>();
            ActorAbility ability = GetAbility(boss);
            if (ability == null || !ability.Enabled)
                return;
            IAbilityBossMovement movement = ability as IAbilityBossMovement;
            Vector3 destination = movement.CurrentTargetPosition;
            if (movement.CurrentTargetPosition == boss.WorldPosition)
                return;
            Vector3 direction = boss.transform.InverseTransformPoint(destination).normalized;
            if (boss.GetTraits().UsesSmoothedTransformRotator)
                SetBossMovementDirections(boss, movement.MovementAnimationAngleMultiplier * direction.x, direction.z, context.DeltaTime);
            HandleMovement(boss, destination, movement.IsMomentum, context.DeltaTime);
        }

        // A momentum move sets the horizontal velocity from the vector itself. Otherwise the boss is moved onto the destination
        // and the animator speed is the squared speed implied by the step from the previous position (the destination itself
        // when the stored previous position is approximately zero, including when none was stored). The ground raycast result is not used.
        private void HandleMovement(Boss boss, Vector3 destination, bool isMomentum, float deltaTime)
        {
            float speed;
            if (isMomentum)
            {
                Vector3 velocity = new Vector3(destination.x, 0f, destination.z);
                boss.SetWorldVelocity(velocity);
                speed = velocity.sqrMagnitude;
            }
            else
            {
                GetHeightFromRaycast(boss, boss.WorldPosition);
                Vector3 previous = boss.Storage.GetValue<Vector3>(BossFSMKeys.CurrentPosition);
                if (previous == Vector3.zero)
                    previous = destination;
                speed = ((previous - destination) / deltaTime).sqrMagnitude;
                boss.Storage.SetValue(BossFSMKeys.CurrentPosition, destination);
                boss.SetWorldPosition(destination);
            }
            SetBossMovementSpeed(boss.Animator, speed);
        }

        protected override void DoOnLeave(IGraphUser user, FSMStateChangeAction action)
        {
            base.DoOnLeave(user, action);
            user.GetAs<Boss>().SetWorldVelocity(Vector3.zero);
        }

        private void SetBossMovementDirections(Boss boss, float normalisedDistanceX, float normalisedDistanceZ, float deltaTime)
        {
            boss.SmoothedTransformRotator.UpdateLocalRotation(new Vector3(normalisedDistanceX, 0f, normalisedDistanceZ), deltaTime);
        }

        // The height of the ground below the position along the boss's gravity, or the position's own height without a hit.
        private float GetHeightFromRaycast(Boss boss, Vector3 worldPosition)
        {
            if (Physics.Raycast(new Ray(worldPosition, -boss.UpDirection), out RaycastHit hit,
                (worldPosition.y + RaycastStartHeightBoost) * RaycastDistanceMultiplier, boss.GetTraits().TrackMask))
                return hit.point.y;
            return worldPosition.y;
        }
    }
}
