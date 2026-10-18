using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x02000961: holds a UI blur request while enabled.
    // OnDisable keeps the cancelled handle in m_uiBlurRequest.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class FullscreenShaderUIBlur : MonoBehaviour, ISystem
    {
        private readonly SystemRef<FullscreenShaderManager> m_fullscreenShaderManagerRef = ProcessManager.GetSystemRef<FullscreenShaderManager>();
        private StackableDataHandle m_uiBlurRequest;

        private void OnEnable()
        {
            m_uiBlurRequest = m_fullscreenShaderManagerRef.Get().RequestUIBlurActive();
        }

        private void OnDisable()
        {
            if (m_fullscreenShaderManagerRef.IsNull()) return;
            m_fullscreenShaderManagerRef.Get().CancelFullscreenEffectRequest(m_uiBlurRequest);
        }
    }
}
