using System.Text;
using Hardlight;
using TMPro;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000852; bodies translated from the 1.10.1 x86-64 binary, 0x60DB10..0x60DD10.
    // A debug label above a boss that shows the UI text of one of its abilities while the ability has any.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class ViewBossAbilityInfo : ViewBase
    {
        [SerializeField] private TextMeshProUGUI m_nameText;
        private Boss m_boss;
        private ActorAbility m_ability;
        private readonly StringBuilder m_stringInfoBuilder = new StringBuilder();

        // 06003029 (1.10.1 RVA 0x60DB10): parameters of any other type are ignored. A missing ability hides the label,
        // otherwise it is parented to the boss and drawn through the current camera.
        protected override void OnSetup(ViewParameters viewParams)
        {
            if (!(viewParams is ViewBossAbilityInfoParameters parameters))
                return;
            m_boss = parameters.Boss;
            m_ability = parameters.Ability;
            if (m_ability == null)
            {
                gameObject.SetActive(false);
                return;
            }
            gameObject.SetActive(true);
            gameObject.transform.SetParent(m_boss.transform, false);
            RectTransform.GetComponent<Canvas>().worldCamera = Camera.current;
        }

        // 0600302a (1.10.1 RVA 0x60DC40): the label is shown, one boss height above the boss, only while the ability
        // reports text.
        private void Update()
        {
            m_stringInfoBuilder.Clear();
            bool hasText = m_ability.GetUIText(m_stringInfoBuilder);
            RectTransform.gameObject.SetActive(hasText);
            if (!hasText)
                return;
            float height = m_boss.Bounds.size.y;
            transform.localPosition = new Vector3(0f, height, 0f);
            m_nameText.SetText(m_stringInfoBuilder.ToString());
        }

        // 0600302b (1.10.1 RVA 0x60DD10)
        public ViewBossAbilityInfo()
        {
        }
    }
}
