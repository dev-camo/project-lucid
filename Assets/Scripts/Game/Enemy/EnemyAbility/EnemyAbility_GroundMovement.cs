using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 02000636; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class EnemyAbility_GroundMovement : EnemyAbility_Movement<EnemyAbilityDefinition_GroundMovement>
    {
        // 06002182 (1.10.1 RVA 0x570550)
        public EnemyAbility_GroundMovement() { }
    }
}
