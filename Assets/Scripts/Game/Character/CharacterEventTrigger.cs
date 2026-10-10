using System;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [RequireComponent(typeof(Collider))]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class CharacterEventTrigger : CharacterTrigger
    {
        public event Action<CharacterEventTrigger> OnTriggerEnter;
        public event Action<CharacterEventTrigger> OnTriggerExit;
        private Bounds m_bounds;
        private bool m_characterIsPresent;

        protected override void Awake()
        {
            base.Awake();
            if (m_collider == null)
                return;
            m_bounds = m_collider.bounds;
        }

        // Disabled or destroyed colliders use the original Awake snapshot.
        // Returning current enabled bounds does not replace that snapshot.
        public Bounds GetBounds()
        {
            if (m_collider != null && m_collider.enabled)
                return m_collider.bounds;
            return m_bounds;
        }

        protected override void DoTriggerEnter(Character character)
        {
            m_characterIsPresent = true;
            OnTriggerEnter?.Invoke(this);
        }

        protected override void DoTriggerExit(Character character)
        {
            m_characterIsPresent = false;
            OnTriggerExit?.Invoke(this);
        }

        // Original destruction notification retains the presence flag.
        private void OnDestroy()
        {
            if (m_characterIsPresent)
                OnTriggerExit?.Invoke(this);
        }

        public CharacterEventTrigger() { }
    }
}
