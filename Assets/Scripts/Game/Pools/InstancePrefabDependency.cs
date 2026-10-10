using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0200077e; complete 06002a12..06002a1a.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class InstancePrefabDependency : PerformanceDependency
    {
        [SerializeField] private float m_cullDistance = 100f;
        [SerializeField] private Vector3 m_cullCentre;
        [SerializeField] private float m_cullRadius = 1f;
        public Bounds CullBounds { get; private set; }
        public float CullDistance => m_cullDistance;
        public float CullDistanceSqr { get; private set; }

        // 06002a17: base settings first, then the three authored culling fields.
        // The two calculated backing fields are not copied.
        public void Copy(InstancePrefabDependency otherDependency)
        {
            base.Copy(otherDependency);
            m_cullDistance = otherDependency.m_cullDistance;
            m_cullCentre = otherDependency.m_cullCentre;
            m_cullRadius = otherDependency.m_cullRadius;
        }

        // 06002a18: Bounds receives diameter, then the fresh distance is squared.
        public void GenerateCullBounds()
        {
            Vector3 cullCentre = m_cullCentre;
            float cullRadius = m_cullRadius;
            CullBounds = new Bounds(cullCentre, Vector3.one * (cullRadius * 2f));
            CullDistanceSqr = m_cullDistance * m_cullDistance;
        }

        // 06002a19: retain the original global gizmo colour write without restore.
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.TransformPoint(m_cullCentre), m_cullRadius);
        }

        // 06002a1a: authored distance/radius initializers precede the base ctor.
        public InstancePrefabDependency() { }
    }
}
