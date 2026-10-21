using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime type 0x02000256. Full source candidate; runtime acceptance pending.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class CameraAversionWithLimit : TimeScaledComponent_SDT
    {
        [SerializeField] private float m_orbitRadius = 2f;
        [SerializeField] private bool m_affectX;
        [SerializeField] private bool m_affectY;
        [SerializeField] private bool m_affectZ;
        private SystemRef<CinemachineCameraManager> m_cameraManagerRef;
        private Camera m_camera;
        private Vector3 m_relativeDirection;
        private Transform m_transform;
        private Vector3 m_originalLocal;

        // 0x06000cb4: remember the rest position in the parent's space.
        protected override void Awake()
        {
            base.Awake();
            m_cameraManagerRef = ProcessManager.GetSystemRef<CinemachineCameraManager>(null, true);
            m_transform = transform;
            m_originalLocal = m_transform.localPosition;
        }

        // 0x06000cb5: the output camera is only known once the manager exists.
        protected override void OnEnable()
        {
            base.OnEnable();
            m_cameraManagerRef.InvokeOnValid(cameraManager => m_camera = cameraManager.CinemachineBrain.OutputCamera);
        }

        // 0x06000cb6: push this object from its rest position along the camera's forward, per enabled axis.
        protected override void InternalUpdate(float deltaTime)
        {
            if (m_camera == null) return;
            m_relativeDirection = m_camera.transform.forward;
            Vector3 rest = m_transform.parent.TransformPoint(m_originalLocal);
            Vector3 orbit = rest + m_relativeDirection * m_orbitRadius;
            transform.position = new Vector3(
                m_affectX ? orbit.x : rest.x,
                m_affectY ? orbit.y : rest.y,
                m_affectZ ? orbit.z : rest.z);
        }
    }
}
