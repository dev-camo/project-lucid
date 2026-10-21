using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 020001f0; bodies translated from the 1.10.1 x86-64 binary (0x6FF5E0..0x6FF8E0).
    // Spins the boss on the spot: a curve over the spin time gives the angle, finished once the stated number of
    // rotations is done and the angle lies within the tolerance of the desired end rotation.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class BossAbility_SpinOnSpot : ActorAbility
    {
        private const int DegreesInCircle = 360;

        public BossAbilityDefinition_SpinOnSpot Definition;

        private Boss m_boss;
        private Vector3 m_bossEntryPoint;
        private float m_rotationInDegrees;
        private float m_spunTime;
        private float m_spunTimeNormalised;
        private float m_rotationPreviousTick;
        private int m_rotationsCompleted;
        private int m_rotationInDegreesComparison;

        public override AbilityDefinition DefinitionBase => Definition; // 06000b3b
        public float RotationInDegrees => m_rotationInDegrees; // 06000b3c
        public int RotationsCompleted => m_rotationsCompleted; // 06000b3d

        // 06000b3e: both casts are `as`; a null boss or definition stores null.
        public override void Initialise(Actor boss, AbilityDefinition definition)
        {
            base.Initialise(boss, definition);
            m_boss = boss as Boss;
            Definition = definition as BossAbilityDefinition_SpinOnSpot;
        }

        // 06000b3f: the spin restarts from zero after the base entry.
        protected override void DoOnEnter()
        {
            base.DoOnEnter();
            m_rotationInDegrees = 0f;
            m_spunTime = 0f;
            m_rotationPreviousTick = 0f;
            m_rotationsCompleted = 0;
        }

        // 06000b40
        protected override void DoOnLeave()
        {
            base.DoOnLeave();
            AbilityFinished = false;
        }

        // 06000b41: the curve value is a fraction of a full turn; each tick turns by the change in that angle, always in
        // the spin direction. The whole-rotation count is the truncated normalised spin time. When the ability finishes
        // the angle snaps to the desired end rotation.
        protected override void DoOnUpdate(float deltaTime)
        {
            base.DoOnUpdate(deltaTime);
            m_spunTime += deltaTime;
            float spun = m_spunTime / Definition.TimeToFullyRotateInSeconds;
            float rotation = Mathf.Lerp(0f, DegreesInCircle, Definition.SpinRotationCurve.Evaluate(spun));
            float step = Mathf.Abs(rotation - m_rotationPreviousTick);
            if (!Definition.Clockwise)
                step = -step;
            m_rotationInDegrees = (step + m_rotationInDegrees) % DegreesInCircle;
            m_rotationInDegreesComparison = (int)m_rotationInDegrees;
            m_rotationsCompleted = (int)spun;
            m_rotationPreviousTick = rotation;

            BossAbilityDefinition_SpinOnSpot definition = Definition;
            AbilityFinished = m_rotationInDegreesComparison <= definition.DesiredEndRotation + definition.DesiredEndRotationToleranceInDegrees
                && definition.DesiredEndRotation - definition.DesiredEndRotationToleranceInDegrees <= m_rotationInDegreesComparison
                && definition.RotationsUntilComplete <= m_rotationsCompleted;
            if (AbilityFinished)
                m_rotationInDegrees = definition.DesiredEndRotation;
        }
    }
}
