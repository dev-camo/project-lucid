using Cinemachine;
using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Complete original fieldless Game.Runtime owner 0x020009e9, seven APIs.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public static class CameraUtilities
    {
        // 0x060038f4; ARM64 0x68280c, x86_64 0x6a7090. The original
        // GetValue stores a null default; re-read Storage after manager calls.
        public static void SetCharacterCameraProxyTargetSettings(Character character,
            CameraProxyTargetSettings settings)
        {
            StackableDataHandle handle = character.Storage.GetValue<StackableDataHandle>(
                ActorFSMKeys.CameraProxySettingHandle, null, true);
            ProcessManager.GetSystem<CinemachineCameraManager>(null, true)
                .SetCameraProxySettings(settings, ref handle);
            character.Storage.SetValue<StackableDataHandle>(ActorFSMKeys.CameraProxySettingHandle, handle);
        }

        // 0x060038f5; ARM64 0x682a48, x86_64 0x6a72e0. Both shipped
        // generic-method globals identify this mismatched removal type.
        public static void ClearCharacterCameraProxyTargetSettings(Character character)
        {
            StackableDataHandle handle = character.Storage.GetValue<StackableDataHandle>(
                ActorFSMKeys.CameraProxySettingHandle, null, true);
            if (handle == null) return;
            ProcessManager.GetSystem<CinemachineCameraManager>(null, true)
                .RemoveCameraProxySettingOverride(handle);
            character.Storage.RemoveValue<CameraProxyTargetSettings>(ActorFSMKeys.CameraProxySettingHandle);
        }

        // 0x060038f6; ARM64 0x682c70, x86_64 0x6a7520. CLR subtype
        // tests precede the transposer's Unity object predicate. GetRig(1)
        // has no added null guard; unrelated cameras return the saved zero.
        public static Vector3 TryGetFollowOffset(CinemachineVirtualCameraBase virtualCameraBase)
        {
            Vector3 offset = Vector3.zero;
            CinemachineTransposer transposer = null;
            if (virtualCameraBase is CinemachineVirtualCamera camera)
                transposer = camera.GetCinemachineComponent<CinemachineTransposer>();
            else if (virtualCameraBase is CinemachineFreeLook freeLook)
                transposer = freeLook.GetRig(1).GetCinemachineComponent<CinemachineTransposer>();
            if (transposer != null) offset = transposer.EffectiveOffset;
            return offset;
        }

        // 0x060038f7; ARM64 0x682e54, x86_64 0x6a76a0. Retain the
        // original fmodf remainder and native addition grouping before clamp.
        public static float ClampAxisStateValue(float minValue, float maxValue, float value, bool wrap)
        {
            float range = maxValue - minValue;
            if (range > 0.0001f && wrap)
            {
                float remainder = (value - minValue) % range;
                value = remainder + (minValue + (remainder < 0f ? range : 0f));
            }
            return Mathf.Clamp(value, minValue, maxValue);
        }

        // 0x060038f8; ARM64 0x682ec8, x86_64 0x6a7720. The original
        // projection flips both viewport axes and returns negative clip W.
        public static Vector3 WorldToViewportPoint(Matrix4x4 worldToProjectionMatrix, Vector3 position)
        {
            Vector4 projected = worldToProjectionMatrix * new Vector4(position.x, position.y, position.z, 1f);
            float scale = 1f / (projected.w + projected.w);
            return new Vector3(0.5f - projected.x * scale, 0.5f - projected.y * scale, -projected.w);
        }

        // 0x060038f9; ARM64 0x682f2c, x86_64 0x6a77a0. The original
        // optional Vector2 default is zero, and each screen boundary is strict.
        public static bool WorldPositionIsOnScreen(Matrix4x4 worldToProjectionMatrix,
            Vector3 worldPosition, Vector2 borders = default(Vector2))
        {
            Vector3 viewport = WorldToViewportPoint(worldToProjectionMatrix, worldPosition);
            return viewport.x > borders.x && viewport.x < 1f - borders.x
                && viewport.y > borders.y && viewport.y < 1f - borders.y && viewport.z > 0f;
        }

        // 0x060038fa; ARM64 0x682fdc, x86_64 0x6a7870. Read projection
        // first, then the component transform's actual world-to-local matrix.
        public static Matrix4x4 WorldToProjectionMatrix(Camera camera)
        {
            Matrix4x4 projection = camera.projectionMatrix;
            return projection * camera.transform.worldToLocalMatrix;
        }
    }
}
