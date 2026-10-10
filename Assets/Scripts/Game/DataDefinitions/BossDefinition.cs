using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Whole original Game.Runtime 0200049a, methods06001a66..a68.
    [CreateAssetMenu(fileName = "BossDefinition", menuName = "HardlightProject/DefinitionData/Definitions/BossDefinition")]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class BossDefinition : ActorDefinition
    {
        public BossTraits Traits;

        // 06001a66..a67 both dispatch the real ActorDefinition virtual slot7.
        // The original validation override does not call base.OnValidate.
        protected override void OnValidate() => UpdateCachedValues();
        protected void OnEnable() => UpdateCachedValues();
        // 06001a68 contains only the genuine ActorDefinition base constructor.
    }
}
