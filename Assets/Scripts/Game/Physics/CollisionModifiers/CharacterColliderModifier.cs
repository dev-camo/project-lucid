using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime type 0x02000740: five native bodies and one abstract API.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public abstract class CharacterColliderModifier : CharacterCollisionModifier
    {
        [SerializeField]
        private Collider m_collider;

        // 0x060028d3. Original callback directly writes the stored collider.
        private void Awake()
        {
            m_collider.isTrigger = true;
        }

        // 0x060028d4.
        private void Reset()
        {
            m_collider = GetComponent<Collider>();
        }

        // 0x060028d5. Evaluate only after the original manager accepts this collider.
        private void OnTriggerEnter(Collider other)
        {
            if (TryGetCharacter(other, out Character character) &&
                !DoEvaluateConditions(character))
                m_collider.isTrigger = false;
        }

        // 0x060028d6. Read Collision.collider before resolving the manager.
        private void OnCollisionExit(Collision other)
        {
            if (TryGetCharacter(other.collider, out Character character))
                m_collider.isTrigger = true;
        }

        // 0x060028d7. Original abstract virtual declaration has no native body.
        public abstract bool DoEvaluateConditions(Character character);

        // 0x060028d8. Original real base initializer supplies the manager reference.
        protected CharacterColliderModifier() { }
    }
}
