// Complete original type candidate; source/runtime/serialization acceptance pending.
using System;
using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Serializable]
    [Il2CppSetOption((Unity.IL2CPP.CompilerServices.Option)2, false)]
    [Il2CppSetOption((Unity.IL2CPP.CompilerServices.Option)1, false)]
    public class CameraHeadingOverride
    {
        public Transform Target;
        public CameraHeadingOverride.CameraHeadingOverrideSettings Settings;
        public void UpdateCachedValues() => Settings.UpdateCachedValues();
        public CameraHeadingOverride() { }

        [Serializable]
        [Il2CppSetOption((Unity.IL2CPP.CompilerServices.Option)2, false)]
        [Il2CppSetOption((Unity.IL2CPP.CompilerServices.Option)1, false)]
        public class CameraHeadingOverrideSettings
        {
            [Tooltip("If enabled, camera X axis will orientate to face the forward of target transform.")]
            public bool UseTargetAsXAxisOverride = true;
            [ShowIf("UseTargetAsXAxisOverride", (string)null)]
            public bool OverrideXAxisRecenteringTime;
            [ShowIf("OverrideXAxisRecenteringTime", (string)null)]
            public float XAxisRecenteringTime = 0.5f;
            public bool OverrideYAxisValue;
            [Range(0f, 1f)]
            [ShowIf("OverrideYAxisValue", (string)null)]
            public float YAxisValue = 0.5f;
            [ShowIf("OverrideYAxisValue", (string)null)]
            public float YAxisSpeed = 0.5f;
            [Tooltip("If player makes a camera input, wait this number of seconds before recentering again.")]
            public float ResetAfterCameraInputTimeSeconds;
            [Tooltip("If true, then after camera input is made the camera will recenter behind the target as it normally would.")]
            [HideIf("ResetAfterCameraInputTimeSeconds", 0f)]
            public bool RecenterToCameraTargetOnCameraInput;
            [Tooltip("Enable to ignore this heading override based on the angle between heading forward and the incoming direction.")]
            public bool RequireMinimumAngle;
            [Tooltip("Heading override will be ignored in selection if the angle between the heading forward and incoming direction is greater than or equal to this angle.Note: heading trigger selection method determines what the incoming direction is.")]
            [Range(0f, 180f)]
            [ShowIf("RequireMinimumAngle", (string)null)]
            public float MaximumAngleThreshold;
            [Tooltip("If set, targeting homing will always project from the camera whilst heading override is triggered.")]
            public bool OverrideTargetingHoming;
            public float MaximumAngleThresholdCosine { get; private set; }
            public void UpdateCachedValues() => MaximumAngleThresholdCosine =
                Mathf.Cos(MaximumAngleThreshold * Mathf.Deg2Rad);
            public CameraHeadingOverrideSettings() { }
        }
    }
}
