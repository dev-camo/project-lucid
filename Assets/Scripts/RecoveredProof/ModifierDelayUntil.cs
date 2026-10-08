using System.Collections.Generic;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    [CreateAssetMenu(menuName = "Hardlight/HLInput/Modifiers/Delay Until")]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public sealed class ModifierDelayUntil : InputModifier
    {
        [SerializeField] private float m_delaySeconds;
        private readonly Dictionary<GameInput, float> m_cachedTimeByInput = new Dictionary<GameInput, float>();
        private float m_inputHeldSeconds;

        public override float Modify(float value, float deltaTime) => value; // Original 060023e6 is identity.

        // Original 060023e7 tests the OLD per-input time with strict > before accumulation.
        // Unordered comparison also accumulates and returns zero on both shipped CPUs.
        public override float Modify(float value, float deltaTime, GameInput gameInput)
        {
            if (!m_cachedTimeByInput.TryGetValue(gameInput, out float time))
            {
                time = 0f;
                m_cachedTimeByInput.Add(gameInput, time);
            }
            if (time > m_delaySeconds)
                return value;
            time += deltaTime;
            m_cachedTimeByInput[gameInput] = time;
            return 0f;
        }

        // Original 060023e8 tests the NEW vector time with ordered <, unlike the keyed scalar.
        public override Vector3 Modify(Vector3 value, float deltaTime)
        {
            m_inputHeldSeconds += deltaTime;
            if (m_inputHeldSeconds < m_delaySeconds)
                return Vector3.zero;
            return value;
        }

        public override void Reset() // Original 060023e9: a clear fault leaves vector time unchanged.
        {
            m_cachedTimeByInput.Clear();
            m_inputHeldSeconds = 0f;
        }

        public ModifierDelayUntil() { } // Original 060023ea; no nonzero delay initializer.
    }
}
