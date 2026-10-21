using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.UI;

namespace HardlightProject
{
    // Original Game.Runtime 0x0200084b; bodies translated from the 1.10.1 x86-64 binary.
    // Tints the settings background and drops its material while the settings are opened from the main menu.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [RequireComponent(typeof(Image))]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class UISettingsBackgroundImage : MonoBehaviour
    {
        [SerializeField] private Image m_backgroundImage;
        [SerializeField] private Color m_backgroundImageColourOnMainMenu = new Color(0.3960784f, 0.2117647f, 0.8392157f, 0.9607843f);

        // 0x06003007 (0x60B9B0)
        private void Reset()
        {
            m_backgroundImage = GetComponent<Image>();
        }

        // 0x06003008 (0x60BA00): while a character is in gameplay the background keeps its authored look.
        private void Awake()
        {
            if (CharacterManager.IsCharacterGameplayActive())
                return;
            if (m_backgroundImage == null)
                return;
            m_backgroundImage.material = null;
            m_backgroundImage.color = m_backgroundImageColourOnMainMenu;
        }

        // 0x06003009 (0x60BAB0)
        public UISettingsBackgroundImage() { }
    }
}
