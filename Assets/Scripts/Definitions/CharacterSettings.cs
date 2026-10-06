// Complete original Character settings graph; native defaults and cache order retained.
using System;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;
namespace HardlightProject
{
    [Serializable]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class CharacterSettings // original0200051c
    {
        [Tooltip("Settings for general collider tracking.")]
        public ColliderSettings Collider;
        [Tooltip("Settings for general surface tracking.")]
        public SurfaceSettings Surface;
        [Tooltip("Settings for general animation parameters outside of FSM states.")]
        public ActorAnimationSettingsDefinition AnimationSettings;
        public void UpdateCachedValues() { Collider.UpdateCachedValues(); } // original06001be7
        public CharacterSettings() { } // be8: original null fields.

        [Serializable]
        [Il2CppSetOption(Option.NullChecks, false)]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        public class ColliderSettings // original0200051d
        {
            [Tooltip("Layers the character interacts with and treats as the usable track.")]
            public LayerMask TrackMask = -1;
            [Tooltip("Layers the character collides with.")]
            public LayerMask CollisionMask = -1;
            [Tooltip("Layers the character sticks to when within range.")]
            public LayerMask StickToColliderMask;
            [Range(0f, 89f)] [Tooltip("Maximum angle, when travelling at max speed, to stick to a collider.")]
            public float StickToColliderAngleMax = 45f;
            [Range(1f, 10f)] [Tooltip("Force applied when sticking to a collider.")]
            public float StickToColliderForce = 1f;
            [Tooltip("Height to project raycast from character in current velocity direction to detect a slope ahead.")] [Range(0f, 1f)]
            public float LookAheadSlopeHeight = 0.1f;
            [Tooltip("Minimum raycast distance for ground check.  This determines when to enter ground state.")]
            public float GroundCheckMinDistance = 0.1f;
            [Tooltip("Maximum raycast distance for ground check.  This determines when character is close enough to ground to not trigger any air abilities but to buffer any actions for when on ground.")]
            public float GroundCheckMaxDistance = 1f;
            [Tooltip("The distance to step when tracking along collider to target.")]
            public float TrackingStep = 5f;
            [Tooltip("The distance to offset raycast upwards when tracking along collider to target.")]
            public float TrackingDistanceUp = 1f;
            [Tooltip("The distance to project raycast downwards when tracking along collider to target.")]
            public float TrackingDistanceDown = 2f;
            [Tooltip("Maximum character angle deviation from gravity to be considered for sticking logic.")]
            public float MaxGravityDeviationAngle = 135f;
            public float MaxGravityDeviationAngleCosine { get; private set; } // be9/bea
            public float GroundCheckMaxDistanceSqr { get; private set; } // beb/bec
            public void UpdateCachedValues() // bed: calculate both values before publishing either cache.
            {
                float cosine = Mathf.Cos(MaxGravityDeviationAngle * Mathf.Deg2Rad);
                float distance = GroundCheckMaxDistance;
                float squaredDistance = distance * distance;
                MaxGravityDeviationAngleCosine = cosine;
                GroundCheckMaxDistanceSqr = squaredDistance;
            }
            public ColliderSettings() { } // bee: exact masks/scalars are field initializers.
        }

        [Serializable]
        [Il2CppSetOption(Option.NullChecks, false)]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        public class SurfaceSettings // original0200051e
        {
            [Tooltip("Prevents the character from going off the sides of ribbons.")]
            public bool ClampToSurfaceBoundsOnGround;
            [Tooltip("Prevents the character from going off the sides of ribbons while jumping.")]
            public bool ClampToSurfaceBoundsInAir;
            public SurfaceSettings() { } // bef: both clamps default false.
        }
    }
}
