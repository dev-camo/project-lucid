using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public sealed class UIContainerMainMenu : UIContainer
    {
        [SerializeField] private UIModernEvent m_startEvent;
        [SerializeField] private float m_startAfterSeconds;
        private float m_elapsedTime;

        // Original 06003300: load elapsed before querying the unscaled clock.
        private void Update()
        {
            m_elapsedTime += Time.unscaledDeltaTime;
            StartAfterSeconds();
        }

        // Original 06003301: both shipping branches publish for unordered values.
        private void StartAfterSeconds()
        {
            if (m_elapsedTime < m_startAfterSeconds) return;
            m_elapsedTime = 0f;
            PublishStart();
        }

        // Original 06003302: the App lookup precedes the fresh event field read.
        private void PublishStart()
        {
            ProcessManager.GetSystem<App>(null, true).AddUniqueUIModernEvent(m_startEvent);
        }

        // Original 06003303.
        public UIContainerMainMenu() { }
    }
}
