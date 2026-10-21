using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime TimeScaledShader (TypeDef 0x020007ff, x86-64 0x5FB630..0x5FB6BF): publishes the total
    // scaled time to a global shader float, restarting from zero once it passes the maximum.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class TimeScaledShader : TimeScaledComponent_SDT
    {
        [SerializeField, Tooltip("The shader property name to set with the total scaled time.")]
        private string m_timePropertyName;
        [Tooltip("Maximum allowable time before a reset is enforced.  Default is 36000 seconds, ie 10 hours.  Half precision ranges to +- 60,000."), SerializeField]
        private float m_maxTime = 36000f;
        private float m_time;

        public override void ResetSynchronise() // 06002e5b
        {
            base.ResetSynchronise();
            m_time = 0f;
        }

        // 06002e5c: a NaN time is not above the maximum and is published as it is.
        protected override void InternalUpdate(float deltaTime)
        {
            m_time += deltaTime;
            if (m_time > m_maxTime) ResetSynchronise();
            Shader.SetGlobalFloat(m_timePropertyName, m_time);
        }

        public TimeScaledShader() { } // 06002e5d
    }
}
