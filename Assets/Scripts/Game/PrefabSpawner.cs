using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime owner 02000a1b; methods 060039e6..060039e8.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class PrefabSpawner : MonoBehaviour
    {
        [SerializeField]
        private GameObject[] m_prefabsToSpawn;
        [SerializeField]
        private Transform m_spawnPoint;
        [SerializeField]
        private bool m_parentToSpawnPoint;

        // 060039e6: an unassigned (or destroyed) spawn point falls back to this transform.
        private void Awake()
        {
            if (m_spawnPoint == null)
                m_spawnPoint = transform;
        }

        // 060039e7: parented spawns use Instantiate(original, parent); unparented
        // spawns copy the spawn point's world position and rotation.
        public void Spawn()
        {
            foreach (GameObject prefab in m_prefabsToSpawn)
            {
                if (m_parentToSpawnPoint)
                    Instantiate(prefab, m_spawnPoint);
                else
                    Instantiate(prefab, m_spawnPoint.position, m_spawnPoint.rotation);
            }
        }
    }
}
