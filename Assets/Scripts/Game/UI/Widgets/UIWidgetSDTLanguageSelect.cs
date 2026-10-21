using System;
using Hardlight.Enums;
using Hardlight.UI.Binding;
using Hardlight.Utils;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x020009c3; bodies translated from the 1.10.1 x86-64 binary.
    // One language choice on the language page: it announces its language and shows whether it is the chosen one.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public sealed class UIWidgetSDTLanguageSelect : MonoBehaviour
    {
        public Bindable<bool> m_languageIsChosen = new Bindable<bool>();
        [HashEnum(typeof(Languages))]
        [SerializeField] private Languages m_language;
        public Action<Languages> SelectionEvent;

        // 0x0600382e (0x69FC90)
        public void Action_SetLanguage()
        {
            SelectionEvent?.Invoke(m_language);
        }

        // 0x0600382f (0x69FCC0)
        public void SetCachedLanguage(Languages cachedLanguage)
        {
            m_languageIsChosen.Value = m_language == cachedLanguage;
        }

        // 0x06003830 (0x69FCF0)
        public UIWidgetSDTLanguageSelect() { }
    }
}
