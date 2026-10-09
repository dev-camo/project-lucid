using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [CreateAssetMenu(menuName = "Hardlight/HLInput/Modifiers/Cooldown")]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class ModifierCoolDown : ModifierGameInputState
    {
        [SerializeField] private float m_cooldownTime = 0.2f;

        // 06000148: scalar activation returns immediately; NaN is not nonzero.
        public override float Modify(float value, float deltaTime)
        {
            if ((value > 0f || value < 0f) && !m_stateChange)
            {
                m_stateChange = true;
                m_timer = 0f;
                return value;
            }
            if (m_stateChange)
            {
                m_timer += deltaTime;
                if (m_timer >= m_cooldownTime) m_stateChange = false;
                return 0f;
            }
            return value;
        }

        // 06000149: lookup/allocation/Add occurs even for a zero or NaN input.
        public override float Modify(float originalValue, float deltaTime, GameInput gameInput)
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
                return originalValue;
            }
            if (state.StateChange)
            {
                state.Timer += deltaTime;
                if (state.Timer >= m_cooldownTime) state.StateChange = false;
                return 0f;
            }
            return originalValue;
        }

        // 0600014a: vector activation falls through and advances its timer.
        // While cooling it returns input; only the completion frame returns zero.
        public override Vector3 Modify(Vector3 value, float deltaTime)
        {
            if (value.sqrMagnitude > 0f && !m_stateChange)
            {
                m_stateChange = true;
                m_timer = 0f;
            }
            if (m_stateChange)
            {
                m_timer += deltaTime;
                if (m_timer >= m_cooldownTime)
                {
                    Vector3 zero = Vector3.zero;
                    m_stateChange = false;
                    return zero;
                }
            }
            return value;
        }

        public ModifierCoolDown() { } // 0600014b: derived initializer before actual base.
    }
}
