using Hardlight;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public abstract class CharacterAbility<T> : CharacterAbility where T : CharacterAbilityDefinition
    {
        private T m_definition;
        private T m_definitionOverride;

        public T Definition => m_definitionOverride != null ? m_definitionOverride : m_definition;
        public override AbilityDefinition DefinitionBase => Definition;
        public override CharacterAbilityDefinition CharacterAbilityDefinition => Definition;

        public override void Initialise(Actor actor, AbilityDefinition definition)
        {
            m_definition = definition as T;
            if (m_definition == null) return;
            base.Initialise(actor, definition);
        }

        // Original06000fc6 clears the cached override before the storage read.
        // A missing entry leaves the current gravity multiplier untouched.
        public override void SetDefinitionOverride()
        {
            m_definitionOverride = null;
            if (!m_character.Storage.TryGetValue(ActorFSMKeys.AbilityDefinitionOverride,
                out CharacterAbilityDefinition definition)) return;
            m_definitionOverride = definition as T;
            if (m_definitionOverride == null)
                m_character.Storage.RemoveValue<CharacterAbilityDefinition>(ActorFSMKeys.AbilityDefinitionOverride);
            else
                GravityMultiplier = m_definitionOverride.GravityMultiplier;
        }

        protected CharacterAbility() { }
    }
}
