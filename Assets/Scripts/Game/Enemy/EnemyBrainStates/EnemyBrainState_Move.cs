using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 0200063f; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [GraphNodeMenuFormat("Enemy/Brain/{0}")]
    [Il2CppSetOption(Option.NullChecks, false)]
    public abstract class EnemyBrainState_Move : ActorState
    {
        // 060021c1 (1.10.1 RVA 0x572190)
        protected EnemyBrainState_Move(FiniteStateMachine fsm, FSMIdentifier stateId, BaseJSONCtorArgs ctorArgs) : base(fsm, stateId, ctorArgs) { }

        // 060021c2 (1.10.1 RVA 0x5721A0). Steers towards the destination in the plane
        // of the enemy's up direction: x turns (+1 / -1 once the signed angle exceeds
        // FaceAngleTolerance), y moves forward (1) only while the destination is
        // outside MoveWithinDistanceTolerance and the angle is under
        // MaxAngleForwardMovement. Returns whether the destination counts as reached:
        // within the distance tolerance, or, when forward movement is ignored, facing it.
        // The comparisons keep the native NaN behaviour (ucomiss with jbe / setae).
        public static bool SetMovementImpulseForDestination(EnemyBrain enemyBrain, Vector3 destination, bool ignoreForwardMovement = false)
        {
            EnemyTraits traits = enemyBrain.GetTraits();
            Enemy enemy = enemyBrain.Enemy;
            Vector3 toDestination = Vector3.ProjectOnPlane(destination - enemy.WorldPosition, enemy.UpDirection);
            float sqrDistance = toDestination.sqrMagnitude;
            float toleranceSqr = traits.MoveWithinDistanceToleranceSquared;
            float angle = Vector3.SignedAngle(enemy.ForwardDirection, toDestination, enemy.UpDirection);
            float absAngle = Mathf.Abs(angle);
            float faceAngleTolerance = traits.FaceAngleTolerance;

            float turn = 0f;
            float forward = 0f;
            if (absAngle > faceAngleTolerance)
            {
                turn = angle > 0f ? 1f : -1f;
            }
            if (!ignoreForwardMovement)
            {
                if (!(sqrDistance <= toleranceSqr) && absAngle < traits.MaxAngleForwardMovement)
                {
                    forward = 1f;
                }
            }
            enemyBrain.SetMovementImpulse(new Vector2(turn, forward));

            Debug.DrawLine(enemy.WorldPosition, destination, Color.red);

            bool facing = !(absAngle > faceAngleTolerance);
            bool withinTolerance = sqrDistance <= toleranceSqr;
            return ignoreForwardMovement ? facing : withinTolerance;
        }
    }
}
