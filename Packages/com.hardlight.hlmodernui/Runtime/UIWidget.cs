using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
    // Original HLModernUI.Runtime.dll 0x0200003a: field/method-free marker.
    public interface IUIWidgetParameters { }

    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [DisallowMultipleComponent]
    [Il2CppSetOption(Option.NullChecks, false)]
    public abstract class UIWidget : MonoBehaviour
    {
        // 0x06000197; ARM64 0x1a4fac8. Genuine player RET, including null input.
        public virtual void Setup(IUIWidgetParameters parameters) { }

        // 0x06000198; ARM64 0x1a4facc. Genuine MonoBehaviour constructor forward.
        protected UIWidget() { }
    }
}
