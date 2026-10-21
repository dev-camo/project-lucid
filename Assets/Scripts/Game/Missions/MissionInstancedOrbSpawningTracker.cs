using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x020006ff: instead of completing straight away, the last objective spawns a completion orb
    // and the mission completes when the orb is collected.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class MissionInstancedOrbSpawningTracker : MissionInstancedTracker
    {
        [SerializeField] private Collectable m_completionOrb;
        [SerializeField] private Transform m_orbLocation;
        private Collectable m_completionOrbInstance;
        private LightweightTransform m_orbSpawnLocation;

        // 06002692 (0x5AF6E0): the base handling is replaced. The last completed objective's own transform is the
        // fallback spawn point; the orb spawns once every objective is complete, whatever the target count says.
        protected override void StatusChanged(MissionObjective missionObjective)
        {
            if (missionObjective.IsComplete)
            {
                if (m_completedObjectives.Add(missionObjective))
                {
                    m_onAnyObjectiveCompleted.Invoke();
                    m_orbSpawnLocation = new LightweightTransform(missionObjective.transform);
                }
            }
            else
            {
                m_completedObjectives.Remove(missionObjective);
            }

            int completeCount = 0;
            foreach (MissionObjective objective in m_missionObjectives)
                completeCount += objective.IsComplete ? 1 : 0;

            if (m_persistObjectiveCompletion)
                m_missionState.MarkObjectiveComplete(m_missionObjectives.IndexOf(missionObjective), missionObjective.IsComplete);
            m_missionState.UpdateMissionProgress(completeCount, m_targetObjectivesToComplete, m_persistObjectiveCompletion);
            if (completeCount == m_missionObjectives.Count)
            {
                UnsubscribeFromObjectives();
                SpawnOrb();
            }
        }

        // 06002693 (0x5AFD00): an assigned orb location overrides the fallback spawn point.
        private void SpawnOrb()
        {
            if (m_orbLocation != null)
                m_orbSpawnLocation = new LightweightTransform(m_orbLocation);
            m_completionOrbInstance = Instantiate(m_completionOrb, m_orbSpawnLocation.Location, m_orbSpawnLocation.Orientation);
            m_completionOrbInstance.OnCollected += OrbCollected;
        }

        // 06002694 (0x5AFEF0)
        private void OrbCollected(Collectable orb)
        {
            m_completionOrbInstance.OnCollected -= OrbCollected;
            MissionObjectivesCompleted();
        }

        public MissionInstancedOrbSpawningTracker() { }
    }
}
