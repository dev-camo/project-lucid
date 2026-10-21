using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000718: an instanced mission whose objectives become active in sequence; at most
    // m_activeObjectiveLimit are active at once and each completion activates the next waiting objective.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class MissionSequencedInstancedTracker : MissionInstancedTracker
    {
        [SerializeField]
        [Min(1)]
        private int m_activeObjectiveLimit = 1;
        private readonly HashSet<MissionObjective> m_inactiveObjectives = new HashSet<MissionObjective>();
        private readonly HashSet<MissionObjective> m_activeObjectives = new HashSet<MissionObjective>();

        // 060027a5 (0x5C0EF0): a complete mission keeps the sets it has; otherwise both start empty.
        public override void Initialise(MissionState missionState, IMissionContext missionContext)
        {
            base.Initialise(missionState, missionContext);
            if (missionState.Complete)
                return;
            m_inactiveObjectives.Clear();
            m_activeObjectives.Clear();
        }

        // 060027a6 (0x5C0FA0): the base start-up (MissionTracker.StartMission, inlined in the native body) runs first.
        // Every objective is then reset without a status report; the first m_activeObjectiveLimit are activated and
        // the rest wait.
        public override void StartMission(bool isSubMission)
        {
            base.StartMission(isSubMission);
            int activated = 0;
            foreach (MissionObjective objective in MissionObjectives)
            {
                objective.OnObjectiveReset(false);
                if (activated >= m_activeObjectiveLimit)
                {
                    m_inactiveObjectives.Add(objective);
                    continue;
                }
                objective.OnObjectiveActivated();
                m_activeObjectives.Add(objective);
                activated++;
            }
        }

        // 060027a7 (0x5C13E0): an unstarted, non-persisting mission ignores the change entirely, base included. The
        // changed objective is filed as complete (in neither set), active or waiting; objectives are then activated
        // in order while some wait and fewer than the limit are active. The base counts progress afterwards.
        protected override void StatusChanged(MissionObjective missionObjective)
        {
            if (!m_hasStarted && !m_persistObjectiveCompletion)
                return;
            if (missionObjective.IsComplete)
            {
                m_inactiveObjectives.Remove(missionObjective);
                m_activeObjectives.Remove(missionObjective);
            }
            else if (missionObjective.IsActive)
            {
                m_inactiveObjectives.Remove(missionObjective);
                m_activeObjectives.Add(missionObjective);
            }
            else
            {
                m_inactiveObjectives.Add(missionObjective);
                m_activeObjectives.Remove(missionObjective);
            }

            foreach (MissionObjective objective in MissionObjectives)
            {
                if (m_inactiveObjectives.Count == 0 || m_activeObjectives.Count >= m_activeObjectiveLimit)
                    break;
                if (objective.IsActive || objective.IsComplete)
                    continue;
                objective.OnObjectiveActivated();
                m_inactiveObjectives.Remove(objective);
                m_activeObjectives.Add(objective);
            }
            base.StatusChanged(missionObjective);
        }

        // 060027a8 (0x5C1820): a cyan sphere (radius 0.5) on each objective, joined to the previous one by a line.
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            MissionObjective previous = null;
            foreach (MissionObjective objective in MissionObjectives)
            {
                Vector3 position = objective.transform.position;
                Gizmos.DrawSphere(position, 0.5f);
                if (previous != null)
                    Gizmos.DrawLine(previous.transform.position, position);
                previous = objective;
            }
        }

    }
}
