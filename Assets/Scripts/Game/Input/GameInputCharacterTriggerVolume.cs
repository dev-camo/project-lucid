using Hardlight;
using Hardlight.Utils;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 020001d2; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class GameInputCharacterTriggerVolume : CharacterTrigger, IInputButton, ITimeScaled
    {
        // 020001d3. Declared before InitialPressInput: its closure class is <>c__DisplayClass22_0.
        private enum InputTypeSimulation
        {
            HeldButton = 0,
            SingleFrameButtonOnEntry = 1,
            SingleFrameButtonOnExit = 2
        }

        // 060009c7..060009cc (1.10.1 RVA 0x6F3020..0x6F32F0): compiler-generated accessors.
        public event ButtonHandler.OnHeld ButtonHandlers;
        public event ButtonHandler.OnDown ButtonDownHandlers;
        public event ButtonHandler.OnUp ButtonUpHandlers;

        private const InputType InputType = Hardlight.InputType.DefaultController;
        [SerializeField] private GameInput[] m_inputsToProvide;
        [SerializeField] private InputTypeSimulation m_typeSimulation;
        [Tooltip("Triggering immediately can cause issues with form changes, tick this box if you expect to enter ball form.")]
        [SerializeField]
        private bool m_delayInputUntilWholeColliderInside;
        private float m_frameCount;
        private bool m_delayComplete;
        private ControlMapping m_controlMapping;

        // 060009cd (1.10.1 RVA 0x6F3380)
        protected override void Awake()
        {
            base.Awake();
            m_controlMapping = ProcessManager.GetSystem<InputSystem>().ControlMapping;
            m_controlMapping.RegisterButtonProvider(this);
        }

        // 060009ce (1.10.1 RVA 0x6F3420): a delayed held button waits for DoTriggerStay.
        protected override void DoTriggerEnter(Character character)
        {
            base.DoTriggerEnter(character);
            switch (m_typeSimulation)
            {
                case InputTypeSimulation.SingleFrameButtonOnEntry:
                    InitialPressInput();
                    break;
                case InputTypeSimulation.HeldButton:
                    if (!m_delayInputUntilWholeColliderInside) InitialHoldInput();
                    break;
            }
        }

        // 060009cf (1.10.1 RVA 0x6F36B0): presses once the character's bounds lie inside this collider.
        // m_delayComplete is never cleared.
        protected override void DoTriggerStay(Character character)
        {
            base.DoTriggerStay(character);
            if (!m_delayInputUntilWholeColliderInside || m_delayComplete) return;
            if (!m_collider.bounds.Contains(character.Bounds.min) || !m_collider.bounds.Contains(character.Bounds.max)) return;
            InitialHoldInput();
            m_delayComplete = true;
        }

        // 060009d0 (1.10.1 RVA 0x6F3820): deltaTime is unused.
        public void OnUpdate(float deltaTime)
        {
            ContinuousHoldInput();
        }

        // 060009d1 (1.10.1 RVA 0x6F3940)
        protected override void DoTriggerExit(Character character)
        {
            base.DoTriggerExit(character);
            switch (m_typeSimulation)
            {
                case InputTypeSimulation.SingleFrameButtonOnExit:
                    InitialPressInput();
                    break;
                case InputTypeSimulation.HeldButton:
                    EndHoldInput();
                    break;
            }
        }

        // 060009d2 (1.10.1 RVA 0x6F3590); closure <>c__DisplayClass22_0.<InitialPressInput>b__0 (0x6F3C20).
        // Each release is deferred a frame and reads its input then; it reports value 1.
        private void InitialPressInput()
        {
            m_frameCount = 1f;
            for (int i = 0; i < m_inputsToProvide.Length; i++)
            {
                ButtonDownHandlers?.Invoke(0, m_inputsToProvide[i], 1f, InputType);
                int j = i;
                CoroutineUtils.OnNextFrame(() => ButtonUpHandlers?.Invoke(0, m_inputsToProvide[j], 1f, InputType));
            }
        }

        // 060009d3 (1.10.1 RVA 0x6F34F0): each input is pressed, then held.
        private void InitialHoldInput()
        {
            m_frameCount = 1f;
            for (int i = 0; i < m_inputsToProvide.Length; i++)
            {
                ButtonDownHandlers?.Invoke(0, m_inputsToProvide[i], 1f, InputType);
                ButtonHandlers?.Invoke(0, m_inputsToProvide[i], 1f, InputType);
            }
            SubscribeToTimeManager();
        }

        // 060009d4 (1.10.1 RVA 0x6F38B0)
        private void ContinuousHoldInput()
        {
            m_frameCount += 1f;
            for (int i = 0; i < m_inputsToProvide.Length; i++)
                ButtonHandlers?.Invoke(0, m_inputsToProvide[i], 1f, InputType);
        }

        // 060009d5 (1.10.1 RVA 0x6F39F0): the release reports value 1.
        private void EndHoldInput()
        {
            m_frameCount += 1f;
            for (int i = 0; i < m_inputsToProvide.Length; i++)
                ButtonUpHandlers?.Invoke(0, m_inputsToProvide[i], 1f, InputType);
            UnsubscribeFromTimeManager();
        }

        // 060009d6 (1.10.1 RVA 0x6F3A90)
        private void SubscribeToTimeManager()
        {
            ProcessManager.GetSystem<TimeManager>().Subscribe(this, TimeCategory.System, UpdateOn.Update, "");
        }

        // 060009d7 (1.10.1 RVA 0x6F3B30)
        private void UnsubscribeFromTimeManager()
        {
            TimeManager timeManager = ProcessManager.GetSystemSafe<TimeManager>();
            if (timeManager != null) timeManager.Unsubscribe(this, TimeCategory.System, UpdateOn.Update);
        }

        // 060009d8/060009d9 (1.10.1 RVA 0x6F3BF0/0x6F3C00)
        public bool IsPaused { get; set; }

        // 060009da (1.10.1 RVA 0x6F3C10)
        public GameInputCharacterTriggerVolume()
        {
        }
    }
}
