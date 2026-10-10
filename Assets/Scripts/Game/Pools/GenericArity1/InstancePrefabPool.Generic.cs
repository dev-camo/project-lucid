using System;
using System.Collections.Generic;
using System.Text;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000783; it is abstract despite its four concrete APIs.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public abstract class InstancePrefabPool<T> : InstancePrefabPool where T : Component
    {
        // Original nested 02000784/06002a2d: both fields default null, no list init.
        [Serializable]
        public class InstancePrefabPoolProxies
        {
            public ComponentPrefabPool<T> m_pool;
            public List<InstancePrefabProxy> m_proxies;
            public InstancePrefabPoolProxies() { }
        }

        [SerializeField]
        [Tooltip("Game objects designated to each prefab pool.")]
        private SerializableDictionary<T, InstancePrefabPoolProxies> m_poolProxies =
            new SerializableDictionary<T, InstancePrefabPoolProxies>();

        // 06002a29: validate each pool before max reduction and proxy population.
        // The prior distance is Max's second operand, including its NaN behavior.
        public override void Instantiate(BoundsOctree<InstancePrefabProxy> octree, ref float cullDistance)
        {
            foreach (var pair in m_poolProxies)
            {
                InstancePrefabPoolProxies poolProxies = pair.Value;
                ComponentPrefabPool<T> pool = poolProxies.m_pool;
                pool.RefreshDependency(true);
                cullDistance = Mathf.Max(pool.CullDistance, cullDistance);
                foreach (InstancePrefabProxy proxy in poolProxies.m_proxies)
                    proxy.Populate(octree, pool);
            }
        }

        // 06002a2a: foreach disposal and original missing-reference faults retained.
        public override void UpdateProxies()
        {
            foreach (var pair in m_poolProxies)
                foreach (InstancePrefabProxy proxy in pair.Value.m_proxies)
                    proxy.UpdatePool();
        }

        // 06002a2b: capture pool/proxies before the two live name reads. Preserve
        // string literals, boxing order, trailing space and final AppendLine.
        public override void GetInfo(StringBuilder stringInfoBuilder)
        {
            foreach (var pair in m_poolProxies)
            {
                ComponentPrefabPool<T> pool = pair.Value.m_pool;
                List<InstancePrefabProxy> proxies = pair.Value.m_proxies;
                stringInfoBuilder.Append(string.Concat("Prefab: ", pair.Key.name, ", Pool: ", pool.name));
                stringInfoBuilder.Append(string.Format("Proxies: Active = ({0} / {1}) Total = {2} ",
                    pool.UsedObjects, pool.MaxObjects, proxies.Count));
                stringInfoBuilder.AppendLine();
            }
        }

        // 06002a2c: dictionary initialization above precedes the non-generic base.
        protected InstancePrefabPool() { }
    }
}
