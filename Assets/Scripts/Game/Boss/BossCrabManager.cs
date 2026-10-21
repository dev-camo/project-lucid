using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 020001f4; 0x700910..0x700A10.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class BossCrabManager : MonoBehaviour, ISystem
    {
        [SerializeField] private PrefabPoolType m_attackWaveType;

        // 06000b61: only a registered BossTimedObjectPool of the attack wave type is flagged.
        public void DespawnAttackWaves()
        {
            if (ProcessManager.GetSystemRef<PrefabPoolManager>(null, true).TryGet(out PrefabPoolManager manager)
                && manager.GetPoolByType(m_attackWaveType) is BossTimedObjectPool pool)
                pool.ForceDespawnAllObjects();
        }

        private void OnDestroy() => DespawnAttackWaves();
    }
}
