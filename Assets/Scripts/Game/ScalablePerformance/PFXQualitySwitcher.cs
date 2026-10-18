using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x02000a19: shows the very-low variant only at
    // quality level 0, chosen each time the switcher is enabled.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class PFXQualitySwitcher : MonoBehaviour
    {
        [SerializeField]
        private GameObject m_default;
        [SerializeField]
        private GameObject m_veryLow;

        private void Awake()
        {
            m_default.SetActive(false);
            m_veryLow.SetActive(false);
        }

        private void OnEnable()
        {
            GameObject variant = QualitySettings.GetQualityLevel() == 0 ? m_veryLow : m_default;
            variant.SetActive(true);
        }

        private void OnDisable()
        {
            m_default.SetActive(false);
            m_veryLow.SetActive(false);
        }
    }
}
