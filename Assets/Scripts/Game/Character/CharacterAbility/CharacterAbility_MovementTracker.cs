using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public abstract class CharacterAbility_MovementTracker<TAbilityDef, TTrackerDef> : CharacterAbility_Movement<TAbilityDef>
        where TAbilityDef : CharacterAbilityDefinition_Movement
        where TTrackerDef : TrackerMetadataDefinition
    {
        private readonly SystemRef<DataManager> m_dataManagerRef = ProcessManager.GetSystemRef<DataManager>(null, true);
        private readonly SystemRef<CinemachineCameraManager> m_cameraManagerRef = ProcessManager.GetSystemRef<CinemachineCameraManager>(null, true);
        private MetadataGroup m_trackerMetadata;
        private TrackerEnterDefinition m_enterDefinition;
        private TrackerExitDefinition m_exitDefinition;
        private float m_exitStartSpeed;
        private TrackerMotionDefinition m_motionDefinition;
        private TrackerCameraDefinition m_cameraDefinition;
        private readonly CameraHeadingOverride m_headingOverride = new CameraHeadingOverride();
        private StackableDataHandle m_cameraOverrideHandle;
        private bool m_allowForward;
        private bool m_allowBackward;

        protected DataManager DataManager => m_dataManagerRef.Get();
        protected override void DoOnUpdate(CharacterBrain brain, float deltaTime)
        {
            base.DoOnUpdate(brain, deltaTime);
            if (CanTurnAround) UpdateFromTrackerMetadata();
        }
        protected override void DoOnLeave()
        {
            base.DoOnLeave();
            RemoveCameraOverride();
        }
        public override void Close()
        {
            base.Close();
            RemoveCameraOverride();
        }

        // Original 060011ad: remove the previous handle before querying metadata.
        public virtual void OnSurfaceChanged(ISurface surface)
        {
            RemoveCameraOverride();
            m_exitStartSpeed = 0f;
            if (!TryGetSurfaceMetadata(surface)) return;
            CheckForInstantTurnAround();
            if (m_cameraDefinition == null) return;
            CameraHeadingOverride headingOverride = m_headingOverride;
            headingOverride.Target = m_character.transform;
            m_headingOverride.Settings = m_character.Tracker.SurfaceReversed
                ? m_cameraDefinition.BackwardHeadingOverride : m_cameraDefinition.ForwardHeadingOverride;
            m_cameraOverrideHandle = m_cameraManagerRef.Get().ApplyCameraSettingOverride(CameraSettingType.HeadingOverride, m_headingOverride);
        }
        private bool TryGetSurfaceMetadata(ISurface surface)
        {
            TTrackerDef definition = GetTrackerDefinition();
            m_trackerMetadata = surface?.GetSurfaceMetadata().GetGroup(definition.MetadataGroupKey);
            bool hasMetadata = m_trackerMetadata != null;
            if (hasMetadata)
            {
                m_trackerMetadata.TryGetObject(definition.MetadataEnterKey, out m_enterDefinition);
                m_trackerMetadata.TryGetObject(definition.MetadataExitKey, out m_exitDefinition);
                m_trackerMetadata.TryGetObject(definition.MetadataMotionKey, out m_motionDefinition);
                m_trackerMetadata.TryGetObject(definition.MetadataCameraKey, out m_cameraDefinition);
                UpdateFromTrackerMetadata();
            }
            else
            {
                m_allowForward = true;
                m_allowBackward = true;
                CanTurnAround = true;
                m_enterDefinition = null;
                m_exitDefinition = null;
                m_motionDefinition = null;
                m_cameraDefinition = null;
            }
            return hasMetadata;
        }
        private void UpdateFromTrackerMetadata()
        {
            m_allowForward = true;
            m_allowBackward = true;
            CanTurnAround = true;
            if (m_trackerMetadata == null) return;
            TTrackerDef definition = GetTrackerDefinition();
            if (!m_trackerMetadata.TryGetValue(definition.MetadataForwardKey, out m_allowForward)) m_allowForward = true;
            if (!m_trackerMetadata.TryGetValue(definition.MetadataBackwardKey, out m_allowBackward)) m_allowBackward = true;
            CharacterTracker tracker = m_character.Tracker;
            bool allowForward = m_allowForward;
            TrackerEnterDefinition enterDefinition = m_enterDefinition;
            float trackerDistance = tracker.TrackerDistance;
            ValidateSurfaceDirection(ref m_allowBackward, allowForward, enterDefinition, trackerDistance);
            bool allowBackward = m_allowBackward;
            TrackerExitDefinition exitDefinition = m_exitDefinition;
            float trackerLength = tracker.TrackerLength;
            float distance = tracker.TrackerDistance;
            ValidateSurfaceDirection(ref m_allowForward, allowBackward, exitDefinition, trackerLength - distance);
            CanTurnAround = m_allowForward && m_allowBackward;
        }
        private void ValidateSurfaceDirection(ref bool allowDirectionReverse, bool allowDirection,
            TrackerEndDefinition endDefinition, float trackerDistance)
        {
            if (!allowDirectionReverse) return;
            if (endDefinition == null) return;
            if (allowDirection && endDefinition.ForceDirectionDistance > trackerDistance)
                allowDirectionReverse = false;
        }
        protected override bool TryGetSlopeOverride(out CharacterAbilityDefinition_Movement.SlopeMotion slopeOverride)
        {
            if (m_motionDefinition == null) return base.TryGetSlopeOverride(out slopeOverride);
            slopeOverride = m_motionDefinition.MotionOverride;
            return true;
        }
        public override void UpdateForwardSpeed(ref float forwardSpeed, ref Vector3 forwardDirection, float deltaTime,
            float motionMultiplier = 1f, float effectiveInputMagnitude = 1f,
            AnimationCurve accelerationMultiplier = null, bool applyDeceleration = true)
        {
            if (m_exitDefinition != null)
            {
                float exitDistance = m_exitDefinition.ExitDistance;
                float distance = m_character.Tracker.TrackerDistanceFromEnd;
                if (distance < exitDistance)
                {
                    if (m_exitStartSpeed == 0f) m_exitStartSpeed = m_character.WorldVelocityMagnitude;
                    forwardSpeed = m_exitDefinition.EvaluateSpeed(exitDistance - distance, m_exitStartSpeed);
                    return;
                }
            }
            base.UpdateForwardSpeed(ref forwardSpeed, ref forwardDirection, deltaTime, motionMultiplier,
                effectiveInputMagnitude, accelerationMultiplier, applyDeceleration);
            CheckForInstantTurnAround();
        }
        private void CheckForInstantTurnAround()
        {
            if (m_allowForward && m_allowBackward) return;
            if ((!m_allowForward && !m_character.Tracker.SurfaceReversed) ||
                (!m_allowBackward && m_character.Tracker.SurfaceReversed))
                m_character.TurnAround(0f);
            CharacterAbilityDefinition.Motion motion = m_slopeOverride != null && m_slopeOverride.ForwardMotion != null
                ? m_slopeOverride.ForwardMotion : Definition.ForwardMotion;
            // Both shipping forms skip the write only for ordered speed >= minimum.
            if (m_character.WorldVelocityMagnitude >= motion.SpeedMin) return;
            m_character.SetWorldVelocity(m_character.WorldVelocityNormalised * motion.SpeedMin);
        }
        public float GetExitOrientation() => m_exitDefinition == null ? 0f : m_exitDefinition.ExitRotation;
        public CharacterAbilityDefinition GetExitAbilityDefinitionOverride() => m_exitDefinition == null ? null : m_exitDefinition.AbilityDefinitionOverride;
        private void RemoveCameraOverride()
        {
            if (m_cameraOverrideHandle == null) return;
            m_cameraManagerRef.Get().RemoveCameraSettingOverride(m_cameraOverrideHandle);
            m_cameraOverrideHandle = null;
        }
        protected abstract TTrackerDef GetTrackerDefinition();
        protected CharacterAbility_MovementTracker() { }
    }
}
