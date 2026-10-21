using Cinemachine;
using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime type 0x02000287.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class CinemachinePlayerCharacterAssign : MonoBehaviour
    {
        [SerializeField]
        private bool m_assignPlayerAsFollow;
        [SerializeField]
        private bool m_assignPlayerAsLookAt = true;
        [SerializeField]
        private CinemachineVirtualCamera m_virtualCamera;
        private SystemRef<CharacterManager> m_characterManagerRef;

        // 0x06000df9
        private void Start()
        {
            m_characterManagerRef = ProcessManager.GetSystemRef<CharacterManager>();
            m_characterManagerRef.InvokeOnValid(OnCharacterManagerValid);
        }

        // 0x06000dfa: the current character, when there is one, is assigned immediately.
        private void OnCharacterManagerValid(CharacterManager characterManager)
        {
            characterManager.RegisterOnCharacterChange(OnCharacterChange, true);
        }

        // 0x06000dfb
        private void OnCharacterChange(Character character)
        {
            AssignPlayerAsTarget();
        }

        // 0x06000dfc
        private void OnDestroy()
        {
            if (m_characterManagerRef.IsValid())
                m_characterManagerRef.Get().ReleaseOnCharacterChange(OnCharacterChange);
        }

        // 0x06000dfd: the character's visual proxy is the follow and look-at target.
        private void AssignPlayerAsTarget()
        {
            if (m_characterManagerRef.IsNull()) return;
            Character character = m_characterManagerRef.Get().GetCurrentCharacterUnsafe();
            SmoothedTransformProxy visualProxy = character.VisualProxy;
            if (m_assignPlayerAsFollow) m_virtualCamera.Follow = visualProxy.transform;
            if (m_assignPlayerAsLookAt) m_virtualCamera.LookAt = visualProxy.transform;
        }

        // 0x06000dfe
        public CinemachinePlayerCharacterAssign() { }
    }
}
