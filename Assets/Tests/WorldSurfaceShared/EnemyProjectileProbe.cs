using System.Collections.Generic;
using HardlightProject;
using UnityEngine;

namespace ProjectLucid.Tests.WorldSurfaceShared
{
    // Test-only projectile for the fire-projectile ability fixtures. It supplies the four abstract members and
    // records the focus updates and fires the ability asks for; Kill is the shipped Projectile body.
    public sealed class EnemyProjectileProbe : Projectile
    {
        public readonly List<Vector3> Focuses = new List<Vector3>();
        public int Fires;

        public override void UpdateFocus(Vector3 target) => Focuses.Add(target);

        public override void Fire() => Fires++;

        protected override Vector3 DoMovement(float adjustedDeltaTime) => Vector3.zero;

        protected override bool ShouldIgnoreCollider(Collider collider) => false;

        protected override bool ShouldTeleportToHitPoint() => false;
    }
}
