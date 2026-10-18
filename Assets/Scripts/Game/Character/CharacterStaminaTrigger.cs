using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime type 0200034e. Base of the triggers that act on one of the
    // character's stamina pools, bound to the character's ability of the chosen type.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public abstract class CharacterStaminaTrigger<T> : CharacterTrigger
        where T : class
    {
        [Tooltip("Ability to use for stamina definition.")]
        [SerializeField]
        protected ActorAbilityType m_abilityType;

        // 060014e8 / 060014e9
        protected T Ability { get; private set; }

        // 060014ea
        protected abstract CharacterStamina Stamina { get; }

        // 060014eb
        private void Start() => m_characterManagerRef.Get().RegisterOnCharacterChange(OnCharacterChange, true);

        // 060014ec; the manager may already be gone when the scene is torn down.
        private void OnDestroy()
        {
            if (m_characterManagerRef.IsNull())
                return;
            m_characterManagerRef.Get().ReleaseOnCharacterChange(OnCharacterChange);
        }

        // 060014ed; the native body asks the stamina about this object's Collectable type and
        // discards the answer, so a missing Collectable component faults here.
        private void OnCharacterChange(Character character)
        {
            Collectable collectable = GetComponent<Collectable>();
            Stamina.IsCollectableType(collectable.Type);
            character.TryGetAbilityFromType(m_abilityType, out ActorAbility ability);
            Ability = ability as T;
        }

        // 060014ee
        protected CharacterStaminaTrigger() { }
    }
}
