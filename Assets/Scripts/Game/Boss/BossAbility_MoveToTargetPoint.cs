using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 020001e5; bodies translated from the 1.10.1 x86-64 binary (0x6FBDE0..0x6FC290).
    // Glides the boss to a fixed point, or back to where it started (the origin point stored in its FSM storage).
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class BossAbility_MoveToTargetPoint : BossAbility_MoveTowardsTarget
    {
        public BossAbilityDefinition_MoveToTarget DerivedDefinition;

        private float m_movementTimer;
        private float m_decelerationTimer;

        public override AbilityDefinition DefinitionBase => Definition; // 06000abc
        public override float TurnSpeed => DerivedDefinition.TurnSpeed; // 06000abd

        // 06000abe: the base stores the base definition; the derived one is a second `as` cast of the same object.
        // The target is chosen once, here.
        public override void Initialise(Actor boss, AbilityDefinition definition)
        {
            base.Initialise(boss, definition);
            m_boss = boss as Boss;
            DerivedDefinition = definition as BossAbilityDefinition_MoveToTarget;
            if (DerivedDefinition.TargetIsBossOrigin)
                m_targetPosition = m_boss.Storage.GetValue(BossFSMKeys.OriginPoint, default(Vector3), true);
            else
                m_targetPosition = DerivedDefinition.Target;
        }

        // 06000abf: the native body repeats the base leave's resets after calling it.
        protected override void DoOnLeave()
        {
            base.DoOnLeave();
            AbilityFinished = false;
            m_boss.Storage.SetValue(BossFSMKeys.DestinationReachedTimer, 0f);
            m_reachedTarget = false;
        }
    }
}
