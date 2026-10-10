using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0200077b; complete 06002a02..06002a0a.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class ComponentPrefabPool<T> : PrefabPool<T>, IComponentPrefabPool where T : Component
    {
        [SerializeField] private InstancePrefabDependency m_prefabDependency;
        [SerializeField] private InstancePrefabDependency m_poolDependency;
        public bool Valid { get; private set; }
        public Bounds CullBounds => m_poolDependency.CullBounds;
        public float CullDistance => m_poolDependency.CullDistance;
        public float CullDistanceSqr => m_poolDependency.CullDistanceSqr;

        // 06002a07: copy, generate, append validation, refresh. Duplicate appends
        // and fresh field reads are original; no prior callback is removed here.
        public void RefreshDependency(bool addValidation)
        {
            m_poolDependency.Copy(m_prefabDependency);
            m_poolDependency.GenerateCullBounds();
            if (addValidation)
            {
                m_poolDependency.ValidateDependencyCallback += OnValidateDependency;
                m_poolDependency.ScalablePerformanceRefresh();
            }
        }

        // 06002a08.
        private void OnValidateDependency(bool valid) => Valid = valid;

        // 06002a09: an invalid dependency returns null before the base pool runs.
        public override PooledPrefab SpawnInstance(Transform root) =>
            Valid ? base.SpawnInstance(root) : null;

        // 06002a0a: both dependency references retain their original null defaults.
        public ComponentPrefabPool() { }
    }
}
