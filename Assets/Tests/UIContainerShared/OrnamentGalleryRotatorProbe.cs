using HardlightProject;
using UnityEngine;

namespace ProjectLucid.Tests.UIContainerShared
{
    // Test-only rotator that counts the rotations applied through the protected SetRotation hook.
    // Editor-only assemblies cannot supply attachable components.
    public sealed class OrnamentGalleryRotatorProbe : SmoothedTransformRotator
    {
        public int SetRotationCalls;

        protected override void SetRotation(Quaternion rotation)
        {
            SetRotationCalls++;
            base.SetRotation(rotation);
        }
    }
}
