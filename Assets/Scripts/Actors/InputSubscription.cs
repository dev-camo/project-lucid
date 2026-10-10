using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public abstract class InputSubscription
    {
        private readonly GameControlsBinding m_binding;
        private readonly ValidatorFunc m_validator;

        // Original Game.Runtime 060023d8.
        protected GameInput GameInput => m_binding.GameInput;

        // Original 060023d9; neither argument has an optional default.
        protected InputSubscription(GameControlsBinding binding, ValidatorFunc validator)
        {
            m_binding = binding;
            m_validator = validator;
        }

        // Original 060023da: modifier faults precede tolerance and validator reads.
        protected virtual bool ApplyModifiers(ref float value)
        {
            m_binding.ApplyModifiers(ref value, Time.deltaTime);
            if (MathUtilities.WithinTolerance(value, 0f, 0.0001f))
                return false;
            return m_validator == null || m_validator();
        }

        public abstract void Subscribe(); // Original 060023db, zero native pointer.
        public abstract void Unsubscribe(); // Original 060023dc, zero native pointer.

        // Original nested 020006af/060023dd..e0; delegate runtime emission is held.
        public delegate bool ValidatorFunc();
    }
}
