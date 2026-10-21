using System.Collections.Generic;
using HardlightProject;
using UnityEngine;

namespace ProjectLucid.Tests.CharCoreShared
{
    // Test-only EnemyNavigation: the start and interrupt calls are recorded in call order, the destination is
    // supplied, and IsInitialised can be set (its setter is protected in the original). SetUp, which the base runs
    // from OnValidate and Awake, is deliberately silent so the recorded calls are only the ones a case makes.
    public sealed class Enemies01NavigationProbe : EnemyNavigation
    {
        public Vector3 Destination;
        public readonly List<string> Calls = new List<string>();

        public void SetInitialised(bool initialised) => IsInitialised = initialised;

        protected override void SetUp() { }
        public override void StartNavigation() => Calls.Add("StartNavigation");
        public override void InterruptNavigation() => Calls.Add("InterruptNavigation");
        public override Vector3 GetDestination() => Destination;
        public override Vector3 ConstrainPosition(Vector3 position) => position;
        public override bool CanReachPosition(Vector3 position) => true;
    }
}
