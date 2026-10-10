using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class CharacterTrackingColliderFree : CharacterTrackingCollider //020003c0, zero own fields.
    {
        public override CharacterTrackingType Type { get { return CharacterTrackingType.ColliderFree; } } //060016df
        public CharacterTrackingColliderFree(Character character) : base(character) { } //060016e0

        //060016e1: this method only detects a linked ribbon and selects the authored
        // tracked-collider mode. It does not introduce another movement controller.
        public override void ProcessMovement(CharacterAbilityDefinition_MovementGround abilityDef, float deltaTime)
        {
            if (m_character.Collider.LinkedRibbon != null)
                m_character.SetTrackingType(CharacterTrackingType.ColliderTracker);
        }

        //060016e2: the ability and deltaTime arguments are genuinely unused.
        public override void GetSteeringInput(CharacterAbilityDefinition_MovementFree abilityDef, float deltaTime,
            out Vector3 inputForward, out float inputTurn)
        {
            inputForward = new Vector3(0f, 0f, m_character.ControllerMovementMagnitude);
            inputTurn = Vector2.SignedAngle(m_character.ControllerMovement, Vector2.up);
        }
    }
}
