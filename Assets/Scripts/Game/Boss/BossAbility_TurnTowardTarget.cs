using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 020001f2; bodies translated from the 1.10.1 x86-64 binary (0x700640..0x7008D0).
    // Turns the boss in place: the movement target is the offset from the boss to the character.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class BossAbility_TurnTowardTarget : ActorAbility, IAbilityBossMovement
    {
        public BossAbilityDefinition_MovementTurnTowardTarget Definition;

        private Boss m_boss;
        private Vector3 m_currentTargetPosition;

        public override AbilityDefinition DefinitionBase => Definition; // 06000b54
        public Vector3 CurrentTargetPosition => m_currentTargetPosition; // 06000b55
        public float MovementAnimationAngleMultiplier => 0f; // 06000b56
        public float FaceAngleTolerance => Definition.FaceAngleTolerance; // 06000b57
        public float TurnSpeed => Definition.TurnSpeed; // 06000b58
        public bool IsMomentum => false; // 06000b59
        public bool TurnToCharacter => true; // 06000b5a

        // 06000b5b: both casts are `as`; a null boss or definition stores null.
        public override void Initialise(Actor boss, AbilityDefinition definition)
        {
            base.Initialise(boss, definition);
            m_boss = boss as Boss;
            Definition = definition as BossAbilityDefinition_MovementTurnTowardTarget;
        }

        // 06000b5c
        protected override void DoOnUpdate(float deltaTime)
        {
            base.DoOnUpdate(deltaTime);
            m_currentTargetPosition = ProcessManager.GetSystem<CharacterManager>().GetCurrentCharacterUnsafe().WorldPosition
                - m_boss.WorldPosition;
        }
    }
}
