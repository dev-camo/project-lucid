using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000971; bodies translated from the 1.10.1 x86-64 binary.
    // The on-screen movement stick: the touch offset from the stick origin, measured against half the stick's
    // display width, is the movement input, and the stick graphics follow it through the stick states.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class OnScreenControlStickMovement : OnScreenControlZone, IUserEditableUIComponent
    {
        [SerializeField] private bool m_dynamicPosition;
        [SerializeField] private RectTransform m_originTransform;
        [SerializeField] private RectTransform m_container;
        [SerializeField] private RectTransform m_stickTop;
        [SerializeField] private RectTransform m_miniOuter;
        [SerializeField] private RectTransform m_fullSolidOuter;
        [SerializeField] private RectTransform m_rotatingOuter;
        [Tooltip("Can be applied after the real-world-inches scaling to make base images better reflect stick extents.")]
        [SerializeField] private Vector3 m_baseScaleMultiplier = Vector3.one;
        [Tooltip("Acts as an inner and outer deadzone for the fine-control state to prevent flickering from tiny fractions.")]
        [SerializeField] private float m_magnitudeTolerance = 0.05f;
        [SerializeField] private VibrationEvent m_hapticsReachingFullTilt;
        [SerializeField] private VibrationEvent m_hapticsExitingFullTilt;
        [SerializeField] private VibrationEvent m_hapticsOnSelect;
        [SerializeField] private float m_secondaryXSensitivity = 75f;
        [SerializeField] private float m_secondaryYSensitivity = 125f;
        private readonly SystemRef<VibrationManager> m_vibrationManagerRef = ProcessManager.GetSystemRef<VibrationManager>();
        private readonly SystemRef<CinemachineCameraManager> m_cameraManagerRef = ProcessManager.GetSystemRef<CinemachineCameraManager>();
        private bool m_isControllingCamera;
        private Vector2 m_origin;
        private Vector2 m_visualDisplayAreaDimensions;
        private Vector2 m_lastTouch;
        private Vector3 m_originTransformCentrePrevious;
        private StickState m_state;

        // Original Game.Runtime 02000972.
        private enum StickState
        {
            HIDDEN = 0,
            IDLE = 1,
            HELD_CENTRE = 2,
            FINE_CONTROL = 3,
            FULL_TILT = 4
        }

        // 06003660 (1.10.1 RVA 0x68F030): the stick controls the camera unless the zone is restricted to a single touch.
        public override void Initialise()
        {
            base.Initialise();
            m_saveManagerRef.InvokeOnValid(OnSaveDataLoaded);
            SetStickOrigin(m_originTransform.position);
            if (!m_container.gameObject.activeSelf) m_container.gameObject.SetActive(true);
            m_rotatingOuter.localScale = m_baseScaleMultiplier;
            m_fullSolidOuter.localScale = m_baseScaleMultiplier;
            m_isControllingCamera = !base.IsSingleTouch();
            m_cameraManagerRef.InvokeOnValid(cameraManager => cameraManager.SecondaryMovementSubscribed += SetControllingCamera);
        }

        // 06003661 (RVA 0x68F570)
        private void OnDestroy()
        {
            if (m_cameraManagerRef.TryGet(out CinemachineCameraManager cameraManager))
                cameraManager.SecondaryMovementSubscribed -= SetControllingCamera;
        }

        // 06003662 (RVA 0x68F620)
        private void OnSaveDataLoaded(SaveManager saveManager)
        {
            m_dynamicPosition = saveManager.GetSaveDataSettings().DynamicMovementStick;
        }

        // 06003663 (RVA 0x68F520)
        public void SetActive(bool active)
        {
            if (m_container.gameObject.activeSelf != active) m_container.gameObject.SetActive(active);
        }

        // 06003664 (RVA 0x68F3D0): a fixed stick keeps its origin unless the origin transform has moved; a dynamic
        // stick starts where the touch began and its graphics move there.
        private void SetStickOrigin(Vector2 point)
        {
            Vector3 position = m_originTransform.position;
            if (m_dynamicPosition)
                m_origin = point;
            else if (position != m_originTransformCentrePrevious)
                m_origin = position;
            m_originTransformCentrePrevious = position;
            Rect rect = m_container.rect;
            Vector3 localScale = m_container.localScale;
            m_visualDisplayAreaDimensions = (Vector2)localScale * rect.size;
            SetState(StickState.HELD_CENTRE);
            m_vibrationManagerRef.Get().TriggerEvent(m_hapticsOnSelect);
            if (m_dynamicPosition)
            {
                m_container.position = point;
                m_stickTop.position = point;
            }
        }

        // 06003665 (RVA 0x68F760): the first touch of a press places the stick origin.
        protected override void ProcessTouch(Vector2 touchPosition, Vector2 touchDelta, float pinchDistance, float initialPinchDistance)
        {
            if (!PointerWasDown) SetStickOrigin(touchPosition);
            base.ProcessTouch(touchPosition, touchDelta, pinchDistance, initialPinchDistance);
        }

        // 06003666 (RVA 0x68F920): the stick input is the direction of the touch from the origin, scaled by how far
        // it is through half the display width. A stick that also controls the camera supplies the touch movement
        // scaled by the secondary sensitivities instead.
        protected override Vector2 OnInput(Vector2 touchPosition, Vector2 touchDelta)
        {
            m_lastTouch = touchPosition;
            Vector2 offset = touchPosition - m_origin;
            float halfWidth = m_visualDisplayAreaDimensions.x * 0.5f;
            float extent = Mathf.Clamp01(offset.magnitude / halfWidth);
            m_controllerInput = offset.normalized * extent;
            Vector2 halfDimensions = m_visualDisplayAreaDimensions * 0.5f;
            OnDrag(m_controllerInput * halfDimensions, m_controllerInput.magnitude);
            InputType inputType;
            if (m_isControllingCamera)
            {
                m_controllerInput = touchDelta;
                float xScale = m_secondaryXSensitivity / (float)Screen.height;
                m_controllerInput.x = xScale * m_controllerInput.x;
                float yScale = m_secondaryYSensitivity / (float)Screen.width;
                m_controllerInput.y = yScale * m_controllerInput.y;
                inputType = InputType.Unsupported;
            }
            else
            {
                inputType = ControlMapping.LastInputType;
            }
            SetInputType(inputType);
            return m_controllerInput;
        }

        // 06003667 (RVA 0x68FD30): the stick top follows the movement; beyond the tolerance of the extremes the
        // stick is in fine control, within it at full tilt, each change buzzing the matching haptic event.
        public void OnDrag(Vector3 currentMovement, float magnitude)
        {
            m_stickTop.position = currentMovement.xy() + m_origin;
            if (m_magnitudeTolerance > magnitude) return;
            float fullTiltMagnitude = 1f - m_magnitudeTolerance;
            if (magnitude >= fullTiltMagnitude)
            {
                if (m_state != StickState.FULL_TILT)
                {
                    m_vibrationManagerRef.Get().TriggerEvent(m_hapticsReachingFullTilt);
                    SetState(StickState.FULL_TILT);
                }
            }
            else if (m_state != StickState.FINE_CONTROL)
            {
                if (m_state == StickState.FULL_TILT) m_vibrationManagerRef.Get().TriggerEvent(m_hapticsExitingFullTilt);
                SetState(StickState.FINE_CONTROL);
            }
            RotateToHeading(currentMovement);
        }

        // 06003668 (RVA 0x68FEF0): the rotation about z is the angle from up to the movement, positive when the
        // movement points left (x < 0).
        private void RotateToHeading(Vector3 currentMovement)
        {
            float angle = Vector2.Angle(Vector2.up, currentMovement);
            angle = currentMovement.x < 0f ? angle : -angle;
            Quaternion rotation = Quaternion.Euler(0f, 0f, angle);
            m_rotatingOuter.localRotation = rotation;
            m_stickTop.localRotation = rotation;
        }

        // 06003669 (RVA 0x6900A0)
        protected override void OnTouchRelease()
        {
            base.OnTouchRelease();
            SetState(m_dynamicPosition ? StickState.IDLE : StickState.HELD_CENTRE);
            m_stickTop.localPosition = Vector3.zero;
            RotateToHeading(Vector3.zero);
            SetInputType(ControlMapping.LastInputType);
        }

        // 0600366a (RVA 0x68F640): only the graphics of the new state are shown.
        private void SetState(StickState newState)
        {
            m_state = newState;
            m_miniOuter.gameObject.SetActive(false);
            m_fullSolidOuter.gameObject.SetActive(false);
            m_rotatingOuter.gameObject.SetActive(false);
            m_stickTop.gameObject.SetActive(false);
            switch (m_state)
            {
                case StickState.IDLE:
                    m_miniOuter.gameObject.SetActive(true);
                    break;
                case StickState.HELD_CENTRE:
                    m_stickTop.gameObject.SetActive(true);
                    m_fullSolidOuter.gameObject.SetActive(true);
                    break;
                case StickState.FINE_CONTROL:
                    m_stickTop.gameObject.SetActive(true);
                    m_rotatingOuter.gameObject.SetActive(true);
                    break;
                case StickState.FULL_TILT:
                    m_rotatingOuter.gameObject.SetActive(true);
                    break;
            }
        }

        // 0600366b (RVA 0x690370)
        public override void OnInputTypeChanged(InputType inputType)
        {
            SetInputType(inputType);
        }

        // 0600366c (RVA 0x68FE60): the stick is shown only for the input types the zone is shown on.
        private void SetInputType(InputType inputType)
        {
            bool show = m_showOnInputTypes.Contains(inputType);
            if (show != m_container.gameObject.activeSelf) m_container.gameObject.SetActive(show);
        }

        // 0600366d (RVA 0x690400)
        public void TransformUpdated()
        {
            SetStickOrigin(m_origin);
        }

        // 0600366e (RVA 0x690420)
        public RectTransform GetRectTransform()
        {
            return m_container;
        }

        // 0600366f (RVA 0x690430)
        public bool GetTouchPosition(out Vector2 touch)
        {
            bool pointerIsDown = PointerIsDown;
            touch = pointerIsDown ? m_lastTouch : Vector2.zero;
            return pointerIsDown;
        }

        // 06003670 (RVA 0x6904C0): the pinch distances read 0 unless a pinch is in progress.
        public bool GetPinchInfo(out float pinchDistance, out float initialPinchDistance)
        {
            pinchDistance = PinchDistance;
            initialPinchDistance = InitialPinchDistance;
            return IsPinching;
        }

        // 06003671 (RVA 0x690540): multi-touch follows the component's enabled state; the argument is not read.
        public void SetEditing(bool editing)
        {
            MultiTouchEnabled = enabled;
        }

        // 06003672 (RVA 0x690560)
        private void SetControllingCamera(bool cameraControlActive)
        {
            m_isControllingCamera = cameraControlActive;
        }

        // 06003673 (RVA 0x690570): a stick that controls the camera also takes several touches.
        protected override bool IsSingleTouch()
        {
            return base.IsSingleTouch() && !m_isControllingCamera;
        }

        // 06003674 (RVA 0x690590)
        public OnScreenControlStickMovement()
        {
        }
    }
}
