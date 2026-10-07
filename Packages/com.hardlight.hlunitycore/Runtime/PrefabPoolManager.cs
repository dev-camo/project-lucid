using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class PrefabPoolManager : MonoBehaviour, ISystem
    {
        [SerializeField] private bool m_guiEnabled;
        private GUIStyle m_style;
        private string m_info;
        protected readonly Dictionary<PrefabPoolType, IPrefabPool> m_prefabPools = new Dictionary<PrefabPoolType, IPrefabPool>();

        public bool GUIEnabled { get => m_guiEnabled; private set => m_guiEnabled = value; }

        public void RegisterPool(IPrefabPool pool)
        {
            // Preserve the original preliminary lookup and replacement order.
            m_prefabPools.ContainsKey(pool.ObjectPoolType);
            m_prefabPools[pool.ObjectPoolType] = pool;
        }

        public void UnregisterPool(IPrefabPool pool)
        {
            if (m_prefabPools.TryGetValue(pool.ObjectPoolType, out IPrefabPool registered) && ReferenceEquals(registered, pool))
                m_prefabPools.Remove(pool.ObjectPoolType);
        }

        private void Awake()
        {
            ProcessManager.RegisterSystem(this, null, false, false);
            m_style = new GUIStyle { fontSize = 15, normal = { textColor = Color.white } };
        }

        private void OnDestroy() => ProcessManager.UnregisterSystem(this);

        private void Update()
        {
            if (Input.GetKeyDown((KeyCode)289)) GUIEnabled = !GUIEnabled;
            m_info = string.Empty;
            if (!GUIEnabled) return;
            foreach (IPrefabPool pool in m_prefabPools.Values)
            {
                string overflow = pool.HasOverflowed ? "[Overflowed]" : string.Empty;
                int used = pool.UsedObjects;
                int max = pool.MaxObjects;
                int maxUsed = pool.MaxUsedObjects;
                int percent = (int)((float)used / max * 100f);
                m_info += string.Format("{0} - {1} - {2}/{3} - {4}% - MaxUsed {5} {6}\n",
                    pool.ObjectPoolType.name, pool.name, used, max, percent, maxUsed, overflow);
            }
        }

        public IPrefabPool GetPoolByType(PrefabPoolType meshPoolType) => m_prefabPools.GetValueOrDefault(meshPoolType);
    }
}
