using Cinemachine;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime type 0x02000290. Shifts the camera by the follow target's local-space
    // offset, expressed as a world displacement, once the configured pipeline stage has run.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class CinemachineRelativeOffsetExtension : CinemachineExtension
    {
        [SerializeField] private CinemachineCore.Stage m_applyAfterStage;
        [SerializeField] private Vector3 m_followRelativeOffset;

        // 0x06000e32; x86_64 0x723FE0. The displacement accumulates in the position correction.
        protected override void PostPipelineStageCallback(CinemachineVirtualCameraBase vcam,
            CinemachineCore.Stage stage, ref CameraState state, float deltaTime)
        {
            if (stage != m_applyAfterStage) return;
            Transform follow = vcam.Follow;
            if (follow == null) return;
            state.PositionCorrection += follow.TransformPoint(m_followRelativeOffset) - follow.position;
        }

        // 0x06000e33
        public CinemachineRelativeOffsetExtension() { }
    }
}
