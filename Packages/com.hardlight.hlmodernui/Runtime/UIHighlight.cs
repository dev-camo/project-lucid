// Original HLModernUI.Runtime.dll owner 0x02000012; five-method pair preservation.
// 0x06000036 returns m_definition unchanged; 0x06000037 captures m_onShowHighlight
// once, skips a null event, otherwise invokes the genuine UnityEvent synchronously.
// 0x06000038 only calls MonoBehaviour construction; no authored field initializer.
// Managed syntax is inferred from complete dual native evidence; Unity behavior is unexecuted.
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Events;

namespace Hardlight
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public sealed class UIHighlight : MonoBehaviour
    {
        [SerializeField] private UIHighlightDefinition m_definition;
        [SerializeField] private UnityEvent m_onShowHighlight;

        public UIHighlightDefinition Definition => m_definition;

        public void OnShowHighlight()
        {
            m_onShowHighlight?.Invoke();
        }

        public UIHighlight() { }
    }
}
