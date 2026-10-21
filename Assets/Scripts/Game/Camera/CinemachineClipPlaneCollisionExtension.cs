using System.Diagnostics;
using Cinemachine;
using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;
using Debug = UnityEngine.Debug;

namespace HardlightProject
{
    // Original Game.Runtime type 0x02000271.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class CinemachineClipPlaneCollisionExtension : CinemachineExtension
    {
        [SerializeField]
        private CinemachineCore.Stage m_applyAfterStage;
        [SerializeField]
        private LayerMask m_collisionMask;
        [SerializeField]
        [TagField]
        private string m_collisionIgnoreTag;
        [SerializeField]
        private bool m_showDebugRays;
        [SerializeField]
        [Tooltip("Only use when the character is the look at target. Will enable ghost overlay so the character can be seen behind ignored collision geometry.")]
        private bool m_toggleOcclusionRenderFeatures = true;
        [Tooltip("Multiplies the X and Y coordinates of the near clip plane for raycast origins.")]
        [SerializeField]
        private float m_nearClipPlaneScale = 1.3f;
        [SerializeField]
        private Vector3 m_lookAtOffset;
        private const float LookAtCornerZOffset = -0.2f;
        private readonly Vector3[] m_localFrustumPositions = new Vector3[5];
        private readonly SystemRef<CinemachineCameraManager> m_cameraManagerRef = ProcessManager.GetSystemRef<CinemachineCameraManager>();
        private float m_previousNearClipPlaneValue;
        private float m_previousFieldOfView;
        private bool m_isFreeLookCamera;

        // 0x06000db2
        protected override void OnEnable()
        {
            base.OnEnable();
            m_isFreeLookCamera = VirtualCamera is CinemachineFreeLook;
        }

        // 0x06000db3: casts one ray from each near-plane corner of the look-at target towards the camera
        // and pushes the camera in front of the nearest blocker. Colliders with the ignore tag are
        // skipped, but report to the occlusion render features. The retail build elides the
        // [Conditional] DebugDrawRay call sites.
        protected override void PostPipelineStageCallback(CinemachineVirtualCameraBase vcam,
            CinemachineCore.Stage stage, ref CameraState state, float deltaTime)
        {
            RaycastHit hit = default;
            if (stage != m_applyAfterStage || m_cameraManagerRef.IsNull()) return;
            if (m_isFreeLookCamera && VirtualCamera != vcam) return;
            CinemachineCameraManager cameraManager = m_cameraManagerRef.Get();
            CinemachineBrain brain = cameraManager.CinemachineBrain;
            if (!brain.IsLive(VirtualCamera, false)) return;
            if (vcam.LookAt == null) return;

            Transform lookAt = GetLookAtTransform();
            Camera camera = brain.OutputCamera;
            float nearClipPlane = camera.nearClipPlane;
            float fieldOfView = camera.fieldOfView;
            if (!(Mathf.Approximately(nearClipPlane, m_previousNearClipPlaneValue) &&
                  Mathf.Approximately(fieldOfView, m_previousFieldOfView)))
            {
                camera.CalculateFrustumCorners(new Rect(0f, 0f, 1f, 1f), nearClipPlane,
                    Camera.MonoOrStereoscopicEye.Mono, m_localFrustumPositions);
                m_localFrustumPositions[4] = new Vector3(0f, 0f, nearClipPlane);
            }
            m_previousNearClipPlaneValue = nearClipPlane;
            m_previousFieldOfView = fieldOfView;

            Matrix4x4 cameraMatrix = Matrix4x4.TRS(state.FinalPosition, state.FinalOrientation, Vector3.one);
            Vector3 lookAtPosition = lookAt.TransformPoint(m_lookAtOffset);
            Matrix4x4 lookAtMatrix = Matrix4x4.TRS(lookAtPosition, state.FinalOrientation, Vector3.one);

            Vector3 correction = Vector3.zero;
            float nearestDistance = float.MaxValue;
            bool hitAnything = false;
            bool hitIgnoredCollider = false;
            for (int i = 0; i < m_localFrustumPositions.Length; i++)
            {
                Vector3 corner = m_localFrustumPositions[i];
                Vector3 scaledCorner = new Vector3(corner.x * m_nearClipPlaneScale, corner.y * m_nearClipPlaneScale, corner.z);
                Vector3 origin = cameraMatrix.MultiplyPoint3x4(scaledCorner);
                Vector3 target = lookAtMatrix.MultiplyPoint3x4(new Vector3(scaledCorner.x, scaledCorner.y, LookAtCornerZOffset));
                Vector3 offset = origin - target;
                float distance = offset.magnitude;
                Vector3 direction = offset.normalized;
                if (!Physics.Raycast(new Ray(target, direction), out hit, distance, m_collisionMask.value,
                        QueryTriggerInteraction.Ignore))
                    continue;
                if (hit.collider.CompareTag(m_collisionIgnoreTag))
                {
                    hitIgnoredCollider = true;
                    continue;
                }
                hitAnything = true;
                if (nearestDistance >= hit.distance)
                {
                    nearestDistance = hit.distance;
                    correction = hit.point - origin;
                }
            }
            if (hitAnything) state.PositionCorrection += correction;
            if (m_toggleOcclusionRenderFeatures) cameraManager.SetOcclusionRenderFeaturesActive(hitIgnoredCollider);
        }

        // 0x06000db4
        protected virtual Transform GetLookAtTransform() => VirtualCamera.LookAt;

        // 0x06000db5
        [Conditional("BUILD_DEVELOPMENT")]
        private void DebugDrawRay(Vector3 origin, Vector3 direction, Color color)
        {
            if (m_showDebugRays) Debug.DrawRay(origin, direction, color);
        }

        // 0x06000db6
        public CinemachineClipPlaneCollisionExtension() { }
    }
}
