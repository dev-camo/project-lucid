using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;
namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Serializable]
    public class MissionScorerStreak
    {
        [Tooltip("Defines a mission scorer streak type."), SerializeField]
        private MissionScorerStreakDefinition m_definition;
        [SerializeField, Tooltip("The objectives associated to this streak to complete when all triggered.")]
        private List<MissionScorerStreakObjective> m_objectives = new List<MissionScorerStreakObjective>();
        private float m_time;
        private int m_progress;
        private int ObjectiveCount => m_objectives.Count;
        public void Update(float deltaTime)
        {
            if (m_time == 0f) return;
            m_time = Mathf.Max(m_time - deltaTime, 0f);
            if (m_time <= 0f) m_progress = 0;
        }
        public bool TryUpdateFromObjective(MissionScorerStreakObjective streakObjective, out float score, out bool usesComboMultiplier)
        {
            score = 0f;
            usesComboMultiplier = m_definition.UsesComboMultiplier;
            int index = m_objectives.IndexOf(streakObjective);
            if (index >= 0)
            {
                m_progress = unchecked(m_progress + 1);
                m_time = m_definition.Timeout;
                if (m_progress == ObjectiveCount) score = m_definition.GetScore(m_progress);
            }
            return index >= 0;
        }
        public MissionScorerStreak() { }
    }
}
