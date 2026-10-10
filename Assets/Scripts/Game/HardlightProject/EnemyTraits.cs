using System;
using UnityEngine;

namespace HardlightProject
{
    [Serializable]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
    public class EnemyTraits : ActorTraits
    {
        [UnityEngine.TooltipAttribute("Speed to rotate the enemy in alignment with gravity.")]
        [UnityEngine.MinAttribute(0f)]
        [UnityEngine.HeaderAttribute("Movement.")]
        public float RotateToGravityDegreesPerSecond = 180f;

        [UnityEngine.TooltipAttribute("Only turn when angle to destination is more than this.")]
        [UnityEngine.HeaderAttribute("Brain.")]
        public float FaceAngleTolerance = 3f;

        [UnityEngine.TooltipAttribute("Only move forward if angle to destination is less than this - otherwise, turn on the spot.")]
        public float MaxAngleForwardMovement = 45f;

        [UnityEngine.TooltipAttribute("Don't move forward if distance to destination is less than this.")]
        public float MoveWithinDistanceTolerance = 2f;

        [UnityEngine.TooltipAttribute("Time to spend idling before choosing a new spot to move to.")]
        public float IdleSeconds = 5f;

        [UnityEngine.TooltipAttribute("Maximum distance to look for character to watch or move towards.")]
        public float SightDistance = 20f;

        [UnityEngine.TooltipAttribute("Is there an extra hazard attached to the enemy for attacking?")]
        public bool HasExtraAttackHazard;

        [UnityEngine.TooltipAttribute("Enemy is revived when character respawns either due to OOB or on death.")]
        public bool ReviveOnCharacterRespawn;

        [UnityEngine.TooltipAttribute("If the visual proxy needs to be disconnected to function correctly.")]
        public bool ShouldDetachVisualProxy;

        [UnityEngine.TooltipAttribute("If ticked the enemy will move to the player location at time of sighting, not chase their active position.")]
        public bool CacheTargetLocationOnSight;

        [Hardlight.ShowIfAttribute("CacheTargetLocationOnSight", (string)null)]
        [UnityEngine.TooltipAttribute("How long should they try and reach the cached location for?")]
        public float TimeoutForReachingCachedLocation;

        [UnityEngine.TooltipAttribute("How long to watch the player before charging.")]
        public float WatchToChaseTimeout = 1f;

        [UnityEngine.TooltipAttribute("How far to go past the cached target.")]
        public float OvershootAmount = 5f;

        public float MoveWithinDistanceToleranceSquared { get; private set; }
        public float SightDistanceSquared { get; private set; }
        public float RotateToGravityRadiansPerSecond { get; private set; }

        // Original 06001c98: movement and sight cache squares precede the
        // degrees-to-radians cache. There are no clamps or base callbacks.
        public void UpdateCachedValues()
        {
            MoveWithinDistanceToleranceSquared = MoveWithinDistanceTolerance * MoveWithinDistanceTolerance;
            SightDistanceSquared = SightDistance * SightDistance;
            RotateToGravityRadiansPerSecond = RotateToGravityDegreesPerSecond * Mathf.Deg2Rad;
        }

        // Original 06001c99 writes the eight nonzero field defaults before
        // the genuine ActorTraits constructor initializes its Forms list.
        public EnemyTraits() { }
    }
}
