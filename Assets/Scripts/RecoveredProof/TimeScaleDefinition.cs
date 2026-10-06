using System.Collections.Generic;
using UnityEngine;

namespace HardlightProject
{
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
    [UnityEngine.CreateAssetMenu(fileName = "TimeScaleDefinition", menuName = "HardlightProject/DefinitionData/Definitions/TimeScaleDefinition")]
    public class TimeScaleDefinition : UnityEngine.ScriptableObject
    {
        [UnityEngine.Tooltip("The time category that will be scaled.")]
        public HardlightProject.TimeCategory TimeCategory;

        [UnityEngine.Tooltip("Anim curve to evaluate time scale over unscaled time.")]
        public UnityEngine.AnimationCurve TimeScale;

        // Original Game.Runtime 0x06001f72, ARM 0x53234c.
        // The compiler emits the original public, parameterless base-only constructor.
    }
}
