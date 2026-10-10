using System.Collections.Generic;
using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Complete original Game.Runtime 0200067c, eight methods. Native unchecked
    // bounds/null operations and exceptional floating-point scheduling remain
    // separate from common valid-input source behavior.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class VariableLengthLasers : MonoBehaviour
    {
        private const float LaserLengthInPfx = 15f;
        [SerializeField] private List<LineRenderer> m_lineRenderers = new List<LineRenderer>();
        [SerializeField] private List<SmoothedTransformProxy> m_smoothedTransformProxies = new List<SmoothedTransformProxy>();
        [SerializeField] private List<ParticleSystem> m_midPositionedPfx = new List<ParticleSystem>();
        [SerializeField] private Transform m_laserPfxParent;
        [SerializeField] private Transform m_nearEdgePfx;
        [SerializeField] private Transform m_farEdgePfx;

        // Original 06002319: register the genuine empty callback first, then
        // enumerate the current proxy list. Foreach retains finally disposal.
        private void Start()
        {
            ProcessManager.GetSystemRef<DataManager>(null, true).InvokeOnValid(Initialise);
            foreach (SmoothedTransformProxy proxy in m_smoothedTransformProxies)
                proxy.Detach(null);
        }

        // Original 0600231a is an empty return on both architectures.
        private void Initialise(DataManager dataManager) { }

        // Original 0600231b: own activation callbacks precede the proxy-list read.
        public void EnableLasers(bool enable)
        {
            gameObject.SetActive(enable);
            foreach (SmoothedTransformProxy proxy in m_smoothedTransformProxies)
                proxy.gameObject.SetActive(enable);
        }

        // Original 0600231c: one two-position buffer is reused for all renderers;
        // GetPositions' count is discarded. Preserve Y while replacing X/Z,
        // and reread the live list and Count after every SetPositions callback.
        public void SetLaserPositions(Vector3[] calculatedAbilityPositions)
        {
            Vector3[] positions = new Vector3[2];
            for (int i = 0; i < m_lineRenderers.Count; ++i)
            {
                LineRenderer lineRenderer = m_lineRenderers[i];
                lineRenderer.GetPositions(positions);
                for (int j = 0; j < positions.Length; ++j)
                {
                    positions[j].x = calculatedAbilityPositions[j].x;
                    positions[j].z = calculatedAbilityPositions[j].z;
                }
                lineRenderer.SetPositions(positions);
            }
        }

        // Original 0600231d: direct local-position assignment, no guard.
        public void SetPfxParentPosition(Vector3 localPosition)
        {
            m_laserPfxParent.localPosition = localPosition;
        }

        // Original 0600231e: gameObject then transform getters remain distinct;
        // each scale preserves Y/Z. Both native compilers hoist radius/15 after
        // GetEnumerator; exact exceptional FP scheduling is not claimed here.
        public void SetMidPositionedEmissionScale(float radius)
        {
            foreach (ParticleSystem particleSystem in m_midPositionedPfx)
            {
                Transform particleTransform = particleSystem.gameObject.transform;
                Vector3 scale = particleTransform.localScale;
                scale.x = radius / LaserLengthInPfx;
                particleTransform.localScale = scale;
            }
        }

        // Original 0600231f: near setter precedes the fresh far-field read.
        public void SetStartAndEndPfx(Vector3 nearEmitter, Vector3 farEmitter)
        {
            m_nearEdgePfx.localPosition = nearEmitter;
            m_farEdgePfx.localPosition = farEmitter;
        }

        // Original 06002320: implicit constructor creates the three lists in
        // declaration order before invoking MonoBehaviour's constructor.
    }
}
