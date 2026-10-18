using System.Collections.Generic;
using HardlightProject;
using UnityEngine;

namespace ProjectLucid.Tests.CharCoreShared
{
    // Test-only concrete ViewTarget (an abstract MonoBehaviour), recording what the shipped
    // base class asks of its abstract members. Editor-only assemblies cannot supply attachable
    // components, so it lives in this runtime test assembly.
    public sealed class ViewTargetProbe : ViewTarget
    {
        public bool ScreenSpace;
        public bool InRange = true;
        public Vector3 StoredPosition;
        public Vector3 StoredScale = Vector3.one;
        public Color StoredColour = Color.white;
        public readonly List<bool> Shown = new List<bool>();
        public readonly List<Vector3> RangeQueries = new List<Vector3>();

        protected override Vector3 TargetPosition { get => StoredPosition; set => StoredPosition = value; }
        protected override Vector3 TargetScale { get => StoredScale; set => StoredScale = value; }
        protected override Color TargetColour { get => StoredColour; set => StoredColour = value; }
        protected override bool ProjectToScreenSpace => ScreenSpace;

        protected override bool TargetInRange(Vector3 targetScreenPosition)
        {
            RangeQueries.Add(targetScreenPosition);
            return InRange;
        }

        protected override void ShowTarget(bool isActive) => Shown.Add(isActive);

        // The members of the shipped base class a case drives directly.
        public void RunAwake() => Awake();
        public void RunLateUpdate() => LateUpdate();
        public void RunRefresh() => Refresh();
        public float MinDistance => MinRenderDistance;
        public float MaxDistance => MaxRenderDistance;
        public bool DefaultTargetInRange(Vector3 position) => base.TargetInRange(position);
    }
}
