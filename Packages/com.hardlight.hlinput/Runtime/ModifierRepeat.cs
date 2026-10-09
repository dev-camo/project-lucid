using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [CreateAssetMenu(menuName = "Hardlight/HLInput/Modifiers/Repeat")]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class ModifierRepeat : ModifierGameInputState<ModifierRepeat.State>
    {
        [Il2CppSetOption(Option.NullChecks, false)]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        public class State
        {
            public float Timer;
            public float LastUpdateTime;
            public float LastResetTime;
            public void Reset() { Timer = 0f; LastResetTime = Time.time; } // 06000166: LastUpdateTime unchanged.
            public State() { } // 06000167
        }

        [SerializeField] private float m_resetAfterInactivitySec = 0.05f;
        [SerializeField] private AnimationCurve m_cooldownOverTimeSec = new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(1f, 0.1f));

        // 06000162: original m_state is not initialized; preserve this null fault
        // after the near-zero early return. No repaired/synthetic state is added.
        public override float Modify(float originalValue, float deltaTime) { return InternalModify(originalValue, deltaTime, m_state); }

        public override float Modify(float originalValue, float deltaTime, GameInput gameInput) // 06000163
        {
            State state;
            if (!m_gameInputStates.TryGetValue(gameInput, out state))
            {
                state = new State();
                m_gameInputStates.Add(gameInput, state);
            }
            return InternalModify(originalValue, deltaTime, state);
        }

        private float InternalModify(float originalValue, float deltaTime, State state) // 06000164
        {
            if (MathUtilities.WithinTolerance(originalValue, 0f, 0.0001f)) return originalValue;
            float previousUpdateTime = state.LastUpdateTime;
            state.LastUpdateTime = Time.time;
            // These are distinct original Time reads; Reset has its own third read.
            float resetAfterInactivity = m_resetAfterInactivitySec;
            float currentTime = Time.time;
            if (previousUpdateTime + resetAfterInactivity < currentTime)
            {
                state.Reset();
                return originalValue;
            }
            float cooldown = m_cooldownOverTimeSec.Evaluate(state.LastUpdateTime - state.LastResetTime);
            float timer = state.Timer + deltaTime;
            if (timer >= cooldown)
            {
                state.Timer = 0f;
                return originalValue;
            }
            state.Timer = timer;
            return 0f;
        }

        public ModifierRepeat() { } // 06000165: original curve then generic-base dictionary; no m_state allocation.
    }
}
