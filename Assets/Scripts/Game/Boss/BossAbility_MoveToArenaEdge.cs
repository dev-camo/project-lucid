using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 020001e3; bodies translated from the 1.10.1 x86-64 binary (0x6FB520..0x6FB950).
    // Glides the boss to the arena edge, on the circle of the stated radius around the arena centre.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class BossAbility_MoveToArenaEdge : BossAbility_MoveTowardsTarget
    {
        public BossAbilityDefinition_MoveToArenaEdge DerivedDefinition;

        public override AbilityDefinition DefinitionBase => Definition; // 06000ab4

        // 06000ab5: the base stores the base definition; the derived one is a second `as` cast of the same object.
        public override void Initialise(Actor boss, AbilityDefinition definition)
        {
            base.Initialise(boss, definition);
            m_boss = boss as Boss;
            DerivedDefinition = definition as BossAbilityDefinition_MoveToArenaEdge;
        }

        // 06000ab6: the target is the point at -90 degrees on the edge circle, at the boss's height. The native body
        // has the base entry inlined after the target is set.
        protected override void DoOnEnter()
        {
            m_targetPosition = ArenaCentre.PointOnCircleWithFixedY(-Mathf.PI / 2f, DerivedDefinition.Radius, m_boss.WorldPosition.y);
            base.DoOnEnter();
        }
    }
}
