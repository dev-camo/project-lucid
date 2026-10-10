using Hardlight.Enums;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public sealed class MetaGameUnlockCharacterArchetype : MetaGameUnlockBase
    {
        // Original06002a74: genuine private readonly auto-property and generated getter marker.
        private CharacterArchetypeDefinition m_archetypeDefinition { get; }

        // Original06002a75.
        public MetaGameUnlockCharacterArchetype(CharacterArchetypeDefinition archetypeDefinition)
        {
            m_archetypeDefinition = archetypeDefinition;
        }

        // Original06002a76/77/78: original archetype string, key0x6fdc5af9, and widget.
        public override string GetId() => m_archetypeDefinition.CharacterArchetype.GetString();
        public override PlayerProgressionTypes GetProgressionType() => PlayerProgressionTypes.CharacterArchetype;
        public override UIWidgetProgression GetWidget() => m_archetypeDefinition.ProgressionUnlockWidget;

        // Original06002a79: UnlockSeen@0x19 marks the record dirty before the manager request.
        public override void Save(SaveManager saveManager)
        {
            saveManager.CurrentSave.GetOrCreateCharacterArchetypeData(m_archetypeDefinition.CharacterArchetype).UnlockSeen = true;
            saveManager.RequestSave();
        }
    }
}
