using System;
using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Serializable]
    public class VelocityRestoreParameters
    {
        // Original 0x04000d89; offset 0x10.
        [Tooltip("If stored velocity component is greater than the target then only restore if this is true.")]
        public bool RestoreIfGreater;
        // Original 0x04000d8a; offset 0x14.
        [Tooltip("The maximum blend time towards stored velocity components.")]
        public float BlendDurationSeconds;
        // Original 0x04000d8b; offset 0x18.
        [Tooltip("Blend back to stored X local velocity component.")]
        public bool BlendX;
        // Original 0x04000d8c; offset 0x19.
        [Tooltip("Blend back to stored Y local velocity component.")]
        public bool BlendY;
        // Original 0x04000d8d; offset 0x1a.
        [Tooltip("Blend back to stored Z local velocity component.")]
        public bool BlendZ;
        // Original 0x04000d8e; offset 0x1c.
        [Tooltip("If slope angle exceeds this, then buff is expired.")]
        public float MaximumSlopeAngle;
        // Original 0x04000d8f; offset 0x20.
        [Tooltip("Expire buff if character makes contact with this layer and the dot of the contact normal is above the threshold.")]
        public LayerMask ExpireOnContact;
        // Original 0x04000d90; offset 0x24.
        [Range(-1, 1)]
        [Tooltip("Used to check if contact normal is facing the same direction as the actor's forward (1 is facing exactly the same direction).")]
        public float ExpireOnContactDotThreshold;

        public VelocityRestoreParameters() { }
    }
}
