using UnityEngine;

namespace HardlightProject
{
    // Original readonly value type02000a38 / constructor06003a79.
    public readonly struct TurningMovementInput
    {
        public readonly Character Character;
        public readonly Vector3 InputForward;
        public readonly float InputTurn;
        public readonly float ForwardSpeed;
        public readonly CharacterTraits.TurnTraits TurnTraits;
        public readonly float DeltaTime;

        public TurningMovementInput(Character character, Vector3 inputForward, float inputTurn,
            float forwardSpeed, CharacterTraits.TurnTraits turnTraits, float deltaTime)
        {
            Character = character;
            InputForward = inputForward;
            InputTurn = inputTurn;
            ForwardSpeed = forwardSpeed;
            TurnTraits = turnTraits;
            DeltaTime = deltaTime;
        }
    }
}
