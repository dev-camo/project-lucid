using Cinemachine;
using Hardlight;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime TimeScaledCinemachineCore (TypeDef 0x020007fb, x86-64 0x5F9D20..0x5F9F9F): the single
    // instance that overrides Cinemachine's delta and current time with the category's scaled time.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class TimeScaledCinemachineCore : TimeScaledComponent_SDT
    {
        private static TimeScaledCinemachineCore s_instance;
        private float m_overallTime;

        // 06002e35: a second component neither registers nor subscribes while an instance is alive.
        protected override void Initialise(TimeManager timeManager)
        {
            if (s_instance != null) return;
            s_instance = this;
            base.Initialise(timeManager);
        }

        protected override void InternalUpdate(float deltaTime) // 06002e36
        {
            m_overallTime += deltaTime;
            CinemachineCore.UniformDeltaTimeOverride = deltaTime;
            CinemachineCore.CurrentTimeOverride = m_overallTime;
        }

        // 06002e37: the overrides go back to Cinemachine's "unset" value of -1.
        protected override void OnDisable()
        {
            base.OnDisable();
            CinemachineCore.UniformDeltaTimeOverride = -1f;
            CinemachineCore.CurrentTimeOverride = -1f;
            s_instance = null;
        }

        public TimeScaledCinemachineCore() { } // 06002e38
    }
}
