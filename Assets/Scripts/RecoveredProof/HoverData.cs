using System;
using System.Collections.Generic;
using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Option.NullChecks, false)]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Serializable]
    public class HoverData
    {
        public UnityEngine.Transform Origin;
        [UnityEngine.Tooltip("How high from the pivot point the character will aim to get to.")]
        public float TargetHeight;
        [UnityEngine.Tooltip("Character will be allowed to move up and down this distance above and below the target height.")]
        public float OscillateHeightBounds;
        public float MoveSpeed;
        [UnityEngine.Tooltip("X Axis = Character height difference from target height (always positive)Y Axis = Multiplier to move speed.")]
        public UnityEngine.AnimationCurve DistanceSpeedCurve;
        [UnityEngine.Tooltip("Smooth damp time for how quickly the y velocity will move to target speed.")]
        public float SmoothTimeSeconds;
        // Original0600167d only delegates Object; all own values remain zero/null.
        public HoverData() { }
    }
}
