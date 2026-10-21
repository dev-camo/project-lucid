using System.Collections.Generic;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000253; 0x7118D0..0x711AF0 plus the out-of-line bodies below.
    // The boss list is the BossManager's live list; the active boss rotates through it.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class FourmenManager : MonoBehaviour, ISystem
    {
        private int m_activeBossIndex;
        private SystemRef<BossManager> m_bossManagerRef;
        private BossManager m_bossManager;

        public List<Boss> BossesInMotion { get; set; } = new List<Boss>(); // 06000c9e / 06000c9f

        // 06000ca0: the system registers before the manager callback is subscribed.
        private void Awake()
        {
            ProcessManager.RegisterSystem(this, null, false, false);
            m_bossManagerRef = ProcessManager.GetSystemRef<BossManager>(null, true);
            m_bossManagerRef.InvokeOnValid(OnBossManagerValid);
        }

        // 06000ca1.
        private void OnBossManagerValid(BossManager bossManager) => m_bossManager = bossManager;

        // 06000ca2.
        public void ResetActiveBossIndex() => m_activeBossIndex = 0;

        // 06000ca3.
        public void IncrementActiveBossIndex() => m_activeBossIndex++;

        // 06000ca4.
        public bool HasActiveBossUnit()
        {
            foreach (Boss boss in m_bossManager.Bosses)
                if (boss.IsActivePartOfMultiBodyBoss) return true;
            return false;
        }

        // 06000ca5: the active index wraps over the boss list; only that boss keeps priority 1.
        public void ConfigureActiveBoss()
        {
            int activeIndex = m_activeBossIndex % m_bossManager.Bosses.Count;
            m_bossManager.Bosses[activeIndex].SetActiveBossEntity();
            for (int i = 0; i < m_bossManager.Bosses.Count; i++)
                m_bossManager.Bosses[i].SetMovementPriority(0);
            m_bossManager.Bosses[activeIndex].SetMovementPriority(1);
        }

        // 06000ca6.
        public void ClearActiveBosses()
        {
            for (int i = 0; i < m_bossManager.Bosses.Count; i++)
                m_bossManager.Bosses[i].ClearActiveBossEntity();
        }

        // 06000ca7.
        public void ClearLoopStatus()
        {
            for (int i = 0; i < m_bossManager.Bosses.Count; i++)
                m_bossManager.Bosses[i].ClearLoop();
        }

        // 06000ca8.
        public bool HasCompletedActiveLoop()
        {
            foreach (Boss boss in m_bossManager.Bosses)
                if (!boss.HasBeenActiveThisLoop) return false;
            return true;
        }

        // 06000ca9: the mean of the boss world positions (an empty list divides by zero).
        public Vector3 GetAveragePosition()
        {
            Vector3 sum = Vector3.zero;
            foreach (Boss boss in m_bossManager.Bosses)
                sum += boss.WorldPosition;
            return sum / m_bossManager.Bosses.Count;
        }

        // 06000caa: the emitters of the given boss and of the next boss around the ring.
        public Vector3[] CalculateLaserPositions(Boss boss, ActorAttachPointType attachPointType)
        {
            Vector3[] positions = new Vector3[2];
            for (int i = 0; i < m_bossManager.Bosses.Count; i++)
            {
                if (boss == m_bossManager.Bosses[i])
                {
                    positions[0] = m_bossManager.Bosses[i % m_bossManager.Bosses.Count].GetLaserEmitterPosition(attachPointType);
                    positions[1] = m_bossManager.Bosses[(i + 1) % m_bossManager.Bosses.Count].GetLaserEmitterPosition(attachPointType);
                }
            }
            return positions;
        }

        // 06000cab.
        public int GetIndexForBoss(Boss boss)
        {
            for (int i = 0; i < m_bossManager.Bosses.Count; i++)
                if (m_bossManager.Bosses[i] == boss) return i;
            return -1;
        }

        // 06000cac.
        public int BossCount() => m_bossManager.Bosses.Count;

        // 06000cad.
        private void OnDestroy() => ProcessManager.UnregisterSystem(this);
    }
}
