using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime020009ea; all17 own APIs. Nine methods retain
    // previously recovered bodies; eight missing methods are newly recovered.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public static class CharacterMovementUtilities
    {
        //060038fb. Capture fields before sticky-control callbacks, retain output
        // publication order and the original separate multiplication steps.
        public static void ProcessTurningMovement(TurningMovementInput input,
            out Vector3 forwardVelocity, out Vector3 tangentVelocity, ref Vector3 planeVelocity,
            out Vector3 inputDirection, out float effectiveInputMagnitude, out Quaternion inputRotation)
        {
            Character character = input.Character;
            if ((input.InputForward.sqrMagnitude > 0f || input.InputTurn != 0f) &&
                planeVelocity.sqrMagnitude < character.Constants.MovementInputSpeedMinSqr)
                planeVelocity = Vector3.forward * character.Constants.MovementInputSpeedMin;
            CharacterTraits.TurnTraits turnTraits = input.TurnTraits;
            float inputTurn = input.InputTurn;
            float forwardSpeed = input.ForwardSpeed;
            float intendedMagnitude = character.ControllerMovementMagnitude;
            inputDirection = input.InputForward;
            effectiveInputMagnitude = 0f;
            Vector3 cameraForward = character.CameraForward;
            Vector3 cameraWorldUp = character.CameraWorldUp;
            Vector3 characterForward = character.ForwardDirection;
            Vector3 characterUp = character.UpDirection;
            float turnAngle = 0f;
            if ((inputDirection.magnitude > 0f || (inputTurn != 0f && !float.IsNaN(inputTurn))) &&
                Mathf.Abs(Vector3.Dot(cameraForward, characterUp)) < 1f && input.TurnTraits.BaseRateOfTurn > 0f)
            {
                turnAngle = inputTurn;
                UpdateStickyControls(character, ref turnAngle);
                Vector3 up = character.UpDirection;
                Vector3 projected = characterForward - up * Vector3.Dot(characterForward, up);
                float intendedTurnDelta = Mathf.DeltaAngle(
                    Vector3.SignedAngle(character.CameraForwardOnCharacterPlane, projected, up), turnAngle);
                effectiveInputMagnitude = CalculateEffectiveInputMagnitude(intendedMagnitude, intendedTurnDelta, input.TurnTraits);
                inputRotation = Quaternion.AngleAxis(intendedTurnDelta, Vector3.up);
                inputDirection = inputRotation * planeVelocity;
                inputDirection.y = 0f;
                float baseRate = turnTraits.BaseRateOfTurn;
                float velocityRate = turnTraits.RateOfTurnByVelocity.Evaluate(forwardSpeed);
                float angleRate = turnTraits.RateOfTurnByAngle.Evaluate(Mathf.Abs(intendedTurnDelta) * Mathf.Deg2Rad);
                float turnRate = ((baseRate * input.DeltaTime) * velocityRate) * angleRate;
                planeVelocity = Vector3.RotateTowards(planeVelocity, inputDirection, turnRate * Mathf.Deg2Rad, 0f);
                inputDirection = planeVelocity.normalized;
            }
            else
            {
                float speedMin = character.Constants.MovementInputSpeedMin;
                if (forwardSpeed > speedMin) inputDirection = planeVelocity / forwardSpeed;
                else if (forwardSpeed > 0f)
                {
                    if (Vector3.Dot(cameraWorldUp, characterUp) < character.Constants.MovementStationarySlopeCosineAngleMax)
                    {
                        inputDirection = character.WorldToLocalRotation * character.CameraForwardOnCharacterPlane;
                        planeVelocity = inputDirection * speedMin;
                        effectiveInputMagnitude = 1f;
                        forwardSpeed = speedMin;
                    }
                    else
                    {
                        planeVelocity = Vector3.zero;
                        forwardSpeed = 0f;
                    }
                }
                UpdateStickyControls(character, ref turnAngle);
                inputRotation = Quaternion.identity;
            }
            forwardVelocity = inputDirection * forwardSpeed;
            tangentVelocity = planeVelocity - forwardVelocity;
        }

        //060038fc. The original projections subtract a dot product against the
        // supplied camera up; no extra denominator or validation is introduced.
        public static void UpdateStickyControls(Character character, ref float turnAngle)
        {
            if (!character.ControllerEnabled)
            {
                character.Storage.SetValue(ActorFSMKeys.TurnInputInverted, false);
                return;
            }
            Vector3 up = character.UpDirection;
            Quaternion cameraRotation = character.CameraRotation;
            Vector3 cameraForward = character.CameraForward;
            Vector3 cameraUp = character.CameraWorldUp;
            float upDeg = Vector3.Angle(up, cameraUp);
            Vector3 projectedCamera = cameraForward - cameraUp * Vector3.Dot(cameraForward, cameraUp);
            Vector3 projectedUp = up - cameraUp * Vector3.Dot(up, cameraUp);
            float forwardDeg = Vector3.Angle(projectedCamera, projectedUp);
            float inputMagnitude = character.ControllerMovementMagnitude;
            bool inverted = character.Storage.GetValue(ActorFSMKeys.TurnInputInverted, false, true);
            bool cameraActive = character.Storage.GetValue(ActorFSMKeys.TurnCameraActive, false, true);
            bool wasActive = cameraActive;
            Quaternion storedRotation = character.Storage.GetValue(ActorFSMKeys.TurnCameraRotation, default(Quaternion), true);
            UpdateStickyInput(character, ref turnAngle, ref inverted, inputMagnitude, upDeg, forwardDeg);
            UpdateStickyCamera(character, ref cameraActive, ref storedRotation, cameraRotation, inputMagnitude, upDeg);
            character.Storage.SetValue(ActorFSMKeys.TurnInputInverted, inverted);
            character.Storage.SetValue(ActorFSMKeys.TurnCameraActive, cameraActive);
            character.Storage.SetValue(ActorFSMKeys.TurnCameraRotation, storedRotation);
            if (wasActive && !cameraActive)
                character.Storage.GetValue<CharacterStickyControls>(ActorFSMKeys.TurnCameraStickyControls, null, true).DeactivateStickyControls(character);
        }

        private static void UpdateStickyInput(Character character, ref float turnAngle,
            ref bool turnInputInverted, float inputMagnitude, float upDeg, float forwardDeg)
        {
            if (inputMagnitude < character.Constants.StickyControlsInputDeadZone)
            {
                turnInputInverted = false;
                if (!(upDeg < character.Constants.ControlsUpAngleInversionThreshold ||
                    forwardDeg > character.Constants.ControlsForwardAngleInversionThreshold))
                {
                    turnAngle = -turnAngle;
                    turnInputInverted = true;
                }
            }
            else if (turnInputInverted) turnAngle = -turnAngle;
        }

        private static void UpdateStickyCamera(Character character, ref bool turnCameraActive,
            ref Quaternion turnCameraRotation, Quaternion cameraRotation, float inputMagnitude, float upDeg)
        {
            if (turnCameraActive)
            {
                if (character.AreControlsEnabled() && character.ControllerMovementMagnitude <= 0.0001f)
                    turnCameraActive = false;
                else turnCameraActive = character.Storage.GetValue<CharacterStickyControls>(
                    ActorFSMKeys.TurnCameraStickyControls, null, true).UpdateExitConditions(character, inputMagnitude);
            }
            else if (upDeg < character.Constants.ControlsUpAngleInversionThreshold)
                turnCameraRotation = cameraRotation;
            else character.SetTurnCameraActive(cameraRotation);
        }

        //060038ff. Genuine steering forward.Z is the throttle; original turn
        // angle is retained separately from the sticky-adjusted working value.
        public static void ProcessAirControl<T>(Character character, CharacterAbility_Movement<T> ability,
            CharacterAbilityDefinition_MovementAir abilityDef, bool maintainHeading, float deltaTime,
            bool applyDeceleration) where T : CharacterAbilityDefinition_Movement
        {
            character.ActiveTracking.GetSteeringInput(abilityDef, deltaTime, out Vector3 inputForward, out float inputTurn);
            CharacterTraits.TurnTraits turnTraits = abilityDef.Turn;
            float inputMagnitude = inputForward.z;
            float turnAngle = inputTurn;
            Vector3 localVelocity = character.LocalVelocity;
            float forwardSpeed = Mathf.Sqrt(localVelocity.x * localVelocity.x + localVelocity.z * localVelocity.z);
            Vector3 forwardDirection;
            Vector3 intendedForward;
            if (!(forwardSpeed > 0.0001f))
            {
                intendedForward = character.ForwardDirection;
                forwardDirection = intendedForward * inputMagnitude;
            }
            else
            {
                forwardDirection = new Vector3(localVelocity.x, 0f, localVelocity.z) / forwardSpeed;
                Vector3 velocityNormalised = character.WorldVelocityNormalised;
                Vector3 up = character.UpDirection;
                intendedForward = (velocityNormalised - up * Vector3.Dot(velocityNormalised, up)).normalized;
            }
            float intendedDelta = CalculateIntendedTurnDelta(character, intendedForward, ref turnAngle);
            float effective = CalculateEffectiveInputMagnitude(inputMagnitude, intendedDelta, turnTraits);
            ability.UpdateForwardSpeed(ref forwardSpeed, ref forwardDirection, deltaTime, intendedDelta,
                effective, turnTraits.AccelerationMultiplier, applyDeceleration);
            character.Storage.SetValue(ActorFSMKeys.GroundInputDirection, forwardDirection);
            Vector3 velocity = character.WorldRotation * new Vector3(
                forwardDirection.x * forwardSpeed, localVelocity.y, forwardDirection.z * forwardSpeed);
            ability.UpdateClampedVelocity(ref velocity);
            character.SetWorldVelocity(velocity);
            if (!Mathf.Approximately(0f, inputMagnitude))
            {
                Vector3 up = character.UpDirection;
                Vector3 forward;
                if (character.Storage.GetValue(ActorFSMKeys.TurnCameraActive, false, true))
                    forward = character.CameraForwardOnCharacterPlane;
                else if (maintainHeading) forward = Vector3.ProjectOnPlane(character.CameraForward, up);
                else
                {
                    up = character.WorldUp;
                    forward = Vector3.ProjectOnPlane(character.CameraForward, up);
                }
                Quaternion rotation = Quaternion.LookRotation(forward, up);
                character.SetWorldRotation(rotation * Quaternion.AngleAxis(inputTurn, Vector3.up));
            }
        }

        private static float CalculateIntendedTurnDelta(Character character, Vector3 characterForward, ref float turnAngle)
        {
            UpdateStickyControls(character, ref turnAngle);
            Vector3 up = character.UpDirection;
            Vector3 projected = characterForward - up * Vector3.Dot(characterForward, up);
            return Mathf.DeltaAngle(Vector3.SignedAngle(character.CameraForwardOnCharacterPlane, projected, up), turnAngle);
        }

        private static float CalculateEffectiveInputMagnitude(float intendedMagnitude, float intendedTurnDelta,
            CharacterTraits.TurnTraits turnTraits)
        {
            float magnitude = intendedMagnitude * Mathf.Max(Mathf.Abs(Mathf.Cos(intendedTurnDelta * Mathf.Deg2Rad)), turnTraits.MinimumTurnMagnitude);
            magnitude += turnTraits.InputDamping * magnitude;
            return Mathf.Clamp(magnitude, turnTraits.MinimumInputMagnitude, 1f);
        }

        public static void ProcessDecelerationCurve(ref float forwardSpeed, CharacterAbilityDefinition.Motion motion,
            AnimationCurve curve, float progress, float deltaTime, float motionMultiplier = 1f)
        {
            float minimum = motion.SpeedMin * motionMultiplier;
            if (forwardSpeed > minimum)
            {
                float factor = curve.Evaluate(progress);
                float deceleration = motion.EvaluateDeceleration(forwardSpeed);
                forwardSpeed = Mathf.Max(forwardSpeed - factor * deceleration * deltaTime, 0f);
            }
            else if (forwardSpeed < minimum)
                forwardSpeed = Mathf.Min(forwardSpeed + motion.Acceleration * deltaTime, minimum);
        }

        // 0x06003903. Neither reversal predicate has a method ExtensionAttribute.
        public static bool AreDirectionsReversed(Vector3 fromDirection, Vector3 toDirection)
        {
            return Vector3.Dot(fromDirection, toDirection) < 0f;
        }

        // 0x06003904. Preserve from-then-to rotation of the genuine forward vector.
        public static bool AreRotationsReversed(Quaternion fromRotation, Quaternion toRotation)
        {
            Vector3 fromDirection = fromRotation * Vector3.forward;
            Vector3 toDirection = toRotation * Vector3.forward;
            return Vector3.Dot(fromDirection, toDirection) < 0f;
        }

        // 0x06003905. Storage.GetValue<bool> uses default=false, storeDefault=true.
        // Original key is ActorFSMKeys.TurnInputInverted, static offset 0x3d0.
        public static float GetIntendedTurnAngle(this Character character, Vector2 intendedTurnVector)
        {
            if (character.Storage.GetValue(ActorFSMKeys.TurnInputInverted, false, true))
                intendedTurnVector.x = -intendedTurnVector.x;
            float angle = Vector2.Angle(Vector2.up, intendedTurnVector);
            return intendedTurnVector.x < 0f ? -angle : angle;
        }

        // 0x06003906. Original filtered input, then camera-plane LookRotation,
        // then AngleAxis. AngleAxis multiplies the camera rotation on the left.
        public static Quaternion GetIntendedTurnRotation(this Character character)
        {
            float intendedTurnAngle = character.GetIntendedTurnAngle(character.ControllerMovement);
            Quaternion cameraRotation = Quaternion.LookRotation(character.CameraForwardOnCharacterPlane, character.UpDirection);
            return Quaternion.AngleAxis(intendedTurnAngle, character.UpDirection) * cameraRotation;
        }

        // 0x06003907. Keep this genuine overload and its original evaluation order.
        public static Quaternion GetIntendedTurnRotation(this Character character, float intendedTurnAngle)
        {
            Quaternion cameraRotation = Quaternion.LookRotation(character.CameraForwardOnCharacterPlane, character.UpDirection);
            return Quaternion.AngleAxis(intendedTurnAngle, character.UpDirection) * cameraRotation;
        }

        // 0x06003908. Gate remains HasControllerMovement even with raw input selected.
        // The original optional default is true. False writes angle=0 and identity.
        public static bool TryGetIntendedTurnRotation(this Character character, out float intendedTurnAngle, out Quaternion intendedTurnRotation, bool useRawInput = true)
        {
            bool hasMovement = character.HasControllerMovement;
            if (hasMovement)
            {
                Vector2 movement = useRawInput ? character.RawControllerMovement : character.ControllerMovement;
                intendedTurnAngle = character.GetIntendedTurnAngle(movement);
                Quaternion cameraRotation = Quaternion.LookRotation(character.CameraForwardOnCharacterPlane, character.UpDirection);
                intendedTurnRotation = Quaternion.AngleAxis(intendedTurnAngle, character.UpDirection) * cameraRotation;
            }
            else
            {
                intendedTurnAngle = 0f;
                intendedTurnRotation = Quaternion.identity;
            }
            return hasMovement;
        }

        // 0x06003909. Original Approximately(squared input,0), unsigned Vector3.Angle.
        public static float GetIntendedToCurrentForwardAngle(this Character character)
        {
            if (Mathf.Approximately(character.ControllerMovement.sqrMagnitude, 0f)) return 0f;
            Vector3 intendedForward = character.GetIntendedForward(character.ControllerMovement);
            return Vector3.Angle(intendedForward, character.ForwardDirection);
        }

        // 0x0600390a. Camera forward is the cached projection onto the character plane.
        public static float GetIntendedToCameraForwardAngle(this Character character)
        {
            if (Mathf.Approximately(character.ControllerMovement.sqrMagnitude, 0f)) return 0f;
            Vector3 intendedForward = character.GetIntendedForward(character.ControllerMovement);
            return Vector3.Angle(intendedForward, character.CameraForwardOnCharacterPlane);
        }

        // 0x0600390b. Zero input retains current forward before touching graph storage.
        // Cached camera forward is read before the original AngleAxis engine call.
        public static Vector3 GetIntendedForward(this Character character, Vector2 intendedTurnVector)
        {
            if (Mathf.Approximately(intendedTurnVector.sqrMagnitude, 0f)) return character.ForwardDirection;
            float intendedTurnAngle = character.GetIntendedTurnAngle(intendedTurnVector);
            Vector3 cameraForward = character.CameraForwardOnCharacterPlane;
            Quaternion inputRotation = Quaternion.AngleAxis(intendedTurnAngle, character.UpDirection);
            return inputRotation * cameraForward;
        }

    }
}
