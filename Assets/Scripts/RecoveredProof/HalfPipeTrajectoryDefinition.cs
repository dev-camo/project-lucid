using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [CreateAssetMenu(fileName = "HalfPipeTrajectoryDefinition", menuName = "HardlightProject/DefinitionData/Definitions/HalfPipeTrajectoryDefinition")]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class HalfPipeTrajectoryDefinition : ScriptableObject
    {
        [SerializeField]
        [Tooltip("Minimum approach speed to use trajectory lookup.")]
        private float m_trajectorySpeedMin;

        [SerializeField]
        [Tooltip("Curve to determine trajectory from approach angle.")]
        private AnimationCurve m_trajectoryFromAngleCurve = AnimationCurve.Constant(0f, 360f, 360f);

        // Game.Runtime06001d26: direct original scalar load without validation.
        public float SpeedMin => m_trajectorySpeedMin;

        // Game.Runtime06001d27: original curve Evaluate receives the angle unchanged.
        public float TrajectoryFromAngle(float angle) => m_trajectoryFromAngleCurve.Evaluate(angle);

        // Game.Runtime06001d28: curve construction/store precedes the genuine base.
        // Minimum speed retains its zero-initialized field; no invented threshold.
        public HalfPipeTrajectoryDefinition() { }
    }
}
