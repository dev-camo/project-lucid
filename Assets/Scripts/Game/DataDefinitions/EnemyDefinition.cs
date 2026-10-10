using UnityEngine;

namespace HardlightProject
{
    [UnityEngine.CreateAssetMenuAttribute(fileName = "EnemyDefinition", menuName = "HardlightProject/DefinitionData/Definitions/EnemyDefinition")]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
    public class EnemyDefinition : ActorDefinition
    {
        [UnityEngine.TooltipAttribute("State machine representing enemy's thought process and control impulses.")]
        public Hardlight.FiniteStateMachineScriptableObject FSMBrain;

        public HardlightProject.EnemyTraits Traits;

        public HardlightProject.EnemyType Type;

        // Original 06001c9a completes the genuine base cache callback before
        // loading Traits and refreshing its original cache values.
        protected override void UpdateCachedValues()
        {
            base.UpdateCachedValues();
            Traits.UpdateCachedValues();
        }

        public EnemyDefinition() { }
    }
}
