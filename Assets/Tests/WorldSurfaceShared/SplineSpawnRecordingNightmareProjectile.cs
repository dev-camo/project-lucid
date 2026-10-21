using System.Collections.Generic;
using Hardlight;
using HardlightProject;
using UnityEngine;

namespace ProjectLucid.Tests.WorldSurfaceShared
{
    // Test-only NightmareEggmanProjectile that records what SplineProjectileSpawner.Fire hands it: the focus of each Setup (through
    // UpdateFocus), and the base state and the spline already stored when Fire arrives. Fire is not chained, so no world spline or
    // player is needed.
    public sealed class SplineSpawnRecordingNightmareProjectile : NightmareEggmanProjectile
    {
        public readonly List<Vector3> FocusTargets = new List<Vector3>();
        public readonly List<int> StatesAtFire = new List<int>();
        public readonly List<Spline> SplinesAtFire = new List<Spline>();

        public override void UpdateFocus(Vector3 target) => FocusTargets.Add(target);

        public override void Fire()
        {
            StatesAtFire.Add((int)m_state);
            // m_spline is private to the base class and set only through SetSpline, so reflection is the only way to observe it.
            SplinesAtFire.Add((Spline)typeof(NightmareEggmanProjectile)
                .GetField("m_spline", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(this));
        }
    }
}
