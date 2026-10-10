using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class CharacterTrackingSurfaceTracker : CharacterTrackingSurface //020003c3, zero own fields.
    {
        public override CharacterTrackingType Type { get { return CharacterTrackingType.SurfaceTracker; } } //060016ef
        public CharacterTrackingSurfaceTracker(Character character) : base(character) { } //f0
        public override void ProcessMovement(CharacterAbilityDefinition_MovementGround abilityDef, float deltaTime)
        {
            m_character.Tracker.ApplyVerticalVelocity(0f);
            m_character.Tracker.UpdateOnTrackerRibbon(deltaTime);
        } //060016f1: the ability argument is genuinely unused.

        public override void OrientateToGround() //060016f2: capture velocity before rotation.
        {
            Vector2 velocity = m_character.Tracker.TrackerVelocityLocal.xz();
            Quaternion rotation = m_character.Tracker.TrackerRotation;
            bool adjustVelocity = velocity.sqrMagnitude > 0.0001f;
            Vector3 up = rotation * Vector3.up;
            m_character.OrientateToPlane(up, adjustVelocity);
        }

        public override void GetSteeringInput(CharacterAbilityDefinition_MovementFree abilityDef, float deltaTime,
            out Vector3 inputForward, out float inputTurn)
        {
            AnimationCurve angleInfluenceCurve = abilityDef != null ? abilityDef.SplineAngleInfluence : null;
            AnimationCurve turnAngleMaxCurve = abilityDef != null ? abilityDef.SplineTurnAngleMax : null;
            GetSteeringInputOnTracker(angleInfluenceCurve, 0f, turnAngleMaxCurve, deltaTime, out inputForward, out inputTurn);
        } //060016f3: each curve uses its own genuine Unity liveness query.
    }
}
