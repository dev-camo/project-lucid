using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 020001de; bodies translated from the 1.10.1 x86-64 binary (0x6F99B0..0x6F9DD0).
    // The defeated boss flies off along the character's facing, with the height taken from a curve over flight time.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class BossAbility_DeathFlight : ActorAbility
    {
        public BossAbilityDefinition_DeathFlight Definition;

        private Boss m_boss;
        private Vector3 m_bossPosition;
        private Vector3 m_characterPosition;
        private Vector3 m_relativeDirection;
        private Vector3 m_currentTargetPosition;
        private float m_flightTime;

        public override AbilityDefinition DefinitionBase => Definition; // 06000a79
        public Vector3 CurrentTargetPosition => m_currentTargetPosition; // 06000a7a

        // 06000a7b: both casts are `as`; a null boss or definition stores null.
        public override void Initialise(Actor boss, AbilityDefinition definition)
        {
            base.Initialise(boss, definition);
            m_boss = boss as Boss;
            Definition = definition as BossAbilityDefinition_DeathFlight;
        }

        // 06000a7c: the flight direction is the current character's normalised forward direction.
        protected override void DoOnEnter()
        {
            m_bossPosition = m_boss.WorldPosition;
            m_relativeDirection = ProcessManager.GetSystem<CharacterManager>().GetCurrentCharacterUnsafe().ForwardDirection.normalized;
            m_currentTargetPosition = new Vector3(
                m_relativeDirection.x + m_bossPosition.x,
                Definition.HeightCurve.Evaluate(m_flightTime),
                m_relativeDirection.z + m_bossPosition.z);
            base.DoOnEnter();
        }

        // 06000a7d
        protected override void DoOnLeave()
        {
            base.DoOnLeave();
            AbilityFinished = false;
            m_flightTime = 0f;
        }

        // 06000a7e: the distance factor is the speed for this step plus the acceleration times the elapsed flight time.
        protected override void DoOnUpdate(float deltaTime)
        {
            base.DoOnUpdate(deltaTime);
            m_flightTime += deltaTime;
            float height = Definition.HeightCurve.Evaluate(m_flightTime);
            float step = deltaTime * Definition.MovementRateMetersPerSecond;
            float acceleration = Definition.AccelerationMetersPerSecond * m_flightTime;
            float distance = step + acceleration;
            float x = m_relativeDirection.x * distance + m_boss.WorldPosition.x;
            float z = m_relativeDirection.z * distance + m_boss.WorldPosition.z;
            m_currentTargetPosition = new Vector3(x, height, z);
        }
    }
}
