using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime owner 02000a99; methods 06003d06..06003d0a.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class PrefabPoolActivation : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("The definition for the pool type we want to spawn from.")]
        private PrefabPoolType m_prefabPoolType;
        [Tooltip("Transforms that set the location of each spawn.")]
        [SerializeField]
        private Transform[] m_spawnTransforms;
        private readonly SystemRef<LevelManager> m_levelManagerRef = ProcessManager.GetSystemRef<LevelManager>();
        private readonly SystemRef<PrefabPoolManager> m_prefabPoolManagerRef = ProcessManager.GetSystemRef<PrefabPoolManager>();
        private IEntityPrefabPool m_prefabPool;

        // 06003d06: Get, not InvokeOnValid; the level manager must already be registered.
        private void Awake()
        {
            m_levelManagerRef.Get().InvokeOnLevelActivated(OnLevelActivated, true);
        }

        private void OnDestroy()
        {
            if (m_levelManagerRef.TryGet(out LevelManager levelManager))
                levelManager.RemoveLevelActivatedAction(OnLevelActivated);
        }

        // 06003d08: a pool of another kind is stored as null.
        private void OnLevelActivated(LevelManagerLevel levelManagerLevel)
        {
            m_prefabPool = m_prefabPoolManagerRef.Get().GetPoolByType(m_prefabPoolType) as IEntityPrefabPool;
        }

        // 06003d09: unassigned (Unity-null) transforms are skipped; each spawn
        // uses the interface defaults (scale 1, no callback).
        public void Activate()
        {
            foreach (Transform spawnTransform in m_spawnTransforms)
            {
                if (spawnTransform == null)
                    continue;
                m_prefabPool.SpawnInstance(spawnTransform);
            }
        }
    }
}
