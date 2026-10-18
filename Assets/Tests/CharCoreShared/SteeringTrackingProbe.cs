using System;
using HardlightProject;
using UnityEngine;

namespace ProjectLucid.Tests.CharCoreShared
{
    // Test-only CharacterTracking (abstract) that reports a scripted steering input and
    // refuses everything else.
    public sealed class SteeringTrackingProbe : CharacterTracking
    {
        public Vector3 InputForward;
        public float InputTurn;
        public int SteeringReads;

        public SteeringTrackingProbe(Character character) : base(character) { }

        public override CharacterTrackingType Type => CharacterTrackingType.ColliderFree;

        public override void GetSteeringInput(CharacterAbilityDefinition_MovementFree abilityDef, float deltaTime,
            out Vector3 inputForward, out float inputTurn)
        {
            SteeringReads++;
            inputForward = InputForward;
            inputTurn = InputTurn;
        }

        public override bool IsOnGround() => throw new NotSupportedException();
        public override bool WasOnGround() => throw new NotSupportedException();
        public override Vector3 GetContactNormal() => throw new NotSupportedException();
        public override bool GetProjectedLandingPosition(float maximumDistance, out Quaternion hitRotation, out float hitTime) =>
            throw new NotSupportedException();
        public override bool RaycastOnTrack(Ray ray, float distance, out RaycastHit raycastHit) => throw new NotSupportedException();
        public override bool RaycastOnCollision(Ray ray, float distance, out RaycastHit raycastHit) => throw new NotSupportedException();
        public override void ProcessMovement(CharacterAbilityDefinition_MovementGround abilityDef, float deltaTime) =>
            throw new NotSupportedException();
        public override void OrientateToGround() => throw new NotSupportedException();
    }
}
