using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 020001df; bodies translated from the 1.10.1 x86-64 binary (0x6F9DD0..0x6F9EE0).
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class BossAbility_LaserFence : ActorAbility
    {
        public BossAbilityDefinition_LaserFence Definition;

        public override AbilityDefinition DefinitionBase => Definition; // 06000a80

        // 06000a81: the definition cast is `as`; anything else stores null.
        public override void Initialise(Actor boss, AbilityDefinition definition)
        {
            base.Initialise(boss, definition);
            Definition = definition as BossAbilityDefinition_LaserFence;
        }

        public float GetHazardDepth() => Definition.HazardDepth; // 06000a82
        public float GetHazardHeight() => Definition.HazardHeight; // 06000a83
        public float GetHazardYOffset() => Definition.HazardYOffset; // 06000a84
    }
}
