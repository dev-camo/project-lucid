using Hardlight;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 02000851; the constructor 0x60DAC0 chains to ViewParameters and stores both references.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class ViewBossAbilityInfoParameters : ViewParameters
    {
        public readonly Boss Boss;
        public readonly ActorAbility Ability;

        // 06003028 (1.10.1 RVA 0x60DAC0)
        public ViewBossAbilityInfoParameters(Boss boss, ActorAbility ability)
        {
            Boss = boss;
            Ability = ability;
        }
    }
}
