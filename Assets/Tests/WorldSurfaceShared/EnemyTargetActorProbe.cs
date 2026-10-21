using Hardlight;
using HardlightProject;
using UnityEngine;

namespace ProjectLucid.Tests.WorldSurfaceShared
{
    // Test-only Actor that stands in as an enemy's sensed or focus target at a chosen position. It supplies only the
    // abstract members Actor leaves open; SetWorldPosition is the shipped Actor body, which an unstarted Enemy or
    // Character cannot run because their overrides need a transform or a physics body.
    public sealed class EnemyTargetActorProbe : Actor
    {
        public override float BrainMovementMagnitude => 0f;

        protected override void DoOnUpdate(float deltaTime)
        {
        }

        public override TimeCategory GetTimeCategory() => TimeCategory.EnemyMovement;

        protected override void UpdateBodyPosition(float deltaTime, out Vector3 position, out Quaternion rotation, out Vector3 velocity)
        {
            position = WorldPosition;
            rotation = WorldRotation;
            velocity = Vector3.zero;
        }

        public override void MovementStop(bool detectCollisions = false)
        {
        }

        public override void MovementResume(Vector3 position, Vector3 velocity)
        {
        }

        protected override void DoClose(bool isBeingDestroyed)
        {
        }
    }
}
