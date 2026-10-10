using System;
using Hardlight;
using Hardlight.Utils;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Complete original Game.Runtime 0x0200025a: 29 ordinary methods plus
    // the two methods of its genuine SubscribeMovementSecondary closure.
    // Body inference is pinned to all original ARM/x86 ranges. Compiler
    // closure naming/layout and original native binding remain unverified.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class CameraInputBrain
    {
        public Action OnSnapButtonPressed;
        public Action<bool, bool> OnSnapButtonReleased;

        public bool MaintainHeading { get; private set; }
        // The original constructor publishes true before the Object base call.
        public bool Enabled { get; set; } = true;
        public bool SecondaryEnabled { get; private set; }

        private Vector2 m_controllerMovement;
        private bool m_movementDebounceRequested;
        private InputDebounce m_movementDebounceHorizontal;
        private InputDebounce m_movementDebounceVertical;

        public void Initialise()
        {
            ControlMapping.Subscribe(GameInput.CameraLeft, AddCameraLeftMovement, InputTrigger.Held);
            ControlMapping.Subscribe(GameInput.CameraRight, AddCameraRightMovement, InputTrigger.Held);
            ControlMapping.Subscribe(GameInput.CameraUp, AddCameraUpMovement, InputTrigger.Held);
            ControlMapping.Subscribe(GameInput.CameraDown, AddCameraDownMovement, InputTrigger.Held);
            ControlMapping.Subscribe(GameInput.CameraMovementComposite, RegisterCompositeMovement);
            ControlMapping.Subscribe(GameInput.CameraSnapBehindTarget, SnapBehindTargetPressed, InputTrigger.Down);
            ControlMapping.Subscribe(GameInput.CameraSnapBehindTarget, SnapBehindTargetRelease, InputTrigger.Up);
            ControlMapping.Subscribe(GameInput.CameraMaintainHeading, MaintainHeadingPressed, InputTrigger.Down);
            ControlMapping.Subscribe(GameInput.CameraMaintainHeading, MaintainHeadingRelease, InputTrigger.Up);
        }

        public void Shutdown()
        {
            ControlMapping.Unsubscribe(GameInput.CameraLeft, AddCameraLeftMovement);
            ControlMapping.Unsubscribe(GameInput.CameraRight, AddCameraRightMovement);
            ControlMapping.Unsubscribe(GameInput.CameraUp, AddCameraUpMovement);
            ControlMapping.Unsubscribe(GameInput.CameraDown, AddCameraDownMovement);
            ControlMapping.Unsubscribe(GameInput.CameraMovementComposite, RegisterCompositeMovement);
            ControlMapping.Unsubscribe(GameInput.CameraSnapBehindTarget, SnapBehindTargetPressed);
            ControlMapping.Unsubscribe(GameInput.CameraSnapBehindTarget, SnapBehindTargetRelease);
            ControlMapping.Unsubscribe(GameInput.CameraMaintainHeading, MaintainHeadingPressed);
            ControlMapping.Unsubscribe(GameInput.CameraMaintainHeading, MaintainHeadingRelease);
        }

        public void SubscribeMovementSecondary(Vector2 movementThresholds, Vector2 movementTimers)
        {
            // Original capture names are preserved naturally. The deferred
            // callback is queued before subscriptions; it has no cancellation
            // or SecondaryEnabled guard and publishes each debounce in order.
            CoroutineUtils.OnNextFrame(() =>
            {
                m_movementDebounceRequested = true;
                m_movementDebounceHorizontal = new InputDebounce(movementThresholds.x, movementTimers.x, 0f);
                m_movementDebounceVertical = new InputDebounce(movementThresholds.y, movementTimers.y, 0f);
            });

            ControlMapping.Subscribe(GameInput.CameraMovementSecondaryComposite, RegisterCompositeMovement);
            ControlMapping.Subscribe(GameInput.Left, AddCameraLeftMovementSecondary, InputTrigger.Held);
            ControlMapping.Subscribe(GameInput.Right, AddCameraRightMovementSecondary, InputTrigger.Held);
            ControlMapping.Subscribe(GameInput.Up, AddCameraUpMovementSecondary, InputTrigger.Held);
            ControlMapping.Subscribe(GameInput.Down, AddCameraDownMovementSecondary, InputTrigger.Held);
            SecondaryEnabled = true;
        }

        public void UnsubscribeMovementSecondary()
        {
            m_movementDebounceRequested = false;
            m_movementDebounceHorizontal = null;
            m_movementDebounceVertical = null;
            ControlMapping.Unsubscribe(GameInput.CameraMovementSecondaryComposite, RegisterCompositeMovement);
            ControlMapping.Unsubscribe(GameInput.Left, AddCameraLeftMovementSecondary);
            ControlMapping.Unsubscribe(GameInput.Right, AddCameraRightMovementSecondary);
            ControlMapping.Unsubscribe(GameInput.Up, AddCameraUpMovementSecondary);
            ControlMapping.Unsubscribe(GameInput.Down, AddCameraDownMovementSecondary);
            SecondaryEnabled = false;
        }

        public void Update(float deltaTime)
        {
            if (InitialiseMovementDebounce())
                return;

            UpdateMovementDebounce(ref m_movementDebounceHorizontal, ref m_controllerMovement.x, deltaTime);
            UpdateMovementDebounce(ref m_movementDebounceVertical, ref m_controllerMovement.y, deltaTime);
        }

        private bool InitialiseMovementDebounce()
        {
            bool requested = m_movementDebounceRequested;
            if (requested)
            {
                m_movementDebounceHorizontal.SetInput(m_controllerMovement.x);
                m_movementDebounceVertical.SetInput(m_controllerMovement.y);
                m_movementDebounceRequested = false;
            }

            return requested;
        }

        private void UpdateMovementDebounce(ref InputDebounce inputDebounce, ref float inputAmount, float deltaTime)
        {
            if (inputDebounce != null)
            {
                inputDebounce.Update(inputAmount, deltaTime);
                if (!inputDebounce.ThresholdExceeded || inputDebounce.TimeExceededContinuous)
                    inputDebounce = null;
                else
                    inputAmount = 0f;
            }
        }

        private void RegisterCompositeMovement(Vector2 amount)
        {
            if (MovementInputValid(amount.x))
                m_controllerMovement.x = amount.x;
            if (MovementInputValid(amount.y))
                m_controllerMovement.y = amount.y;
        }

        private bool MovementInputValid(float value)
        {
            // Original strict comparison uses float bits 0x38d1b717.
            return Enabled && Mathf.Abs(value) > 0.0001f;
        }

        private void AddCameraUpMovement(float value)
        {
            if (MovementInputValid(value))
                m_controllerMovement.y = value;
        }

        private void AddCameraDownMovement(float value)
        {
            if (MovementInputValid(value))
                m_controllerMovement.y = value;
        }

        private void AddCameraRightMovement(float value)
        {
            if (MovementInputValid(value))
                m_controllerMovement.x = value;
        }

        private void AddCameraLeftMovement(float value)
        {
            if (MovementInputValid(value))
                m_controllerMovement.x = value;
        }

        private void AddCameraUpMovementSecondary(float value)
        {
            if (MovementInputValid(value))
                m_controllerMovement.y = value;
        }

        private void AddCameraDownMovementSecondary(float value)
        {
            if (MovementInputValid(value))
                m_controllerMovement.y = -value;
        }

        private void AddCameraRightMovementSecondary(float value)
        {
            if (MovementInputValid(value))
                m_controllerMovement.x = value;
        }

        private void AddCameraLeftMovementSecondary(float value)
        {
            if (MovementInputValid(value))
                m_controllerMovement.x = -value;
        }

        public Vector2 GetAndClearMovement()
        {
            Vector2 movement = m_controllerMovement;
            m_controllerMovement = Vector2.zero;
            return movement;
        }

        private void SnapBehindTargetPressed(float value)
        {
            OnSnapButtonPressed?.Invoke();
        }

        private void SnapBehindTargetRelease(float value)
        {
            OnSnapButtonReleased?.Invoke(true, false);
        }

        private void MaintainHeadingPressed(float value)
        {
            MaintainHeading = true;
        }

        private void MaintainHeadingRelease(float value)
        {
            MaintainHeading = false;
        }

        public CameraInputBrain()
        {
        }
    }
}
