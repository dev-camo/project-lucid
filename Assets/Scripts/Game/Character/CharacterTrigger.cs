using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [RequireComponent(typeof(Collider))]
    public abstract class CharacterTrigger : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Will only trigger if entering collider has this tag. Set to empty to ignore tags.")]
        private string m_triggerTag = "Player";
        [SerializeField] private bool m_requireEntryAngle;
        [ShowIf("m_requireEntryAngle", null)]
        [Tooltip("Will trigger if angle from incoming character direction to this transform's forward is less than or equal to this.")]
        [SerializeField] private float m_entryAngleDegrees;
        [Tooltip("Enter, stay and exit triggers are still called when character is not active.")]
        [SerializeField] private bool m_triggerWhenInactive;
        protected Collider m_collider;
        protected SystemRef<CharacterManager> m_characterManagerRef;
        private bool m_triggerEnterWasBlocked;

        protected virtual void Reset()
        {
            m_collider = GetComponent<Collider>();
            m_collider.isTrigger = true;
            gameObject.layer = LayerMask.NameToLayer("CharacterCollision");
        }

        protected virtual void Awake()
        {
            m_collider = GetComponent<Collider>();
            m_characterManagerRef = ProcessManager.GetSystemRef<CharacterManager>(null, true);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!enabled || !TryGetCharacter(other, out Character character))
                return;

            // Original direction is trigger position minus character world position.
            // Store the blocked decision before invoking the overridable callback.
            if (m_requireEntryAngle)
                m_triggerEnterWasBlocked = Vector3.Angle(transform.position - character.WorldPosition,
                    transform.forward) > m_entryAngleDegrees;
            else
                m_triggerEnterWasBlocked = false;

            if (!m_triggerEnterWasBlocked)
                DoTriggerEnter(character);
        }

        private void OnTriggerStay(Collider other)
        {
            if (enabled && !m_triggerEnterWasBlocked && TryGetCharacter(other, out Character character))
                DoTriggerStay(character);
        }

        private void OnTriggerExit(Collider other)
        {
            if (enabled && !m_triggerEnterWasBlocked && TryGetCharacter(other, out Character character))
                DoTriggerExit(character);
        }

        protected Character Character => m_characterManagerRef.Get().GetCurrentCharacterUnsafe();

        public void SetTriggerWhenInactive(bool triggerWhenInactive)
            => m_triggerWhenInactive = triggerWhenInactive;

        protected bool TryGetCharacter(out Character character)
        {
            return m_characterManagerRef.Get().TryGetCurrentCharacter(out character)
                && (m_triggerWhenInactive || character.IsActive());
        }

        protected bool TryGetCharacter(Collider other, out Character character)
        {
            character = null;
            if (!string.IsNullOrEmpty(m_triggerTag) && !other.CompareTag(m_triggerTag))
                return false;
            return m_characterManagerRef.Get().IsCurrentCharacterColliderCollision(other)
                && TryGetCharacter(out character);
        }

        // All three original virtual defaults are immediate returns on both architectures.
        protected virtual void DoTriggerEnter(Character character) { }
        protected virtual void DoTriggerStay(Character character) { }
        protected virtual void DoTriggerExit(Character character) { }
        protected CharacterTrigger() { }
    }
}
