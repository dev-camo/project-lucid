using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x020006f8: completes once the authored number of boss defeats is reached.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class MissionBossTracker : MissionTracker
    {
        [SerializeField] private int m_bossesToDefeat;
        [Tooltip("Tick this to pull all the rings out on load and prevent them respawning.")]
        [SerializeField] private bool m_removeRingsFromArena;
        private BossManager m_bossManager;
        private int m_bossesDefeated;

        // 06002621 (0x5AA5F0) and closure 06002628 (0x5AAA50): the boss events are subscribed once the manager is valid.
        public override void Initialise(MissionState missionState, IMissionContext missionContext)
        {
            base.Initialise(missionState, missionContext);
            ProcessManager.GetSystemRef<BossManager>(null, true).InvokeOnValid(manager =>
            {
                m_bossManager = manager;
                m_bossManager.OnBossDefeated += OnBossDefeated;
                m_bossManager.OnBossHardFailure += Action_FailMission;
            });
            RemoveRingsIfRequired();
        }

        // 06002622 (0x5AA800)
        private void OnBossDefeated()
        {
            m_bossesDefeated++;
            if (AllBossesDefeated()) MissionObjectivesCompleted();
        }

        // 06002623 (0x5AA830): signed >= comparison.
        private bool AllBossesDefeated() => m_bossesDefeated >= m_bossesToDefeat;

        // 06002624 (0x5AA760): the manager is fetched unguarded; the force-deactivate event is null-conditional.
        private void RemoveRingsIfRequired()
        {
            if (m_removeRingsFromArena)
                ProcessManager.GetSystem<CollectableManager>(null, true).OnCollectableForceDeactivate?.Invoke(CollectableType.Ring);
        }

        // 06002625 (0x5AA850): the count resets and rings are removed before the base reset.
        public override void ResetMission()
        {
            m_bossesDefeated = 0;
            RemoveRingsIfRequired();
            base.ResetMission();
        }

        // 06002626 (0x5AA8F0): m_bossManager is dereferenced unguarded.
        protected override void OnDestroy()
        {
            m_bossManager.OnBossDefeated -= OnBossDefeated;
            m_bossManager.OnBossHardFailure -= Action_FailMission;
            base.OnDestroy();
        }

        public MissionBossTracker() { }
    }
}
