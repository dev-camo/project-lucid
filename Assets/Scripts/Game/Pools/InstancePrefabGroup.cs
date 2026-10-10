using System.Collections.Generic;
using System.Text;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class InstancePrefabGroup : TimeScaledComponent_SDT
    {
        [SerializeField] private HashedTrackGroup m_trackGroup;
        [SerializeField] private List<InstancePrefabPool> m_pools = new List<InstancePrefabPool>();
        private readonly Plane[] m_frustumPlanes = new Plane[6];
        private float m_cullDistance;
        private readonly BoundsOctree<InstancePrefabProxy> m_octreeProxies =
            new BoundsOctree<InstancePrefabProxy>(100f, Vector3.zero, 10f, 2f);
        private readonly List<InstancePrefabProxy> m_activeProxies = new List<InstancePrefabProxy>();
        private readonly SystemRef<CinemachineCameraManager> m_cinemachineCameraManagerRef =
            ProcessManager.GetSystemRef<CinemachineCameraManager>(null, true);

        // Original 06002a1b: base startup precedes each pool's octree population.
        protected override void Awake()
        {
            base.Awake();
            foreach (InstancePrefabPool pool in m_pools)
                pool.Instantiate(m_octreeProxies, ref m_cullDistance);
        }

        // Original 06002a1c clears old visibility before selecting the next set.
        // Its natural cached callback and foreach disposal arise from these constructs.
        protected override void InternalUpdate(float deltaTime)
        {
            if (!m_cinemachineCameraManagerRef.TryGet(out CinemachineCameraManager manager)) return;
            m_activeProxies.ForEach(proxy => proxy.SetActive(false));
            Camera camera = manager.MainCamera;
            Vector3 cameraPosition = manager.CinemachineBrain.transform.position;
            GeometryUtility.CalculateFrustumPlanes(camera, m_frustumPlanes);
            m_frustumPlanes[5].distance += m_cullDistance - camera.farClipPlane;
            m_activeProxies.Clear();
            m_octreeProxies.GetWithinFrustum(m_frustumPlanes, m_activeProxies);
            foreach (InstancePrefabProxy proxy in m_activeProxies)
            {
                if (proxy.enabled)
                    proxy.SetActive(true, (proxy.transform.position - cameraPosition).sqrMagnitude);
            }
            foreach (InstancePrefabPool pool in m_pools) pool.UpdateProxies();
        }

        // Original 06002a1d examines the immediate parent only, preserving the
        // second parent access after the original Unity-object null check.
        protected override void OnValidate()
        {
            base.OnValidate();
            if (m_trackGroup != null) return;
            if (transform.parent == null) return;
            if (transform.parent.TryGetComponent(out HashedTrackGroup trackGroup))
                m_trackGroup = trackGroup;
        }

        // Original 06002a1e captures this parameter in the original display class.
        public void GetInstancePrefabPoolsInfo(StringBuilder stringInfoBuilder)
        {
            m_pools.ForEach(pool => pool.GetInfo(stringInfoBuilder));
        }

        // Original 06002a1f and natural 06002a20..24 arise from the field initializers
        // and two lambdas above, retaining their original owner/field identities.
    }
}
