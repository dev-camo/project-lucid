using System;
using System.Collections.Generic;
using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Serializable]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class HashedComponentGroup : HashedGroup
    {
        [InspectorReadOnly] [SerializeField] private HashedComponent[] m_hashedComponents = Array.Empty<HashedComponent>();
        [SerializeField] [InspectorReadOnly] private MeshRenderer[] m_meshRenderers = Array.Empty<MeshRenderer>();
        [SerializeField] [InspectorReadOnly] private SkinnedMeshRenderer[] m_skinnedMeshRenderers = Array.Empty<SkinnedMeshRenderer>();
        [InspectorReadOnly] [SerializeField] private LineRenderer[] m_lineRenderers = Array.Empty<LineRenderer>();
        [SerializeField] [InspectorReadOnly] private Collider[] m_colliders = Array.Empty<Collider>();
        [SerializeField] [InspectorReadOnly] private ParticleSystem[] m_particleSystems = Array.Empty<ParticleSystem>();
        [InspectorReadOnly] [SerializeField] private ParticleSystem[] m_particleSystemsWithWrappers = Array.Empty<ParticleSystem>();
        [SerializeField] [InspectorReadOnly] private ParticleEffectWrapper[] m_effectWrappers = Array.Empty<ParticleEffectWrapper>();
        [InspectorReadOnly] [SerializeField] private int m_componentsHashCode;
        [InspectorReadOnly] [SerializeField] private int m_renderersHashCode;
        [InspectorReadOnly] [SerializeField] private int m_skinnedMeshRenderersHashCode;
        [SerializeField] [InspectorReadOnly] private int m_lineRenderersHashCode;
        [InspectorReadOnly] [SerializeField] private int m_collidersHashCode;
        [InspectorReadOnly] [SerializeField] private int m_particleSystemHashCode;
        [InspectorReadOnly] [SerializeField] private int m_particleEffectWrappersHashCode;
        private Dictionary<ParticleSystem, ParticleEffectWrapper> m_particleSystemWrappers =
            new Dictionary<ParticleSystem, ParticleEffectWrapper>();

        public void SetPrerenderActive(bool active)
        {
            ToggleComponents(m_hashedComponents, active);
            ToggleRenderers(m_meshRenderers, active);
            ToggleRenderers(m_skinnedMeshRenderers, active);
            ToggleRenderers(m_lineRenderers, active);
            ToggleColliders(m_colliders, active);
        }

        public void SetActive(bool active)
        {
            ToggleComponents(m_hashedComponents, active);
            ToggleRenderers(m_meshRenderers, active);
            ToggleRenderers(m_skinnedMeshRenderers, active);
            ToggleRenderers(m_lineRenderers, active);
            ToggleColliders(m_colliders, active);
            foreach (ParticleSystem particleSystem in m_particleSystems)
            {
                if (particleSystem == null)
                    continue;
                bool hasWrapper = m_particleSystemWrappers.TryGetValue(particleSystem, out ParticleEffectWrapper wrapper);
                if (active)
                {
                    if (hasWrapper)
                        wrapper.StartEffect();
                    else
                        particleSystem.Play(true);
                }
                else
                {
                    if (hasWrapper)
                        wrapper.StopEffect(ParticleSystemStopBehavior.StopEmittingAndClear, false);
                    else
                        particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }
        }

        private void ToggleComponents(IEnumerable<HashedComponent> components, bool active)
        {
            foreach (HashedComponent component in components)
                if (component != null)
                    component.enabled = active;
        }

        private void ToggleRenderers(IEnumerable<Renderer> renderers, bool active)
        {
            foreach (Renderer renderer in renderers)
                if (renderer != null)
                    renderer.enabled = active;
        }

        private void ToggleColliders(IEnumerable<Collider> colliders, bool active)
        {
            foreach (Collider collider in colliders)
            {
                if (collider == null)
                    continue;
                collider.enabled = active;
                if (active && collider.TryGetComponent(out CharacterCollisionData data))
                    collider.hasModifiableContacts = data.HasModifiableContacts;
            }
        }

        // Original unchecked parallel authored arrays are consumed in their original order.
        // Existing mappings are cleared before any array access; duplicate keys overwrite.
        private void Awake()
        {
            m_particleSystemWrappers.Clear();
            for (int index = 0; index < m_particleSystemsWithWrappers.Length; index++)
                m_particleSystemWrappers[m_particleSystemsWithWrappers[index]] = m_effectWrappers[index];
        }

        public HashedComponentGroup() { }
    }
}
