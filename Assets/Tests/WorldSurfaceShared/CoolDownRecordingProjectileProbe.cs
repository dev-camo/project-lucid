using System.Collections.Generic;
using HardlightProject;
using UnityEngine;

namespace ProjectLucid.Tests.WorldSurfaceShared
{
    // Test-only projectile that records the allowExceptions argument of every ForceEnterCoolDown it receives; the other
    // members are the four abstract ones, and Kill is the shipped Projectile body.
    public sealed class CoolDownRecordingProjectileProbe : Projectile
    {
        public readonly List<bool> ForcedCoolDowns = new List<bool>();

        public override void ForceEnterCoolDown(bool allowExceptions) => ForcedCoolDowns.Add(allowExceptions);

        public override void UpdateFocus(Vector3 target) { }

        protected override Vector3 DoMovement(float adjustedDeltaTime) => Vector3.zero;

        protected override bool ShouldIgnoreCollider(Collider collider) => false;

        protected override bool ShouldTeleportToHitPoint() => false;
    }
}
