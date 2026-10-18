using HardlightProject;
using UnityEngine;

namespace ProjectLucid.Tests.CharCoreShared
{
    // Test-only Character whose gravity vector is supplied, because the shipped overrides read a
    // RigidbodyGravity component that Edit Mode does not build. The normalised direction is derived
    // from the same vector.
    public sealed class CharacterGravityVectorProbe : Character
    {
        public Vector3 GravityVector = new Vector3(0f, -9.81f, 0f);

        public override Vector3 Gravity => GravityVector;

        public override Vector3 GravityNormalised => GravityVector.normalized;
    }
}
