using HardlightProject;
using UnityEngine;

namespace ProjectLucid.Tests.CharCoreShared
{
    // Test-only Character whose gravity direction is supplied, because the shipped override reads
    // a RigidbodyGravity component that Edit Mode does not build. The visual proxy, which the
    // shipped Awake builds, can be assigned through the protected setter of the base class.
    public sealed class CharacterGravityProbe : Character
    {
        public Vector3 Down = Vector3.down;

        public override Vector3 GravityNormalised => Down;

        public void AssignVisualProxy(SmoothedTransformProxy proxy) => VisualProxy = proxy;
    }
}
