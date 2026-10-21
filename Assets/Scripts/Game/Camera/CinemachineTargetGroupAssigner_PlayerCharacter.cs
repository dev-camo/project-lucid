using Hardlight;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime type 0x02000297. Keeps the current player character's visual proxy in
    // the target group, following character changes until the character closes.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class CinemachineTargetGroupAssigner_PlayerCharacter : CinemachineTargetGroupAssigner
    {
        private SystemRef<CharacterManager> m_characterManagerRef;

        // 0x06000e55; x86_64 0x725610.
        private void Start()
        {
            m_characterManagerRef = ProcessManager.GetSystemRef<CharacterManager>(null, true);
            m_characterManagerRef.InvokeOnValid(OnCharacterManagerValid);
        }

        // 0x06000e56; x86_64 0x725700.
        private void OnCharacterManagerValid(CharacterManager characterManager)
        {
            characterManager.RegisterOnCharacterChange(OnCharacterChange);
            characterManager.OnCharacterPreClose += OnCharacterPreClose;
        }

        // 0x06000e57; x86_64 0x7257D0.
        protected virtual void OnCharacterChange(Character character)
        {
            AddMember(character.VisualProxy.transform);
        }

        // 0x06000e58; x86_64 0x725850. The current character is read without the validity flag.
        private void OnCharacterPreClose()
        {
            RemoveMember(m_characterManagerRef.Get().GetCurrentCharacterUnsafe().VisualProxy.transform);
        }

        // 0x06000e59; x86_64 0x725810.
        private void SetCharacter(Character character)
        {
            AddMember(character.VisualProxy.transform);
        }

        // 0x06000e5a; x86_64 0x7258B0.
        private void OnDestroy()
        {
            if (m_characterManagerRef.IsNull()) return;
            CharacterManager characterManager = m_characterManagerRef.Get();
            characterManager.ReleaseOnCharacterChange(OnCharacterChange);
            characterManager.OnCharacterPreClose -= OnCharacterPreClose;
        }

        // 0x06000e5b
        public CinemachineTargetGroupAssigner_PlayerCharacter() { }
    }
}
