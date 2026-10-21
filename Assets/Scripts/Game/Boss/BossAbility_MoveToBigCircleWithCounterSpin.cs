using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 020001e4; bodies translated from the 1.10.1 x86-64 binary (0x6FBAC0..0x6FBDE0).
    // Glides each boss to its own spot on a small counter-spinning circle that sits on the big circle.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class BossAbility_MoveToBigCircleWithCounterSpin : BossAbility_MoveTowardsTarget
    {
        public BossAbilityDefinition_MoveToBigCircleWithCounterSpin DerivedDefinition;

        public override AbilityDefinition DefinitionBase => Definition; // 06000ab8

        // 06000ab9: the base stores the base definition; the derived one is a second `as` cast of the same object.
        public override void Initialise(Actor boss, AbilityDefinition definition)
        {
            base.Initialise(boss, definition);
            m_boss = boss as Boss;
            DerivedDefinition = definition as BossAbilityDefinition_MoveToBigCircleWithCounterSpin;
        }

        // 06000aba: the small circle's centre is the big circle's point at angle zero; each boss takes the point of its
        // index times pi on the small circle. The native body evaluates the big-circle point once more and discards it,
        // and has the base entry inlined after the target is set.
        protected override void DoOnEnter()
        {
            Vector3 smallCircleCentre = ArenaCentre.PointOnCircleWithFixedY(0f, DerivedDefinition.BigCircleRadius, m_boss.WorldPosition.y);
            int index = ProcessManager.GetSystem<FourmenManager>().GetIndexForBoss(m_boss);
            float angle = index * Mathf.PI;
            m_targetPosition = smallCircleCentre.PointOnCircleWithFixedY(angle, DerivedDefinition.CounterSpinRadius, m_boss.WorldPosition.y);
            base.DoOnEnter();
        }
    }
}
