using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Game.Runtime020009eb: complete original fieldless utility owner.
    // Requires the genuine Character/Actor, settings and Unity physics graph.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public static class CharacterPhysicsUtilities
    {
        // 0600390c. The inverted gravity guard lets unordered values reach Raycast.
        public static bool StickToCollider(Character character, float deltaTime)
        {
            var settings = character.Settings.Collider;
            var upDirection = character.UpDirection;
            float gravityAlignment = -Vector3.Dot(upDirection, character.GravityNormalised);
            if (gravityAlignment < settings.MaxGravityDeviationAngleCosine)
                return false;

            var velocity = character.WorldVelocity;
            float speed = velocity.magnitude;
            var position = character.WorldPosition;
            var direction = -character.UpDirection;
            float sine = Mathf.Sin(settings.StickToColliderAngleMax * Mathf.Deg2Rad);
            float distance = (speed * deltaTime) * sine;
            if (!Physics.Raycast(position, direction, out RaycastHit hit, distance, settings.StickToColliderMask))
                return false;

            float force = settings.StickToColliderForce;
            var normal = hit.normal;
            character.SetWorldVelocity(velocity - normal * (distance * force));
            return true;
        }

        // 0600390d. Original look-ahead ignores triggers and uses current velocity direction.
        // Rotation changes before the speed is reread for the final velocity publication.
        public static bool OrientateToLookAheadSlope(Character character, float deltaTime)
        {
            var settings = character.Settings.Collider;
            var position = character.WorldPosition + character.UpDirection * settings.LookAheadSlopeHeight;
            var direction = character.WorldVelocityNormalised;
            float distance = character.WorldVelocityMagnitude * deltaTime;
            if (!Physics.Raycast(position, direction, out RaycastHit hit, distance, character.TrackMask,
                QueryTriggerInteraction.Ignore))
                return false;

            var forward = (hit.point - character.WorldPosition).normalized;
            character.SetWorldRotation(Quaternion.LookRotation(forward, hit.normal));
            character.SetWorldVelocity(forward * character.WorldVelocityMagnitude);
            return true;
        }
    }
}
