using System;
using System.Text;
using Hardlight;
using UnityEngine;
using UnityEngine.Events;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 0x020003b9: complete 25 fields and 19 declared methods.
    // Lambdas retain captured callbacks; generated compiler metadata is not byte layout parity.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Serializable]
    public class CharacterStickyControls
    {
        // 0x04000cf9; original static offset 0x0.
        private static readonly UnityEngine.Quaternion m_inverseRotation = Quaternion.AngleAxis(180f, Vector3.up);
        // 0x04000cfa; original instance offset 0x10.
        [UnityEngine.SerializeField()]
        private HardlightProject.TimeCategory m_timeCategory = TimeCategory.PlayerMovement;
        // 0x04000cfb; original instance offset 0x14.
        [UnityEngine.TooltipAttribute("If set, override sticky forward with current or last surface's forward direction.")]
        [UnityEngine.SerializeField()]
        private bool m_surfaceOverride;
        // 0x04000cfc; original instance offset 0x18.
        [Hardlight.ShowIfAttribute("m_surfaceOverride", false)]
        [UnityEngine.TooltipAttribute("If set, override sticky forward with transform's else default to current camera forward.")]
        [UnityEngine.SerializeField()]
        private UnityEngine.Transform m_transformOverride;
        // 0x04000cfd; original instance offset 0x20.
        [UnityEngine.TooltipAttribute("Orientation can be flipped if movement is closer to the other direction.")]
        [UnityEngine.SerializeField()]
        private bool m_canFlipOrientation;
        // 0x04000cfe; original instance offset 0x21.
        [UnityEngine.TooltipAttribute("Input direction is relative to initial vector when sticky controls were activated.")]
        [UnityEngine.SerializeField()]
        private bool m_useRelativeControls;
        // 0x04000cff; original instance offset 0x24.
        [UnityEngine.SerializeField()]
        [UnityEngine.TooltipAttribute("Relative controls only active if input is less than the max angle.")]
        [Hardlight.ShowIfAttribute("m_useRelativeControls", null)]
        private float m_relativeControlMaxAngle = 60f;
        // 0x04000d00; original instance offset 0x28.
        [UnityEngine.SerializeField()]
        [UnityEngine.TooltipAttribute("On camera snap, sticky controls exit condition if angle between camera and character exceeds threshold.")]
        [UnityEngine.Serialization.FormerlySerializedAsAttribute("m_useAngleThreshold")]
        private bool m_useExitConditions = true;
        // 0x04000d01; original instance offset 0x2c.
        [UnityEngine.SerializeField()]
        [UnityEngine.TooltipAttribute("On camera snap, timer until sticky controls exit conditions are evaluated.")]
        [UnityEngine.Serialization.FormerlySerializedAsAttribute("m_useAngleThresholdTimer")]
        [Hardlight.ShowIfAttribute("m_useExitConditions", null)]
        private float m_useExitConditionTimer;
        // 0x04000d02; original instance offset 0x30.
        [UnityEngine.TooltipAttribute("On camera snap, exit condition if the angle between the camera forward and character forward is greater than or equal to this value.")]
        [Hardlight.ShowIfAttribute("m_useExitConditions", null)]
        [UnityEngine.Serialization.FormerlySerializedAsAttribute("m_angleThreshold")]
        [UnityEngine.SerializeField()]
        private float m_exitCameraAngleThreshold = 45f;
        // 0x04000d03; original instance offset 0x34.
        [UnityEngine.SerializeField()]
        [UnityEngine.TooltipAttribute("Exit condition if the angle between input and character forward is less than or equal to this value.")]
        [Hardlight.ShowIfAttribute("m_useExitConditions", null)]
        private float m_exitInputAngleThreshold = 15f;
        // 0x04000d04; original instance offset 0x38.
        [UnityEngine.SerializeField()]
        [UnityEngine.TooltipAttribute("Delay time, after collision trigger is entered, until sticky controls are activated.")]
        private float m_delaySeconds;
        // 0x04000d05; original instance offset 0x3c.
        [UnityEngine.SerializeField()]
        [UnityEngine.TooltipAttribute("Updates sticky controls continuously while active.")]
        private bool m_updateRequired;
        // 0x04000d06; original instance offset 0x3d.
        [UnityEngine.SerializeField()]
        [UnityEngine.TooltipAttribute("Whether sticky controls are deactivated on trigger volume exit.")]
        private bool m_cancelOnExit;
        // 0x04000d07; original instance offset 0x40.
        [UnityEngine.SerializeField()]
        [UnityEngine.TooltipAttribute("Delay until sticky controls are cancelled after leaving trigger volume.")]
        [Hardlight.ShowIfAttribute("m_cancelOnExit", null)]
        private float m_cancelOnExitDelaySeconds;
        // 0x04000d08; original instance offset 0x48.
        [UnityEngine.SerializeField()]
        [UnityEngine.TooltipAttribute("Abilities the sticky controls are allowed to be active for.  No entries means all are allowed.")]
        private System.Collections.Generic.List<HardlightProject.ActorAbilityType> m_abilitiesToActivate = new System.Collections.Generic.List<ActorAbilityType>();
        // 0x04000d09; original instance offset 0x50.
        private bool m_initialised;
        // 0x04000d0a; original instance offset 0x58.
        private UnityEngine.Events.UnityEvent m_onActivated;
        // 0x04000d0b; original instance offset 0x60.
        private UnityEngine.Events.UnityEvent m_onDeactivated;
        // 0x04000d0c; original instance offset 0x68.
        private bool m_flipOrientation;
        // 0x04000d0d; original instance offset 0x6c.
        private float m_exitCameraAngleCosineThreshold;
        // 0x04000d0e; original instance offset 0x70.
        private float m_exitInputAngleCosineThreshold;
        // 0x04000d0f; original instance offset 0x74.
        private bool m_exitConditionActive;
        // 0x04000d10; original instance offset 0x78.
        private float m_exitConditionTime;
        // 0x04000d11; original instance offset 0x7c.
        private float m_elapsedTime;

        // 0x0600167e: flag publication precedes the two original float cosines.
        private void Initialise()
        {
            if (m_initialised) return;
            m_initialised = true;
            m_exitCameraAngleCosineThreshold = Mathf.Cos(m_exitCameraAngleThreshold * Mathf.Deg2Rad);
            m_exitInputAngleCosineThreshold = Mathf.Cos(m_exitInputAngleThreshold * Mathf.Deg2Rad);
        }

        // 0x0600167f: stored default, elapsed accumulation, then exit evaluation.
        public void Update(Character character, float deltaTime)
        {
            if (character.Storage.GetValue(ActorFSMKeys.TurnCameraActive, false, true))
            {
                m_elapsedTime += deltaTime;
                if (!UpdateExitConditions(character, character.ControllerMovementMagnitude))
                    DeactivateStickyControls(character);
            }
        }

        // 0x06001680: original direct reference assignment.
        public void SetTransformOverride(Transform transformOverride)
        {
            m_transformOverride = transformOverride;
        }

        // 0x06001681: captured activation callback has no cancellation handle.
        public void DoTriggerActivate(Character character,
            UnityEvent onActivated = null, UnityEvent onDeactivated = null)
        {
            Initialise();
            m_onActivated = onActivated;
            m_onDeactivated = onDeactivated;
            if (m_delaySeconds > 0f)
                TimeScaledUtilities_SDT.DelayFixedSeconds(m_delaySeconds, m_timeCategory,
                    () => TriggerStickyControls(character));
            else
                TriggerStickyControls(character);
        }

        // 0x06001682: only the original update-required flag gates refresh.
        public void DoTriggerStay(Character character)
        {
            if (m_updateRequired) UpdateStickyControls(character);
        }

        // 0x06001683: the separate captured cancellation callback is retained.
        public void DoTriggerDeactivate(Character character)
        {
            if (!m_cancelOnExit) return;
            if (m_cancelOnExitDelaySeconds > 0f)
                TimeScaledUtilities_SDT.DelayFixedSeconds(m_cancelOnExitDelaySeconds,
                    m_timeCategory, () => DeactivateStickyControls(character));
            else
                DeactivateStickyControls(character);
        }

        // 0x06001684: surface override returns before the orientation-flip branch.
        private Quaternion GetForwardRotation(Character character)
        {
            if (m_surfaceOverride) return character.Tracker.TrackerRotation;
            Quaternion rotation = m_transformOverride != null
                ? m_transformOverride.rotation : character.CameraRotation;
            return m_flipOrientation ? rotation * m_inverseRotation : rotation;
        }

        // 0x06001685: recompute the selected rotation after setting the flip flag.
        private void TriggerStickyControls(Character character)
        {
            m_flipOrientation = false;
            Quaternion cameraRotation = GetForwardRotation(character);
            if (m_canFlipOrientation &&
                Vector3.Dot(character.ForwardDirection, cameraRotation * Vector3.forward) < 0f)
            {
                m_flipOrientation = true;
                cameraRotation = GetForwardRotation(character);
            }
            SetTurnCameraActive(character, cameraRotation);
        }

        // 0x06001686: retain modifier/ability gates and the five storage writes in order.
        public void SetTurnCameraActive(Character character, Quaternion cameraRotation)
        {
            if (!character.GetModifierValue<bool>((int)GameplayModifierType.StickyControlsAvailable))
                return;
            if (!ValidForAbility(character)) return;
            character.Storage.SetValue(ActorFSMKeys.TurnCameraActive, true);
            character.Storage.SetValue(ActorFSMKeys.TurnCameraInput, character.RawControllerMovement);
            character.Storage.SetValue(ActorFSMKeys.TurnCameraRotation, cameraRotation);
            character.Storage.SetValue(ActorFSMKeys.TurnCameraStickyControls, this);
            float relativeInputAngle = 0f;
            if (m_useRelativeControls)
            {
                float angle = Vector2.SignedAngle(character.RawControllerMovement, Vector2.up);
                if (Mathf.Abs(angle) < m_relativeControlMaxAngle)
                    relativeInputAngle = angle;
            }
            character.Storage.SetValue(ActorFSMKeys.TurnCameraInputRelative, relativeInputAngle);
            m_exitConditionActive = m_useExitConditions;
            m_exitConditionTime = m_useExitConditionTimer;
            m_elapsedTime = 0f;
            if (m_onActivated != null) m_onActivated.Invoke();
        }

        // 0x06001687: refresh rotation first, then raw input; no active-state gate.
        private void UpdateStickyControls(Character character)
        {
            Quaternion cameraRotation = GetForwardRotation(character);
            character.Storage.SetValue(ActorFSMKeys.TurnCameraRotation, cameraRotation);
            character.Storage.SetValue(ActorFSMKeys.TurnCameraInput, character.RawControllerMovement);
        }

        // 0x06001688: an empty real list permits all abilities; null is not substituted.
        private bool ValidForAbility(Character character)
        {
            return m_abilitiesToActivate.Count == 0 ||
                ActorAbilityUtilities.AnyAbilityInUse(character, m_abilitiesToActivate, 0.2f);
        }

        // 0x06001689: consume elapsed time once; preserve strict/order-dependent exits.
        public bool UpdateExitConditions(Character character, float inputMagnitude)
        {
            if (!ValidForAbility(character)) return false;
            float elapsedTime = m_elapsedTime;
            m_elapsedTime = 0f;
            if (m_exitConditionTime > 0f)
            {
                m_exitConditionTime = Mathf.Max(m_exitConditionTime - elapsedTime, 0f);
                m_exitConditionActive = m_exitConditionTime == 0f;
            }
            if (!m_exitConditionActive) return true;
            if (inputMagnitude < character.Constants.StickyControlsInputDeadZone) return false;
            if (GetCharacterToCameraForward(character) < m_exitCameraAngleCosineThreshold)
                return false;
            if (GetInputToCameraForward(character) > m_exitInputAngleCosineThreshold)
            {
                character.Storage.RemoveValue<Vector2>(ActorFSMKeys.TurnCameraInput);
                return false;
            }
            return true;
        }

        // 0x0600168a: project/normalize the camera forward before evaluating raw input.
        private float GetInputToCameraForward(Character character)
        {
            Vector3 cameraForward = Vector3.ProjectOnPlane(character.CameraForward,
                character.UpDirection).normalized;
            Vector3 inputForward = CharacterMovementUtilities.GetIntendedForward(character,
                character.RawControllerMovement);
            return Vector3.Dot(cameraForward, inputForward);
        }

        // 0x0600168b: the original dot uses RawIntendedForward, not ForwardDirection.
        private float GetCharacterToCameraForward(Character character)
        {
            return Vector3.Dot(Vector3.ProjectOnPlane(character.CameraForward,
                character.UpDirection).normalized, character.RawIntendedForward);
        }

        // 0x0600168c: deactivate storage before invoking the retained event.
        public void DeactivateStickyControls(Character character)
        {
            character.Storage.SetValue(ActorFSMKeys.TurnCameraActive, false);
            if (m_onDeactivated != null) m_onDeactivated.Invoke();
        }

        // 0x0600168d: inspect the controls stored on the character, not necessarily this.
        public void GetDebugInfo(Character character, StringBuilder stringInfoBuilder)
        {
            if (!character.Storage.GetValue(ActorFSMKeys.TurnCameraActive, false, true)) return;
            CharacterStickyControls stickyControls =
                character.Storage.GetValue<CharacterStickyControls>(
                    ActorFSMKeys.TurnCameraStickyControls, null, true);
            if (stickyControls != null) GetDebugInfo(stickyControls, character, stringInfoBuilder);
        }

        // 0x0600168e: exact original strings, float formatting and camera/input clamp difference.
        private static void GetDebugInfo(CharacterStickyControls stickyControls,
            Character character, StringBuilder stringInfoBuilder)
        {
            stringInfoBuilder.AppendLine(string.Format("Sticky controls: {0:F2}",
                character.CameraForwardOnCharacterPlane));
            MonoBehaviour surface = stickyControls.m_surfaceOverride
                ? character.Tracker.TrackerLocation.m_surface as MonoBehaviour : null;
            if (!ReferenceEquals(surface, null))
                stringInfoBuilder.AppendLine("   Surface: " + surface.gameObject.name);
            else if (stickyControls.m_transformOverride != null)
                stringInfoBuilder.AppendLine("   Transform: " + stickyControls.m_transformOverride.name);
            else
                stringInfoBuilder.AppendLine("   Character forward");
            if (stickyControls.m_exitConditionTime > 0f)
                stringInfoBuilder.AppendLine(string.Format("   Exit condition timer: {0:F2}",
                    stickyControls.m_exitConditionTime));
            else if (stickyControls.m_exitConditionActive)
            {
                stringInfoBuilder.AppendLine("   Exit condition active");
                stringInfoBuilder.AppendLine(string.Format("      Camera angle: {0:F2}/{1:F2}",
                    Mathf.Acos(stickyControls.GetCharacterToCameraForward(character)) * Mathf.Rad2Deg,
                    stickyControls.m_exitCameraAngleThreshold));
                stringInfoBuilder.AppendLine(string.Format("      Input angle: {0:F2}/{1:F2}",
                    Mathf.Acos(Mathf.Clamp(stickyControls.GetInputToCameraForward(character), -1f, 1f)) *
                        Mathf.Rad2Deg, stickyControls.m_exitInputAngleThreshold));
            }
        }

        // 0x0600168f: real field initializers precede Object ctor; other fields retain zero/null.
        // 0x06001690 is represented by the readonly quaternion initializer above, preserving
        // original beforefieldinit semantics instead of adding an explicit static constructor.
        public CharacterStickyControls() { }
    }
}
