using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime020009de; all four complete native bodies retained.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public static class ActorMovementUtilities
    {
        public static float GetSlopeAngle(Actor actor)
        {
            Vector3 forward = actor.ForwardDirection;
            Vector3 right = actor.RightDirection;
            return GetSlopeAngle(forward, right, actor);
        }

        public static float GetSlopeAngle(Quaternion slopeRotation, Actor actor)
        {
            Vector3 forward = slopeRotation * Vector3.forward;
            Vector3 right = slopeRotation * Vector3.right;
            return GetSlopeAngle(forward, right, actor);
        }

        private static float GetSlopeAngle(Vector3 forward, Vector3 right, Actor actor)
        {
            // Both originals construct the rotation before reading virtual Gravity.
            Quaternion rotation = Quaternion.AngleAxis(-90f, right);
            Vector3 gravity = actor.Gravity;
            float angle = Vector3.SignedAngle(forward, rotation * gravity, right);
            if (angle < 0f)
                angle += 360f;
            return angle;
        }

        public static void OrientateHeadingToForward(this Actor actor)
        {
            actor.OrientateVelocityToForward();
            Vector3 velocity = actor.WorldVelocity;
            actor.Storage.SetValue(ActorFSMKeys.AbilityHeading, velocity);
        }
    }
}
