using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [CreateAssetMenu(menuName = "Hardlight/HLInput/Modifiers/Debounce")]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class ModifierDebounce : ModifierGameInputState
    {
        [SerializeField] private float m_debounceTime = 1f;

        public override float Modify(float value, float deltaTime) // 06000150
        {
            if ((value > 0f || value < 0f) && !m_stateChange)
            {
                m_stateChange = true;
                m_timer = 0f;
            }
            if (m_stateChange)
            {
                m_timer += deltaTime;
                if (m_timer >= m_debounceTime)
                {
                    m_stateChange = false;
                    return 0f;
                }
            }
            return value;
        }

        public override float Modify(float originalValue, float deltaTime, GameInput gameInput) // 06000151
        {
            GameInputState state;
            if (!m_gameInputStates.TryGetValue(gameInput, out state))
            {
                state = new GameInputState();
                m_gameInputStates.Add(gameInput, state);
            }
            if ((originalValue > 0f || originalValue < 0f) && !state.StateChange)
            {
                state.StateChange = true;
                state.Timer = 0f;
            }
            if (state.StateChange)
            {
                state.Timer += deltaTime;
                if (state.Timer >= m_debounceTime)
                {
                    state.StateChange = false;
                    return 0f;
                }
            }
            return originalValue;
        }

        public override Vector3 Modify(Vector3 value, float deltaTime) // 06000152
        {
            if (value.sqrMagnitude > 0f && !m_stateChange)
            {
                m_stateChange = true;
                m_timer = 0f;
            }
            if (m_stateChange)
            {
                m_timer += deltaTime;
                if (m_timer >= m_debounceTime)
                {
                    Vector3 zero = Vector3.zero;
                    m_stateChange = false;
                    return zero;
                }
            }
            return value;
        }

        // 06000153: original reset leaves the per-GameInput dictionary intact.
        public override void Reset() { m_stateChange = false; m_timer = 0f; }
        public ModifierDebounce() { } // 06000154
    }
}
