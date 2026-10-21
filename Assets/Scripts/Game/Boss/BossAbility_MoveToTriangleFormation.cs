using Hardlight;
using System.Diagnostics;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 020001e6; bodies translated from the 1.10.1 x86-64 binary (0x6FC290..0x6FC5F0).
    // Glides the bosses into an evenly spaced formation around the centroid of all bosses.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class BossAbility_MoveToTriangleFormation : BossAbility_MoveTowardsTarget
    {
        public BossAbilityDefinition_MoveToTriangleFormation DerivedDefinition;

        public override AbilityDefinition DefinitionBase => Definition; // 06000ac1

        // 06000ac2: the base stores the base definition; the derived one is a second `as` cast of the same object.
        public override void Initialise(Actor boss, AbilityDefinition definition)
        {
            base.Initialise(boss, definition);
            m_boss = boss as Boss;
            DerivedDefinition = definition as BossAbilityDefinition_MoveToTriangleFormation;
        }

        // 06000ac3: the bosses divide the circle evenly in order of their index. SetDebugPositions is conditional on
        // BUILD_DEVELOPMENT and its call, if there was one, is not in the retail body. The base entry is inlined natively.
        protected override void DoOnEnter()
        {
            FourmenManager fourmen = ProcessManager.GetSystem<FourmenManager>();
            Vector3 centroid = fourmen.GetAveragePosition();
            float angleStep = Mathf.PI * 2f / fourmen.BossCount();
            float angle = fourmen.GetIndexForBoss(m_boss) * angleStep;
            m_targetPosition = centroid.PointOnCircleWithFixedY(angle, DerivedDefinition.StartingRadius, m_boss.WorldPosition.y);
            base.DoOnEnter();
        }

        // 06000ac4
        [Conditional("BUILD_DEVELOPMENT")]
        private void SetDebugPositions(Vector3 currentBossCentroid)
        {
            m_boss.m_movementTargetingDebugBoxCenter.gameObject.SetActive(true);
            m_boss.m_movementTargetingDebugBoxCenter.position = currentBossCentroid;
        }
    }
}
