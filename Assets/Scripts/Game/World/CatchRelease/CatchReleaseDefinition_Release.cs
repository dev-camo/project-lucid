using System;
using UnityEngine;
using UnityEngine.Serialization;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 0x020004ab; CalculateCachedValues 0x5402E0 (an empty body),
    // constructor 0x540600.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Serializable]
    public class CatchReleaseDefinition_Release
    {
        [Tooltip("Release transition for FSM logic.")]
        public CatchInputReleaseType InputType = CatchInputReleaseType.ToAir;
        [Tooltip("Points to snap transform to on catch release.")]
        [FormerlySerializedAs("Points")]
        public CatchReleasePoints TransformPoints;
        [Tooltip("Points to snap velocity to on catch release.  If none specified, TransformPoints are used.")]
        public CatchReleasePoints VelocityPoints;
        [Tooltip("If checked, the current real world speed will be used.")]
        public bool UseWorldSpeed;
        [Tooltip("X axis = scaled velocity magnitude \nY axis = release speed.")]
        public AnimationCurve SpeedCurve = AnimationCurve.Linear(0f, 0f, 100f, 100f);
        [Tooltip("X axis = attach time \nY axis = release speed multiplier.")]
        public AnimationCurve MultiplierCurve = AnimationCurve.Linear(0f, 1f, 10f, 1f);
        [Tooltip("Period of time to disable controls after release.")]
        public float ControlsDisabledTime;
        [Tooltip("Period of time to lock velocity after release.")]
        public float VelocityLockTime;
        [Min(0)]
        [Tooltip("Speed that attach point will travel after release and returning back to start.")]
        public float ReturnSpeed = 10f;
        [Tooltip("Optional leave checker logic.")]
        public ObjectLeaveChecker LeaveChecker;

        public void CalculateCachedValues()
        {
        }
    }
}
