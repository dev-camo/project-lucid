using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 0x02000abe; 0x6E3000..0x6E3870 (constructor 0x6E37E0).
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class CatchConnector : MonoBehaviour, ITimeScaled
    {
        [Tooltip("Time category to update from.")]
        [SerializeField]
        private TimeCategory m_timeCategory = TimeCategory.PlayerPhysics;
        [SerializeField]
        [Tooltip("If set, this is used to scale connector to as the pivot.  If not set, the attach point is used.")]
        private Transform m_anchor;
        [Tooltip("Offset from the character when attached.")]
        [SerializeField]
        private float m_offset;
        [Min(0)]
        [Tooltip("Time to extend to pivot.")]
        [SerializeField]
        private float m_timeToExtend;
        private bool m_alwaysActive;
        private Vector3 m_scale;
        private Quaternion m_startRotation;
        private Transform m_anchorTransform;
        private Rigidbody m_anchorBody;
        private Transform m_catchTransform;
        private Vector3 m_catchOffset;
        private float m_time;
        private readonly SystemRef<TimeManager> m_timeManagerRef = ProcessManager.GetSystemRef<TimeManager>(null, true);

        // SetPivots activates a connector that is inactive at that moment and marks it as not always
        // active, so ClearPivots later deactivates only those connectors.
        private void Awake()
        {
            m_alwaysActive = true;
            m_scale = transform.localScale;
            m_startRotation = transform.rotation;
        }

        private void OnEnable()
        {
            m_timeManagerRef.InvokeOnValid(timeManager => timeManager.Subscribe(this, m_timeCategory, UpdateOn.LateUpdate));
        }

        private void OnDisable()
        {
            if (m_timeManagerRef == null || m_timeManagerRef.IsNull())
                return;
            m_timeManagerRef.Get().Unsubscribe(this, m_timeCategory, UpdateOn.LateUpdate);
        }

        // An authored anchor wins over the supplied one.
        public void SetPivots(Transform catchTransform, Transform anchorTransform, Vector3 catchOffset)
        {
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
                m_alwaysActive = false;
            }
            m_catchTransform = catchTransform;
            m_catchOffset = catchOffset;
            m_anchorTransform = m_anchor != null ? m_anchor : anchorTransform;
            m_anchorBody = m_anchorTransform.GetComponent<Rigidbody>();
            m_time = 0f;
        }

        public void ClearPivots()
        {
            if (m_alwaysActive)
                return;
            m_catchTransform = null;
            m_anchorTransform = null;
            m_anchorBody = null;
            gameObject.SetActive(false);
        }

        // The connector stretches along its Y axis from the anchor towards the catch
        // point (extending over m_timeToExtend seconds) and is placed past it by m_offset.
        public void OnLateUpdate(float deltaTime)
        {
            if (m_alwaysActive && m_anchorTransform == null)
                return;
            Transform connector = transform;
            Transform catchTransform = m_catchTransform != null ? m_catchTransform : connector;
            Vector3 anchorPosition = m_anchorBody != null ? m_anchorBody.position : m_anchorTransform.position;
            Quaternion anchorRotation = m_anchorBody != null ? m_anchorBody.rotation : m_anchorTransform.rotation;
            Vector3 catchPosition = catchTransform.position;
            Vector3 position = anchorRotation * m_catchOffset + catchPosition;
            if (m_timeToExtend > m_time)
            {
                position = Vector3.LerpUnclamped(anchorPosition, position, m_time / m_timeToExtend);
                m_time = Mathf.Min(deltaTime + m_time, m_timeToExtend);
            }
            Vector3 toAnchor = anchorPosition - position;
            float distance = toAnchor.magnitude;
            Vector3 direction = toAnchor.normalized;
            position += direction * m_offset;
            Quaternion rotation = m_anchor != null ? m_startRotation : Quaternion.LookRotation(direction) * m_startRotation;
            connector.SetPositionAndRotation(position, rotation);
            m_scale.y = distance + m_offset;
            connector.localScale = m_scale;
        }

        public bool IsPaused { get; set; }
    }
}
