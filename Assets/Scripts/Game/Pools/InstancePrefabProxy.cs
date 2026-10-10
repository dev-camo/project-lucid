using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class InstancePrefabProxy : MonoBehaviour
    {
        private bool m_active;
        private bool m_hasInstance;
        private PooledPrefab m_instance;
        private IComponentPrefabPool m_prefabPool;

        // Original 06002a2e transforms the local bounds center only; its extents
        // remain authored. Pool assignment precedes all bounds/transform work.
        public void Populate(BoundsOctree<InstancePrefabProxy> octree, IComponentPrefabPool prefabPool)
        {
            m_prefabPool = prefabPool;
            Bounds bounds = m_prefabPool.CullBounds;
            bounds.center = transform.TransformPoint(bounds.center);
            octree.Add(this, bounds);
        }

        // Original 06002a2f reads hierarchy activity even for a false request.
        // Equal distance is culled: the comparison is strict on both architectures.
        public void SetActive(bool active, float distanceSqr = 0f)
        {
            m_active = gameObject.activeInHierarchy && active && m_prefabPool.Valid
                && m_prefabPool.CullDistanceSqr > distanceSqr;
        }

        // Original 06002a30 invokes SetActive(false) before immediately retiring it.
        private void OnDisable()
        {
            SetActive(false);
            UpdatePool();
        }

        // Original 06002a31 deliberately marks ownership before spawning. A failed
        // despawn preserves the old instance; both cleared fields follow success.
        public void UpdatePool()
        {
            if (m_active)
            {
                if (!m_hasInstance)
                {
                    m_hasInstance = true;
                    m_instance = m_prefabPool.SpawnInstance(transform);
                }
            }
            else if (m_hasInstance)
            {
                m_prefabPool.DespawnInstance(in m_instance);
                m_hasInstance = false;
                m_instance = null;
            }
        }

        // Original 06002a32 is the natural MonoBehaviour constructor.
    }
}
