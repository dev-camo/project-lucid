using Hardlight.UI.Binding;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime owner 02000a1d; methods 060039e9..060039f0.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class RateOfChangeBindable : TimeScaledComponent_SDT
    {
        [SerializeField]
        private float m_lowerBound;
        [SerializeField]
        private float m_upperBound = 1f;
        [SerializeField]
        private float m_startValue;
        [SerializeField]
        private float m_startRateOfChange;
        public readonly Bindable<float> Output = new Bindable<float>();
        private float m_rateOfChange;

        protected override void Awake()
        {
            base.Awake();
            m_rateOfChange = m_startRateOfChange;
            SetOutputWithConstraint(m_startValue);
        }

        // 060039ea: the shipping body reads gameObject (0x6b35a5) and discards
        // it. An ordered lower bound at or above the upper bound is moved one
        // below it; NaN bounds are left alone (ucomiss/jb at 0x6b35b4).
        protected override void OnValidate()
        {
            base.OnValidate();
            _ = gameObject;
            if (m_lowerBound >= m_upperBound)
                m_lowerBound = m_upperBound - 1f;
            m_startValue = LoopOutputWithinBounds(m_startValue, m_lowerBound, m_upperBound);
        }

        protected override void InternalUpdate(float deltaTime)
        {
            SetOutputWithConstraint(Output.Value + deltaTime * m_rateOfChange);
        }

        public void SetOutput(float output)
        {
            SetOutputWithConstraint(output);
        }

        public void SetRateOfChange(float output)
        {
            m_rateOfChange = output;
        }

        private void SetOutputWithConstraint(float output)
        {
            Output.Value = LoopOutputWithinBounds(output, m_lowerBound, m_upperBound);
        }

        // 060039ef: wraps by whole multiples of the range using a ceiling
        // (roundss 0xa). Only ordered out-of-range values move, so a value on a
        // bound, or NaN, is returned unchanged.
        private static float LoopOutputWithinBounds(float output, float lowerBound, float upperBound)
        {
            float range = upperBound - lowerBound;
            if (output < lowerBound)
                return output + Mathf.Ceil((lowerBound - output) / range) * range;
            if (output > upperBound)
                return output - Mathf.Ceil((output - upperBound) / range) * range;
            return output;
        }
    }
}
