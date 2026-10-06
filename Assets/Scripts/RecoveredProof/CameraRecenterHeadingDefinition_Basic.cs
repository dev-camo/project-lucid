using Hardlight;
using Cinemachine;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption((Option)1, false)]
    [Il2CppSetOption((Option)2, false)]
    [CreateAssetMenu(fileName = "CameraRecenterHeadingDefinition_Basic", menuName = "HardlightProject/DefinitionData/Definitions/CameraRecenterHeadingDefinition_Basic")]
    public class CameraRecenterHeadingDefinition_Basic : CameraRecenterHeadingDefinition
    {
        [Tooltip("X axis = Normalized angle from character forward to camera forward.\nY axis = Multiplier to rotation speed.")]
        [SerializeField]
        private AnimationCurve m_angleToSpeedMultiplierCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 2f);
        [SerializeField]
        private float m_rotationSpeed = 150f;

        // Original06001a71 and06001a72 return the actual authored references/scalar.
        public AnimationCurve AngleToSpeedMultiplierCurve => m_angleToSpeedMultiplierCurve;
        public float RotationSpeed => m_rotationSpeed;

        // Original06001a73 constructs EaseInOut(0,1,1,2), then150, then the genuine
        // inherited timeout3 initializer before ScriptableObject construction.
        public CameraRecenterHeadingDefinition_Basic() { }
    }
}
