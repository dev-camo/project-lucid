using System;
using System.Collections.Generic;
using HardlightProject;

namespace ProjectLucid.Tests
{
    // Test-only concrete SurfaceMeshSplineGenerator. It records which of the
    // original virtual slots Awake dispatches (InitialiseSplineTrackers is slot 31,
    // the Unity Reset message slot 26) and runs a hook at the tracker slot so the
    // test can see what Awake had already done by then. It provides no game API.
    public sealed class SurfaceMeshSplineGeneratorAwakeProbe : SurfaceMeshSplineGenerator
    {
        public readonly List<string> Calls = new List<string>();
        public Action AtInitialiseSplineTrackers;

        protected override void InitialiseSplineTrackers()
        {
            Calls.Add(nameof(InitialiseSplineTrackers));
            AtInitialiseSplineTrackers?.Invoke();
        }

        protected override void ApplyTrackerToMesh() => Calls.Add(nameof(ApplyTrackerToMesh));

        protected override bool UpdateSamplePoints(bool forceGenerate)
        {
            Calls.Add(nameof(UpdateSamplePoints));
            return false;
        }

        // Records the dispatch only; the base editor message body is not part of this proof.
        protected override void Reset() => Calls.Add(nameof(Reset));

        // Control path: a direct virtual Reset dispatch must show up in Calls.
        public void InvokeReset() => Reset();
    }
}
