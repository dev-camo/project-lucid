using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0200097b; bodies translated from the 1.10.1 x86-64 binary.
    // Shows the air stomp and spin dash icons for the abilities the player character has. A character that
    // has both shows the air stomp while the screen controls are the in-air group and the spin dash otherwise.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class OnScreenTertiaryButton : MonoBehaviour
    {
        [SerializeField] private GameObject m_airStompIcon;
        [SerializeField] private GameObject m_spinDashIcon;
        [SerializeField] private UIContainerOnScreenControls m_uiContainerOnScreenControls;
        private readonly SystemRef<CharacterManager> m_characterManagerRef = ProcessManager.GetSystemRef<CharacterManager>();
        private Character m_playerCharacter;
        private bool m_airStompAttackAvailable;
        private bool m_spinDashAvailable;
        private ScreenControlsGroup m_lastScreenControlsGroup;

        // 060036ae (1.10.1 RVA 0x6924E0)
        private void Awake()
        {
            m_characterManagerRef.Get().RegisterOnCharacterChange(OnCharacterChange, true);
            RefreshIcons();
        }

        // 060036af (RVA 0x692650)
        private void OnEnable()
        {
            DoUpdate();
        }

        // 060036b0 (RVA 0x6925E0)
        private void RefreshIcons()
        {
            if (m_airStompAttackAvailable && m_spinDashAvailable)
            {
                if (m_lastScreenControlsGroup == ScreenControlsGroup.InAir) SetIconsActive(true, false);
                else SetIconsActive(false, true);
            }
            else
            {
                SetIconsActive(m_airStompAttackAvailable, m_spinDashAvailable);
            }
        }

        // 060036b1 (RVA 0x6926F0)
        private void SetIconsActive(bool airStompIcon, bool spinDashIcon)
        {
            m_airStompIcon.SetActive(airStompIcon);
            m_spinDashIcon.SetActive(spinDashIcon);
        }

        // 060036b2 (RVA 0x692720)
        private void OnDestroy()
        {
            if (m_characterManagerRef.TryGet(out CharacterManager characterManager))
                characterManager.ReleaseOnCharacterChange(OnCharacterChange);
        }

        // 060036b3 (RVA 0x6927D0)
        private void OnCharacterChange(Character character)
        {
            m_playerCharacter = character;
            if (m_playerCharacter == null) return;
            m_airStompAttackAvailable = m_playerCharacter.HasAbilityType(ActorAbilityType.Character_AirStompAttack);
            m_spinDashAvailable = m_playerCharacter.HasAbilityType(ActorAbilityType.Character_SpinDashCharge);
            RefreshIcons();
        }

        // 060036b4 (RVA 0x6928C0)
        private void Update()
        {
            DoUpdate();
        }

        // 060036b5 (RVA 0x692660): follows the last screen controls group the container announced.
        private void DoUpdate()
        {
            ScreenControlGroupMessageData lastControlGroup = m_uiContainerOnScreenControls.GetLastControlGroupMessageData();
            ScreenControlsGroup group = lastControlGroup != null ? lastControlGroup.GroupToEnable : ScreenControlsGroup.None;
            if (m_lastScreenControlsGroup == group) return;
            m_lastScreenControlsGroup = group;
            RefreshIcons();
        }

        // 060036b6 (RVA 0x6928D0): the native body only queries CanValidate and keeps no result.
        private void OnValidate()
        {
            gameObject.CanValidate();
        }

        // 060036b7 (RVA 0x6928F0)
        public OnScreenTertiaryButton()
        {
        }
    }
}
