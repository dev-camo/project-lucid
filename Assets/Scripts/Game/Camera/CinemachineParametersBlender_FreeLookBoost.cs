using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime type 0x02000286.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class CinemachineParametersBlender_FreeLookBoost : CinemachineParametersBlender_FreeLook
    {
        [SerializeField]
        private AnimationCurve m_blendCurveBoosting;
        [SerializeField]
        [Tooltip("Blend speed into boosting.")]
        private float m_blendInSpeed = 1f;
        [Tooltip("Blend speed out of boosting.")]
        [SerializeField]
        private float m_blendOutSpeed = 1f;
        [SerializeField]
        [Tooltip("Used to smoothly blend from default curve to boosting curve.")]
        private AnimationCurve m_blendBetweenDefaultAndBoostCurve = AnimationCurve.Constant(0f, 1f, 1f);
        private readonly SystemRef<CharacterManager> m_characterManagerRef = ProcessManager.GetSystemRef<CharacterManager>();
        private float m_boostToDefaultTime;

        // 0x06000df6: the boost timer moves along the transition curve and mixes the two blend curves.
        protected override float EvaluateBlendCurve(float deltaTime)
        {
            m_boostToDefaultTime += IsBoosting() ? deltaTime * m_blendInSpeed : -deltaTime * m_blendOutSpeed;
            float lastKeyTime = m_blendBetweenDefaultAndBoostCurve[m_blendBetweenDefaultAndBoostCurve.length - 1].time;
            m_boostToDefaultTime = Mathf.Clamp(m_boostToDefaultTime, 0f, lastKeyTime);
            float boostWeight = m_blendBetweenDefaultAndBoostCurve.Evaluate(m_boostToDefaultTime);
            float defaultValue = base.EvaluateBlendCurve(deltaTime);
            float boostValue = m_blendCurveBoosting.Evaluate(GetBlendSourceValue());
            return Mathf.Lerp(defaultValue, boostValue, boostWeight);
        }

        // 0x06000df7
        private bool IsBoosting()
        {
            return !m_characterManagerRef.IsNull()
                && m_characterManagerRef.Get().TryGetCurrentCharacter(out Character character)
                && character.BoostStamina.Active;
        }

        // 0x06000df8
        public CinemachineParametersBlender_FreeLookBoost() { }
    }
}
