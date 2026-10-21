using System;
using System.Collections;
using System.Collections.Generic;
using Hardlight;
using Hardlight.Utils;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0200096c; bodies translated from the 1.10.1 x86-64 binary.
    // Supplies its game inputs while the UI button is pressed, either once on the chosen trigger or held
    // frame by frame. Every supplied input is reported as a touch with value 1.
    public class OnScreenButton : OnScreenInputButton
    {
        [SerializeField] protected UIButton m_button;
        [SerializeField] private GameInput[] m_inputToSupply;
        [SerializeField] private GameInput m_inputOnExit = GameInput.None;
        [SerializeField] private InputTrigger m_inputTrigger;
        [SerializeField] private GameObject m_buttonGraphic;
        [SerializeField] private List<InputType> m_showOnInputTypes = new List<InputType>();
        // No retail body reads this flag.
        [SerializeField] private bool m_mouseShowsInEditor = true;
        private ControlMapping m_controlMapping;
        private const InputType InputType = Hardlight.InputType.Touch;
        private Coroutine m_heldButtonCoroutine;
        private bool m_buttonHeld;
        private Action m_onButtonHeldCompleteCallback;

        // 06003638 (1.10.1 RVA 0x68BB10)
        private void Reset()
        {
            m_button = GetComponent<UIButton>();
        }

        // 06003639 (RVA 0x68BB60): the trigger picks which button event supplies the inputs; anything else is rejected.
        private void Awake()
        {
            m_controlMapping = ProcessManager.GetSystem<InputSystem>().ControlMapping;
            m_controlMapping.RegisterButtonProvider(this);
            ControlMapping.OnUpdateLastInputType += OnInputTypeChanged;
            switch (m_inputTrigger)
            {
                case InputTrigger.Held:
                    m_button.OnButtonDown.AddListener(OnButtonHoldBegin);
                    m_button.OnButtonUp.AddListener(OnButtonHoldEnd);
                    m_button.OnButtonExit.AddListener(OnButtonExit);
                    break;
                case InputTrigger.Up:
                    m_button.OnButtonUp.AddListener(OnButtonUp);
                    break;
                case InputTrigger.Down:
                    m_button.OnButtonDown.AddListener(OnButtonDown);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        // 0600363a (RVA 0x68BE70): only the down and up listeners are withdrawn.
        private void OnDestroy()
        {
            if (m_buttonHeld)
            {
                EndButtonHold();
                m_buttonHeld = false;
            }
            ControlMapping.OnUpdateLastInputType -= OnInputTypeChanged;
            m_controlMapping.UnregisterButtonProvider(this);
            m_button.OnButtonDown.RemoveListener(OnButtonDown);
            m_button.OnButtonUp.RemoveListener(OnButtonUp);
            m_onButtonHeldCompleteCallback = null;
        }

        // 0600363b (RVA 0x68C0E0): a held button finishes its release first and calls back from there.
        public void DisableWhenReady(Action onComplete)
        {
            if (m_buttonHeld)
            {
                m_onButtonHeldCompleteCallback = onComplete;
                m_buttonHeld = false;
            }
            else
            {
                onComplete?.Invoke();
            }
        }

        // 0600363c (RVA 0x68C130)
        private void OnButtonHoldBegin()
        {
            if (!IsInteractable()) return;
            CoroutineUtils.StopUtilCoroutine(ref m_heldButtonCoroutine);
            OnButtonDown();
            if (!gameObject.activeInHierarchy)
            {
                EndButtonHold();
                return;
            }
            m_buttonHeld = true;
            m_heldButtonCoroutine = CoroutineUtils.RunCoroutine(SupplyHeldButton());
        }

        // 0600363d (RVA 0x68C0D0)
        private void OnButtonHoldEnd()
        {
            m_buttonHeld = false;
        }

        // 0600363e (RVA 0x68C2C0)
        private void OnButtonDown()
        {
            if (!IsInteractable()) return;
            for (int i = 0; i < m_inputToSupply.Length; i++)
                OnButtonDown(m_inputToSupply[i], 1f, InputType);
        }

        // 0600363f (RVA 0x68C3E0)
        private void OnButtonUp()
        {
            if (!IsInteractable()) return;
            for (int i = 0; i < m_inputToSupply.Length; i++)
                OnButtonUp(m_inputToSupply[i], 1f, InputType);
        }

        // 06003640 (RVA 0x68C490): leaving the button presses the exit input once, unless it is None.
        private void OnButtonExit()
        {
            if (!IsInteractable() || m_inputOnExit == GameInput.None) return;
            SupplyGameInputSequence(m_inputOnExit, InputType);
        }

        // 06003641 (RVA 0x68C340); MoveNext 06003648 (0x68C740): holds every input each frame until the
        // press ends or the button is deactivated, then releases.
        private IEnumerator SupplyHeldButton()
        {
            while (m_buttonHeld && gameObject.activeInHierarchy)
            {
                for (int i = 0; i < m_inputToSupply.Length; i++)
                    OnButtonHeld(m_inputToSupply[i], 1f, InputType);
                yield return null;
            }
            m_buttonHeld = false;
            EndButtonHold();
        }

        // 06003642 (RVA 0x68C010): releases the inputs, runs the pending callback once and stops the hold coroutine.
        private void EndButtonHold()
        {
            OnButtonUp();
            m_onButtonHeldCompleteCallback?.Invoke();
            m_onButtonHeldCompleteCallback = null;
            CoroutineUtils.StopUtilCoroutine(ref m_heldButtonCoroutine);
        }

        // 06003643 (RVA 0x68C5F0)
        private void OnInputTypeChanged(InputType inputType)
        {
            bool showGraphic = m_showOnInputTypes.Contains(inputType);
            if (m_buttonGraphic == null) return;
            m_buttonGraphic.SetActive(showGraphic);
        }

        // 06003644 (RVA 0x68C2A0)
        private bool IsInteractable()
        {
            return m_button.Interactable;
        }

        // 06003645 (RVA 0x68C6A0)
        public OnScreenButton()
        {
        }
    }
}
