using Cinemachine.Utility;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime type 0x02000a28. A smoothed proxy whose rotation child turns the
    // camera-relative heading into a local yaw, compensating for the Y axis proxy's own rotation.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class SmoothedTransformProxy_CameraWorldSpace : SmoothedTransformProxy
    {
        [SerializeField]
        private Transform m_forwardRotationChild;
        private readonly SystemRef<CameraYAxisProxyManager> m_cameraYAxisProxyManagerRef =
            ProcessManager.GetSystemRef<CameraYAxisProxyManager>(null, true);
        private CameraYAxisProxyManager m_cameraYAxisProxyManager;

        // 0x06003a2b; x86_64 0x6B4C70.
        protected override void Awake()
        {
            base.Awake();
            m_cameraYAxisProxyManagerRef.InvokeOnValid(manager => m_cameraYAxisProxyManager = manager);
        }

        // 0x06003a2c; x86_64 0x6B4D20. Unlike the base proxy this sets the position directly, with no
        // cached-transform flag or same-space avoidance offset, and requires the Y axis proxy manager.
        public override void UpdatePositionAndRotation(Vector3 targetPosition, Quaternion targetRotation,
            float worldForwardAngle, float deltaTime)
        {
            if (IsDestroyed) return;
            m_cachedTransform.position = targetPosition;
            // The native body inlines the rotation update: an overridden or disabled rotation ends the call here,
            // leaving the child untouched.
            if (m_rotationOverriden || m_disableRotationUpdate) return;
            SetRotation(GetTargetRotation(m_cachedTransform.rotation, targetRotation, deltaTime));
            if (m_disableRotationUpdate) return;

            Vector3 proxyForward = m_cameraYAxisProxyManager.TargetForward.ProjectOntoPlane(m_cachedTransform.up);
            float proxyAngle = UnityVectorExtensions.SignedAngle(proxyForward, m_cachedTransform.forward,
                m_cachedTransform.up);
            float forwardAngle = worldForwardAngle - proxyAngle -
                m_cameraYAxisProxyManager.SignedAngleDifferenceFromLastForward;
            m_forwardRotationChild.localRotation = Quaternion.AngleAxis(forwardAngle, Vector3.up);
        }

        // 0x06003a2d; x86_64 0x6B50E0. While rotation updates are disabled the child is reset as well.
        protected override void SetRotation(Quaternion rotation)
        {
            base.SetRotation(rotation);
            if (m_disableRotationUpdate) m_forwardRotationChild.localRotation = Quaternion.identity;
        }

        // 0x06003a2e; the manager reference is resolved before the base constructor.
        public SmoothedTransformProxy_CameraWorldSpace() { }
    }
}
