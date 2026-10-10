using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Complete original020003be. Private source candidate; its genuine Character,
    // CharacterTracker and movement-definition graph is still unclosed.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public abstract class CharacterTracking
    {
        protected readonly Character m_character;

        protected CharacterTracking(Character character) { m_character = character; } //060016c7
        public abstract CharacterTrackingType Type { get; } //c8: genuine pointer-zero getter.
        public abstract bool IsOnGround(); //c9
        public abstract bool WasOnGround(); //ca
        public abstract Vector3 GetContactNormal(); //cb
        public abstract bool GetProjectedLandingPosition(float maximumDistance, out Quaternion hitRotation, out float hitTime); //cc
        public abstract bool RaycastOnTrack(Ray ray, float distance, out RaycastHit raycastHit); //cd
        public abstract bool RaycastOnCollision(Ray ray, float distance, out RaycastHit raycastHit); //ce
        public abstract void ProcessMovement(CharacterAbilityDefinition_MovementGround abilityDef, float deltaTime); //cf
        public abstract void OrientateToGround(); //d0
        public abstract void GetSteeringInput(CharacterAbilityDefinition_MovementFree abilityDef, float deltaTime,
            out Vector3 inputForward, out float inputTurn); //d1

        //060016d2: preserve virtual dispatch with null ability and zero elapsed time.
        public void GetSteeringInput(out Vector3 inputForward, out float inputTurn)
        {
            GetSteeringInput(null, 0f, out inputForward, out inputTurn);
        }

        public virtual void OnEnter() { } //060016d3: genuine native RET.
        public virtual void OnLeave() { } //060016d4: genuine native RET.

        //060016d5: update before rereading settings; retain the tracker receiver
        // before the value-returning TrackerLocation call for the bounds operation.
        protected Vector3 UpdateTrackerAndClampToBounds(CharacterAbilityDefinition_MovementGround abilityDef,
            Vector3 worldVelocity, float deltaTime)
        {
            m_character.Tracker.UpdateColliderTracker(worldVelocity, abilityDef.SplineAngleInfluence, deltaTime);
            if (m_character.Settings.Surface.ClampToSurfaceBoundsOnGround)
            {
                CharacterTracker tracker = m_character.Tracker;
                worldVelocity = tracker.ClampVelocityToLateralBounds(worldVelocity, tracker.TrackerLocation);
            }
            return worldVelocity;
        }

        //060016d6: steering remains in the character's local space. The authored
        // graph inversion is queried after output-forward publication and curve
        // evaluation. Missing graph values are stored through the original API.
        protected void GetSteeringInputOnTracker(AnimationCurve angleInfluenceCurve, float angleInfluence,
            AnimationCurve turnAngleMaxCurve, float deltaTime, out Vector3 inputForward, out float inputTurn)
        {
            Vector3 forward = Vector3.forward;
            Quaternion trackerRotation = m_character.Tracker.TrackerRotation;
            Vector3 trackerWorldForward = trackerRotation * Vector3.forward;
            Vector3 trackerForward = m_character.WorldToLocalRotation * trackerWorldForward;
            Quaternion trackerHeading = Quaternion.FromToRotation(forward, new Vector3(trackerForward.x, 0f, trackerForward.z));
            float trackerAngle = Hardlight.QuaternionExtensions.SignedAngle(trackerHeading, Quaternion.identity, Vector3.up);
            if (angleInfluenceCurve != null)
                angleInfluence = angleInfluenceCurve.Evaluate(Mathf.Abs(trackerAngle));
            inputForward = new Vector3(0f, 0f, m_character.ControllerMovementMagnitude);
            float inputAngle = Vector2.SignedAngle(m_character.ControllerMovement, Vector2.up);
            float turnAngleMax = inputAngle;
            if (turnAngleMaxCurve != null)
                turnAngleMax = turnAngleMaxCurve.Evaluate(Mathf.Abs(inputAngle));
            IGraphStorage storage = m_character.Storage;
            float clampedInput = Mathf.Clamp(inputAngle, -turnAngleMax, turnAngleMax);
            float trackerInfluence = Mathf.Lerp(0f, trackerAngle, angleInfluence * deltaTime);
            bool inputInverted = storage.GetValue<bool>(ActorFSMKeys.TurnInputInverted, false, true);
            inputTurn = clampedInput - (inputInverted ? -trackerInfluence : trackerInfluence);
        }
    }
}
