using System;
using System.Collections.Generic;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000974; bodies translated from the 1.10.1 x86-64 binary.
    // The camera look zone: the touch movement, scaled by the screen size, becomes the camera input. Tapping the
    // zone twice within the double tap time supplies the configured game inputs (the camera snap).
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class OnScreenControlZoneCamera : OnScreenControlZone
    {
        [SerializeField] private float m_xSensitivity = 75f;
        [SerializeField] private float m_ySensitivity = 125f;
        [SerializeField] private GameObject m_debugTouchZoneView;
        [SerializeField] private float m_cameraSnapDoubleTapTimeSeconds = 0.2f;
        [SerializeField] private float m_minimumCancelDoubleTapInputMagnitude;
        [SerializeField] private List<GameInputCharacter> m_doubleTapInputs = new List<GameInputCharacter>();

        private const int DoubleTapCount = 2;

        private bool m_debugOverlayEnabled;
        private int m_snapTapCount;
        private float m_doubleTapTimerSeconds;
        private float m_totalMovementAmountSqr;
        private float m_minimumCancelDoubleTapInputMagnitudeSqr;
        private bool m_cameraSnapEnabled = true;

        // 06003682 (1.10.1 RVA 0x691200)
        public override void Initialise()
        {
            base.Initialise();
            UpdateCachedValues();
        }

        // 06003683 (RVA 0x691250)
        public override void Shutdown(bool usingValidInputType)
        {
            base.Shutdown(usingValidInputType);
        }

        // 06003684 (RVA 0x691260)
        public void SetCameraSnapEnabled(bool enable)
        {
            m_cameraSnapEnabled = enable;
        }

        // 06003685 (RVA 0x691270): the touch movement is scaled by the sensitivity over the screen height for x and
        // the screen width for y, and its squared length is added up (OnTouchRelease clears the sum while camera snap
        // is enabled).
        protected override Vector2 OnInput(Vector2 touchPosition, Vector2 touchDelta)
        {
            m_controllerInput = touchDelta;
            m_controllerInput.x *= m_xSensitivity / (float)Screen.height;
            m_controllerInput.y *= m_ySensitivity / (float)Screen.width;
            m_totalMovementAmountSqr += m_controllerInput.sqrMagnitude;
            return m_controllerInput;
        }

        // 06003686 (RVA 0x691330)
        private string GetDebugTouchZoneViewText()
        {
            return "Touch Zone View: " + (m_debugOverlayEnabled ? "On" : "Off");
        }

        // 06003687 (RVA 0x6913A0)
        private void ToggleDebugTouchZoneView()
        {
            m_debugOverlayEnabled = !m_debugOverlayEnabled;
            m_debugTouchZoneView.SetActive(m_debugOverlayEnabled);
        }

        // 06003688 (RVA 0x6913D0): the sum of the squared scaled movements since the last release is compared with the squared cancel
        // magnitude; a smaller sum counts as a tap. The second tap starts the down/held/up input sequence for each
        // configured input whose ignored abilities are not in use.
        protected override void OnTouchRelease()
        {
            if (m_cameraSnapEnabled && PointerWasDown)
            {
                if (m_totalMovementAmountSqr >= m_minimumCancelDoubleTapInputMagnitudeSqr)
                {
                    m_doubleTapTimerSeconds = 0f;
                    m_snapTapCount = 0;
                }
                else
                {
                    m_snapTapCount++;
                    if (m_snapTapCount >= DoubleTapCount)
                    {
                        foreach (GameInputCharacter doubleTapInput in m_doubleTapInputs)
                        {
                            if (TouchValidForCharacter(doubleTapInput.IgnoreAbilities))
                                SupplyGameInputSequence(doubleTapInput.Input, InputType.Touch);
                        }
                        m_doubleTapTimerSeconds = 0f;
                        m_snapTapCount = 0;
                    }
                }
                m_totalMovementAmountSqr = 0f;
            }
            base.OnTouchRelease();
        }

        // 06003689 (RVA 0x6916F0): the tap count expires once the double tap time has passed in unscaled time.
        private void Update()
        {
            if (m_snapTapCount > 0)
            {
                m_doubleTapTimerSeconds += Time.unscaledDeltaTime;
                if (m_doubleTapTimerSeconds >= m_cameraSnapDoubleTapTimeSeconds) ResetSnapValues();
            }
            else
            {
                m_doubleTapTimerSeconds = 0f;
            }
        }

        // 0600368a (RVA 0x691620)
        private void ResetSnapValues()
        {
            m_snapTapCount = 0;
            m_doubleTapTimerSeconds = 0f;
        }

        // 0600368b (RVA 0x691750)
        private void OnValidate()
        {
            if (gameObject.CanValidate()) UpdateCachedValues();
        }

        // 0600368c (RVA 0x691230)
        private void UpdateCachedValues()
        {
            m_minimumCancelDoubleTapInputMagnitudeSqr = m_minimumCancelDoubleTapInputMagnitude * m_minimumCancelDoubleTapInputMagnitude;
        }

        // 0600368d (RVA 0x691790)
        public OnScreenControlZoneCamera()
        {
        }

        // Original Game.Runtime 02000975.
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        [Il2CppSetOption(Option.NullChecks, false)]
        [Serializable]
        private class GameInputCharacter
        {
            [SerializeField]
            [Tooltip("The game input to supply.")]
            private GameInput m_input;
            [SerializeField]
            [Tooltip("If any specified abilities are active on the character then ignore input while in use.")]
            private List<ActorAbilityType> m_ignoreAbilities = new List<ActorAbilityType>();

            // 0600368e (RVA 0x691830)
            public GameInput Input => m_input;

            // 0600368f (RVA 0x691840)
            public List<ActorAbilityType> IgnoreAbilities => m_ignoreAbilities;

            // 06003690 (RVA 0x691850)
            public GameInputCharacter()
            {
            }
        }
    }
}
