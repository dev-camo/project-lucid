using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public abstract class CharacterTrackingSurface : CharacterTracking //020003c2, zero own fields.
    {
        protected CharacterTrackingSurface(Character character) : base(character) { } //060016e8
        public override bool IsOnGround() { return m_character.Tracker.LookAheadIsOnTracker(); } //e9
        public override bool WasOnGround() { return m_character.Tracker.LookAheadWasOnTracker(); } //ea
        public override Vector3 GetContactNormal() { return m_character.Tracker.TrackerLocation.m_worldRotation * Vector3.up; } //eb
        public override bool GetProjectedLandingPosition(float maximumDistance, out Quaternion hitRotation, out float hitTime)
        {
            return m_character.Tracker.GetProjectedSurfaceCollision(maximumDistance, out hitRotation, out hitTime);
        } //060016ec

        public override bool RaycastOnTrack(Ray ray, float distance, out RaycastHit raycastHit)
        {
            ISurface hitSurface = null;
            return SurfacePhysics.Raycast(ray, distance, m_character.Tracker.Surfaces,
                out raycastHit, out hitSurface, null, true);
        } //060016ed: the real surface result is local and ignored after the call.

        public override bool RaycastOnCollision(Ray ray, float distance, out RaycastHit raycastHit)
        {
            return RaycastOnTrack(ray, distance, out raycastHit);
        } //060016ee: virtual dispatch is preserved, not a base-qualified call.
    }
}
