using System;
using System.Collections.Generic;
using UnityEngine;

namespace HardlightProject
{
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
    [UnityEngine.CreateAssetMenu(fileName = "MissionScorerStreakDefinition", menuName = "HardlightProject/DefinitionData/Definitions/MissionScorerStreakDefinition")]
    public class MissionScorerStreakDefinition : Hardlight.ScriptableObjectWithGuid
    {
        [UnityEngine.Tooltip("The identifier for this streak.")]
        [UnityEngine.SerializeField]
        private HardlightProject.MissionScorerStreakIdentifier m_identifier;

        [UnityEngine.SerializeField]
        [UnityEngine.Tooltip("Definition will be excluded from auto generation lists.")]
        private System.Boolean m_excludeFromAutoGeneration;

        [UnityEngine.Header("Type definition.")]
        [UnityEngine.SerializeField]
        private System.Boolean m_damageableObject;

        [UnityEngine.SerializeField]
        private System.Collections.Generic.List<HardlightProject.EnemyType> m_enemyTypes = new List<EnemyType>();

        [UnityEngine.SerializeField]
        private System.Collections.Generic.List<HardlightProject.CollectableType> m_collectableTypes = new List<CollectableType>();

        [UnityEngine.Tooltip("Thresholds of object count for scoring.")]
        [UnityEngine.SerializeField]
        private System.Collections.Generic.List<HardlightProject.MissionScorerStreakDefinition.ScoreThreshold> m_scoreThresholds = new List<ScoreThreshold>();

        [UnityEngine.Tooltip("Minimum object count to be considered as a streak.")]
        [UnityEngine.SerializeField]
        private System.Single m_minCount;

        [UnityEngine.SerializeField]
        [UnityEngine.Tooltip("Time after an object trigger to cancel the streak.")]
        private System.Single m_timeout;

        [UnityEngine.Tooltip("Whether to multiply score by the current combo multiplier.")]
        [UnityEngine.SerializeField]
        private System.Boolean m_usesComboMultiplier;

        // Original Game.Runtime 0x06001e52, ARM 0x52b1b0.
        public HardlightProject.MissionScorerStreakIdentifier Identifier => m_identifier;

        // Original Game.Runtime 0x06001e53, ARM 0x52b1b8.
        public System.Boolean ExcludeFromAutoGeneration => m_excludeFromAutoGeneration;

        // Original Game.Runtime 0x06001e54, ARM 0x52b1c0.
        public System.Single MinCount => m_minCount;

        // Original Game.Runtime 0x06001e55, ARM 0x52b1c8.
        public System.Single Timeout => m_timeout;

        // Original Game.Runtime 0x06001e56, ARM 0x52b1d0.
        public System.Boolean UsesComboMultiplier => m_usesComboMultiplier;

        // Original Game.Runtime 0x06001e57, ARM 0x52b1d8.
        public System.Boolean DamageableObject => m_damageableObject;

        // Original Game.Runtime 0x06001e58, ARM 0x52b1e0.
        public bool HasEnemyType(EnemyType enemyType) => m_enemyTypes.Contains(enemyType);

        // Original Game.Runtime 0x06001e59, ARM 0x52b23c.
        public bool HasCollectableType(CollectableType collectableType) => m_collectableTypes.Contains(collectableType);

        // Original Game.Runtime 0x06001e5a, ARM 0x52b298.
        // Stop at the first greater threshold, retaining the previous score.
        // Keep authored list order and the native unordered comparison: NaN Count
        // does not take this greater-than branch and its Score is retained.
        public float GetScore(int streakCount)
        {
            float score = 0f;
            foreach (ScoreThreshold threshold in m_scoreThresholds)
            {
                if (threshold.Count > streakCount) break;
                score = threshold.Score;
            }
            return score;
        }

        [Serializable]
        private class ScoreThreshold
        {
            [UnityEngine.SerializeField]
            [UnityEngine.Tooltip("Number of objects in the streak up to which we assign the score.")]
            private System.Single m_count;

            [UnityEngine.Tooltip("Score assigned to this streak count.")]
            [UnityEngine.SerializeField]
            private System.Single m_score;

            // Original Game.Runtime 0x06001e5c, ARM 0x52b518.
            public System.Single Count => m_count;

            // Original Game.Runtime 0x06001e5d, ARM 0x52b520.
            public System.Single Score => m_score;

            // Original Game.Runtime 0x06001e5e, ARM 0x52b528.
            // Natural parameterless Object-base constructor leaves both floats zero.
        }
        // Original Game.Runtime 0x06001e5b, ARM 0x52b3e4.
        // Natural original constructor; documented field initializers precede the genuine base call.
    }
}
