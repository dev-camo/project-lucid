using UnityEngine;
using Unity.IL2CPP.CompilerServices;
namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class MissionScorerObjective : MissionObjective
    {
        [Tooltip("Score to add to overall mission score on triggering this objective."), SerializeField]
        private MissionScorerEvaluator m_scorerEvaluator;
        [SerializeField, Tooltip("Period of time before triggering again.")]
        private float m_cooldownTime = 0.5f;
        [SerializeField, Tooltip("Whether to use the combo multiplier for the added points.")]
        private bool m_usesComboMultiplier;
        [SerializeField, Tooltip("Describes this objective's type so we can uniquely identify in things such as prefab pools.")]
        private MissionScorerObjectiveType m_type;
        private float m_cooldownValue;
        private bool m_isCoolingDown;
        public float ScoreToAdd { get; private set; }
        public bool UsesComboMultiplier => m_usesComboMultiplier;
        public MissionScorerObjectiveType Type => m_type;
        public MissionScorerEvaluator ScorerEvaluator { get => m_scorerEvaluator; set => m_scorerEvaluator = value; }
        public void SetScoreToAdd(float scoreToAdd)
        {
            if (m_isCoolingDown) return;
            ScoreToAdd = scoreToAdd;
            StartCooldown();
        }
        public void Evaluate(Transform otherTransform)
        {
            if (m_isCoolingDown) return;
            ScoreToAdd = m_scorerEvaluator.Evaluate(this, otherTransform, out ActorAnimationDefinition animationDefinition);
            if (animationDefinition != null && TryGetCharacter(out Character character))
                character.TriggerAnimationEnter(animationDefinition);
            StartCooldown();
        }
        protected override void InternalUpdate(float deltaTime)
        {
            if (!m_isCoolingDown) return;
            m_cooldownValue -= deltaTime;
            if (m_cooldownValue > 0f) return;
            m_cooldownValue = 0f;
            m_isCoolingDown = false;
        }
        public void StartCooldown() { m_cooldownValue = m_cooldownTime; m_isCoolingDown = true; }
        public void SkipCooldown() { m_isCoolingDown = false; }
        public MissionScorerObjective() { }
    }
}
