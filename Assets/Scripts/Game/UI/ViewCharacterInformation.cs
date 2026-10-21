using System.Text;
using Hardlight;
using Hardlight.UI.Binding;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 02000605; bodies translated from the 1.10.1 x86-64 binary, 0x563E50..0x564330.
    // Actor information overlay that follows the player character: it adds the character's transform and ability
    // tables (Character.DebugGetTransformInfo and DebugGetAbilitiesInfo, both live in retail) to the base texts, and
    // tells the debug menu when it is shown.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class ViewCharacterInformation : ViewActorInformation
    {
        public readonly Bindable<string> AbilityNameInfo = new Bindable<string>();
        public readonly Bindable<string> AbilityEnabledInfo = new Bindable<string>();
        public readonly Bindable<string> AbilityUpdateInfo = new Bindable<string>();
        public readonly Bindable<string> AbilityExtraInfo = new Bindable<string>();
        private readonly StringBuilder m_stringAbilityNameInfo = new StringBuilder(0);
        private readonly StringBuilder m_stringAbilityEnabledInfo = new StringBuilder(0);
        private readonly StringBuilder m_stringAbilityUpdateInfo = new StringBuilder(0);
        private readonly StringBuilder m_stringAbilityExtraInfo = new StringBuilder(0);
        private SystemRef<CharacterManager> m_characterManagerRef;
        private Character m_character;

        // 06002057 (1.10.1 RVA 0x563E50)
        protected override void OnEnable()
        {
            base.OnEnable();
            m_characterManagerRef = ProcessManager.GetSystemRef<CharacterManager>(null, true);
            m_characterManagerRef.Get().RegisterOnCharacterChange(OnCharacterChange, true);
            DebugMenu.OnCharacterInfoToggle?.Invoke(true);
        }

        // 06002058 (1.10.1 RVA 0x563FC0): the manager is only released while it is valid; the debug menu is told either way.
        protected override void OnDisable()
        {
            if (m_characterManagerRef.IsValid())
                m_characterManagerRef.Get().ReleaseOnCharacterChange(OnCharacterChange);
            DebugMenu.OnCharacterInfoToggle?.Invoke(false);
            base.OnDisable();
        }

        // 06002059 (1.10.1 RVA 0x564100)
        private void OnCharacterChange(Character character)
        {
            m_actor = character;
            m_character = character;
        }

        // 0600205a (1.10.1 RVA 0x564140)
        protected override void Update()
        {
            if (m_actor == null)
                return;
            base.Update();
            m_stringInfo.Length = 0;
            m_character.DebugGetTransformInfo(m_stringInfo);
            TransformInfo.Value = m_stringInfo.ToString();
            m_stringAbilityNameInfo.Length = 0;
            m_stringAbilityEnabledInfo.Length = 0;
            m_stringAbilityUpdateInfo.Length = 0;
            m_stringAbilityExtraInfo.Length = 0;
            m_character.DebugGetAbilitiesInfo(m_stringAbilityNameInfo, m_stringAbilityEnabledInfo,
                m_stringAbilityUpdateInfo, m_stringAbilityExtraInfo);
            AbilityNameInfo.Value = m_stringAbilityNameInfo.ToString();
            AbilityEnabledInfo.Value = m_stringAbilityEnabledInfo.ToString();
            AbilityUpdateInfo.Value = m_stringAbilityUpdateInfo.ToString();
            AbilityExtraInfo.Value = m_stringAbilityExtraInfo.ToString();
        }

        // 0600205b (1.10.1 RVA 0x564330): the field initialisers are the whole constructor.
        public ViewCharacterInformation()
        {
        }
    }
}
