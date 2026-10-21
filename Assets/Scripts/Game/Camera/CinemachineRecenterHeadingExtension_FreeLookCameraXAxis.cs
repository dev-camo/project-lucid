using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime type 0x0200028b.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class CinemachineRecenterHeadingExtension_FreeLookCameraXAxis : CinemachineRecenterHeadingExtension_FreeLookBase
    {
        // 0x06000e1b: drives the free-look camera's own X-axis recentering.
        protected override void OnUpdate(Character character, float deltaTime)
        {
            GetTargetDirections(character, out Vector3 targetForward, out Vector3 targetUp);
            float forwardAngle = GetCameraForwardFromTargetAngle(targetForward, targetUp);
            EvaluateRecentering(character, ref m_freeLookCamera.m_RecenterToTargetHeading, deltaTime, forwardAngle);
        }

        // 0x06000e1c
        public override void SnapToCharacter()
        {
            base.SnapToCharacter();
            RecenterNow(ref m_freeLookCamera.m_RecenterToTargetHeading);
        }

        // 0x06000e1d
        public CinemachineRecenterHeadingExtension_FreeLookCameraXAxis() { }
    }
}
