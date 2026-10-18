using System.Collections;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 020006a3; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class GameInputSupplier : MonoBehaviour, IInputButton
    {
        // 06002394..06002399 (1.10.1 RVA 0x588C90..0x588F60): compiler-generated accessors.
        public event ButtonHandler.OnHeld ButtonHandlers;
        public event ButtonHandler.OnDown ButtonDownHandlers;
        public event ButtonHandler.OnUp ButtonUpHandlers;

        [SerializeField] private GameInput m_input;
        [SerializeField] private float m_repeatInputForSeconds;
        private ControlMapping m_controlMapping;
        private Coroutine m_repeatedInput;
        private readonly SystemRef<CharacterManager> m_characterManagerRef = ProcessManager.GetSystemRef<CharacterManager>();

        // 0600239a (1.10.1 RVA 0x588FF0)
        public void OnEnable()
        {
            m_controlMapping = ProcessManager.GetSystem<InputSystem>().ControlMapping;
            m_controlMapping.RegisterButtonProvider(this);
        }

        // 0600239b (1.10.1 RVA 0x589080): needs a current character, whose impulses are retained first.
        public void SupplyInput()
        {
            if (m_characterManagerRef.IsNull() || !m_characterManagerRef.Get().TryGetCurrentCharacter(out Character character)) return;
            character.RetainCurrentImpulses();
            ButtonDownHandlers?.Invoke(0, m_input, 1f, ControlMapping.LastInputType);
            this.SafeStopCoroutine(ref m_repeatedInput);
            m_repeatedInput = StartCoroutine(SupplyHeldInput(m_repeatInputForSeconds));
        }

        // 0600239c (1.10.1 RVA 0x589270); MoveNext 060023a1 (0x589420). The release is reported with value 0.
        private IEnumerator SupplyHeldInput(float durationSeconds)
        {
            while (durationSeconds > 0f)
            {
                ButtonHandlers?.Invoke(0, m_input, 1f, ControlMapping.LastInputType);
                durationSeconds -= Time.deltaTime;
                yield return null;
            }
            ButtonUpHandlers?.Invoke(0, m_input, 0f, ControlMapping.LastInputType);
        }

        // 0600239d (1.10.1 RVA 0x589310)
        private void OnDisable()
        {
            this.SafeStopCoroutine(ref m_repeatedInput);
            if (m_controlMapping == null) return;
            m_controlMapping.UnregisterButtonProvider(this);
        }

        // 0600239e (1.10.1 RVA 0x589390)
        public GameInputSupplier()
        {
        }
    }
}
