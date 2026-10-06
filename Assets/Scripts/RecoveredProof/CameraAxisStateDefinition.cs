using Hardlight;
using Cinemachine;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption((Option)1, false)]
    [Il2CppSetOption((Option)2, false)]
    [CreateAssetMenu(fileName = "CameraAxisStateDefinition", menuName = "HardlightProject/DefinitionData/Definitions/CameraAxisStateDefinition")]
    public class CameraAxisStateDefinition : ScriptableObject
    {
        [Tooltip("How to interpret the Max Speed setting, or as a direct input value multiplier.")]
        [SerializeField]
        private AxisState.SpeedMode m_speedMode;
        [Tooltip("The maximum speed of this axis, or the input value multiplier, depending on the speed mode.")]
        [SerializeField]
        private float m_maxSpeed;
        [Tooltip("The amount of time it takes to accelerate to MaxSpeed with the supplied Axis at its maximum value.")]
        [SerializeField]
        private float m_accelTime;
        [Tooltip("The amount of time it takes to decelerate the axis to zero if the supplied axis is in a neutral position")]
        [SerializeField]
        private float m_decelTime;

        // Original06001a69 stores only these four axis parameters in this order.
        // The incoming axis value, limits, inversion and input state are retained.
        public void Set(ref AxisState axisState)
        {
            axisState.m_SpeedMode = m_speedMode;
            axisState.m_MaxSpeed = m_maxSpeed;
            axisState.m_AccelTime = m_accelTime;
            axisState.m_DecelTime = m_decelTime;
        }

        // Original06001a6a is genuinely base-only; all four own fields start zero.
        public CameraAxisStateDefinition() { }
    }
}
