using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class CharacterTrackingColliderTracker : CharacterTrackingCollider //020003c1, zero own fields.
    {
        public override CharacterTrackingType Type { get { return CharacterTrackingType.ColliderTracker; } } //060016e3
        public CharacterTrackingColliderTracker(Character character) : base(character) { } //e4
        public override void OnLeave() { m_character.Storage.SetValue(ActorFSMKeys.TurnTrackerActive, false); } //e5

        //060016e6: include the current direction modifier in tracking, then subtract
        // its reread value before publishing velocity. A missing ribbon changes
        // mode and returns before writing TurnTrackerActive.
        public override void ProcessMovement(CharacterAbilityDefinition_MovementGround abilityDef, float deltaTime)
        {
            Vector3 velocity = m_character.WorldVelocity + m_character.AppliedConstantDirectionModifier;
            velocity = UpdateTrackerAndClampToBounds(abilityDef, velocity, deltaTime);
            m_character.SetWorldVelocity(velocity - m_character.AppliedConstantDirectionModifier);
            if (m_character.Collider.LinkedRibbon == null)
            {
                m_character.SetTrackingType(CharacterTrackingType.ColliderFree);
                return;
            }
            CharacterCollisionData collisionData = m_character.Collider.LinkedCollisionData;
            bool turnTrackerActive = collisionData != null && collisionData.HasTrackerForward();
            m_character.Storage.SetValue(ActorFSMKeys.TurnTrackerActive, turnTrackerActive);
        }

        //060016e7: metadata can replace both curve inputs; a missing permission
        // value defaults to true. Preserve repeated Unity liveness checks and the
        // dictionary indexer (a missing definition is not silently normalized).
        public override void GetSteeringInput(CharacterAbilityDefinition_MovementFree abilityDef, float deltaTime,
            out Vector3 inputForward, out float inputTurn)
        {
            float angleInfluence = 0f;
            bool turnAngleCanBeOverriden = false;
            TerrainTrackerType terrainTrackerType = default;
            CharacterCollisionData collisionData = m_character.Collider.LinkedCollisionData;
            if (collisionData == null || !collisionData.HasTracker)
            {
                inputForward = new Vector3(0f, 0f, m_character.ControllerMovementMagnitude);
                inputTurn = Vector2.SignedAngle(m_character.ControllerMovement, Vector2.up);
                return;
            }
            TerrainTrackerMetadataKeyLookupDefinition keys = m_character.Collider.TerrainTrackerMetadataKeyLookup;
            AnimationCurve angleInfluenceCurve = abilityDef != null ? abilityDef.SplineAngleInfluence : null;
            if (collisionData.TrackerMetadata.TryGetValue(keys.SplineAngleInfluenceMetadataKey, out angleInfluence))
                angleInfluenceCurve = null;
            bool mayOverride;
            if (abilityDef == null || abilityDef.SplineTurnAngleCanBeOverriden)
                mayOverride = !collisionData.TrackerMetadata.TryGetValue(keys.SplineTurnAngleCanBeOverridenMetadataKey,
                    out turnAngleCanBeOverriden) || turnAngleCanBeOverriden;
            else
                mayOverride = false;
            AnimationCurve turnAngleMaxCurve = abilityDef != null ? abilityDef.SplineTurnAngleMax : null;
            if (mayOverride && collisionData.TrackerMetadata.TryGetValue<TerrainTrackerType>(
                keys.SplineTurnAngleMaxMetadataKey, out terrainTrackerType))
                turnAngleMaxCurve = ProcessManager.GetSystem<DataManager>(null, true)
                    .TerrainTrackerDefinitions[terrainTrackerType].SplineTurnAngleMax;
            GetSteeringInputOnTracker(angleInfluenceCurve, angleInfluence, turnAngleMaxCurve, deltaTime,
                out inputForward, out inputTurn);
        }
    }
}
