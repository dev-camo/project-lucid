using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace HardlightProject
{
    // Original Game.Runtime02000730: complete12 own fields/6 own methods.
    // Original compiler-option order is array checks first, then null checks.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class BlobShadowRaycaster : MonoBehaviour
    {
        [SerializeField] private Transform m_gravityProxy;
        [SerializeField] private LayerMask m_raycastLayerMask;
        [SerializeField] private DecalProjector m_projector;
        [Tooltip("If the dot between gravity and hit normal is greater than or equal to this value, then set this forward to the inverse of the normal.")]
        [SerializeField] private float m_normalDotThreshold;
        [SerializeField] private float m_updateInterval = 0.1f;
        [SerializeField] private Vector3 m_localPositionOnCharacterSwap;
        private Transform m_cachedTransform;
        private int m_layerMaskValue;
        private float m_timer;
        private RaycastHit[] m_raycastHits = new RaycastHit[1];
        private bool m_hasGravityProxy;
        private bool m_enforceOnEnable;

        // 06002883; ARM5a063c. Capture transform and mask before the Unity-null
        // check. Presence is cached here, so later proxy replacement does not
        // recompute the original branch choice.
        private void Awake()
        {
            m_cachedTransform = transform;
            m_layerMaskValue = m_raycastLayerMask.value;
            m_hasGravityProxy = m_gravityProxy != null;
        }
        // 06002884; ARM5a06e0. Ordered >= leaves NaN timers/intervals idle. Reset
        // follows the raycast/orientation work and does not preserve excess time.
        private void Update()
        {
            m_timer += Time.deltaTime;
            if (m_timer >= m_updateInterval)
            {
                UpdateForwardDirection();
                m_timer = 0f;
            }
        }
        // 06002885; ARM5a0730. Ray normalizes the cast direction while its origin
        // and surface-normal test use the original gravity vector. The actual
        // authored projector depth is size.z; triggers are explicitly ignored.
        private void UpdateForwardDirection()
        {
            Vector3 gravity = m_hasGravityProxy ? -m_gravityProxy.up : Vector3.down;
            Vector3 position = m_cachedTransform.position;
            Ray ray = new Ray(position - gravity, gravity);
            float distance = m_projector.size.z;
            RaycastHit[] hits = m_raycastHits;
            int layerMask = m_layerMaskValue;
            int count = Physics.RaycastNonAlloc(ray, hits, distance, layerMask, QueryTriggerInteraction.Ignore);
            if (count > 0)
            {
                RaycastHit hit = m_raycastHits[0];
                if (Vector3.Dot(-gravity, hit.normal) >= m_normalDotThreshold)
                {
                    Transform currentTransform = m_cachedTransform;
                    currentTransform.forward = -hit.normal;
                    return;
                }
            }
            // Preserve the engine's approximate Vector3 inequality; unordered
            // comparisons enter this fallback assignment rather than suppress it.
            if (m_cachedTransform.forward != gravity)
                m_cachedTransform.forward = gravity;
        }
        // 06002886; ARM5a0a2c. This schedules position enforcement for OnEnable;
        // it neither moves immediately nor clears after one activation.
        public void EnforceLocalPosition() => m_enforceOnEnable = true;
        // 06002887; ARM5a0a38. The flag is deliberately sticky across activations.
        private void OnEnable()
        {
            if (m_enforceOnEnable)
                m_cachedTransform.localPosition = m_localPositionOnCharacterSwap;
        }
        // 06002888; ARM5a0a5c. Initializers set interval then allocate a distinct
        // one-hit buffer before the genuine MonoBehaviour base constructor.
        public BlobShadowRaycaster() { }
    }
}
