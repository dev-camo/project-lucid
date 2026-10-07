using System;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class InputDebounce
    {
        private readonly float m_threshold;
        private readonly float m_timeMax;
        private float m_input;
        private float m_time;
        private bool m_ignoreInitialInput;
        private bool m_timeExceedContinuous;

        // Original HLInput.Runtime methods 060001f3..060001fa. The continuous
        // observation is strict; the Update crossing below includes equality.
        public float Input => m_input;
        public bool TimeExceeded { get; private set; }
        public bool ThresholdExceeded { get; private set; }
        public bool DirectionMaintained { get; private set; }
        public bool TimeExceededContinuous => m_time > m_timeMax;

        // 060001fb: the base constructor precedes these assignments. Native
        // absolute-value instructions do not retain the original Abs call name.
        public InputDebounce(float threshold, float time, float initialInput = 0f)
        {
            m_threshold = threshold;
            m_timeMax = time;
            m_input = initialInput;
            ThresholdExceeded = Math.Abs(initialInput) > threshold;
        }

        // 060001fc: retain both caller flags; Update consumes ignoreInitialInput
        // once and keeps the requested continuous mode until another Reset.
        public void Reset(bool ignoreInitialInput = false, bool timeExceedContinuous = false)
        {
            m_input = 0f;
            m_time = 0f;
            m_ignoreInitialInput = ignoreInitialInput;
            m_timeExceedContinuous = timeExceedContinuous;
            TimeExceeded = false;
            ThresholdExceeded = false;
            DirectionMaintained = false;
        }

        // 060001fd changes input/threshold only; existing timer and pulse state
        // survive until Update evaluates the new input history.
        public void SetInput(float input)
        {
            m_input = input;
            ThresholdExceeded = Math.Abs(input) > m_threshold;
        }

        // 060001fe: retain the previous threshold and input before writing the
        // current values. Ignoring the first input seeds the timer at its limit;
        // this suppresses the initial one-shot pulse rather than delaying it.
        public void Update(float input, float deltaTime)
        {
            float previousInput = m_input;
            m_input = input;
            bool previousThresholdExceeded = ThresholdExceeded;
            ThresholdExceeded = Math.Abs(input) > m_threshold;

            if (m_ignoreInitialInput)
            {
                m_ignoreInitialInput = false;
                m_time = m_timeMax;
                previousThresholdExceeded = ThresholdExceeded;
                previousInput = input;
            }

            if (!ThresholdExceeded || !previousThresholdExceeded)
            {
                DirectionMaintained = false;
                m_time = 0f;
                TimeExceeded = false;
                return;
            }

            DirectionMaintained = Math.Sign(previousInput) == Math.Sign(m_input);
            if (!ThresholdExceeded || !DirectionMaintained)
            {
                m_time = 0f;
                TimeExceeded = false;
                return;
            }

            // Both ordered comparisons matter for NaN and for negative time
            // increments. Write the pre-increment gate, then the accumulated
            // timer, then its crossing result, as in both supplied architectures.
            TimeExceeded = m_timeExceedContinuous || m_time < m_timeMax;
            m_time += deltaTime;
            TimeExceeded = TimeExceeded && m_time >= m_timeMax;
        }
    }
}
