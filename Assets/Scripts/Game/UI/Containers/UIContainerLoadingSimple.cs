using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Events;

namespace HardlightProject
{
    // Original Game.Runtime 0x020008d6; bodies translated from the 1.10.1 x86-64 binary.
    // A loading screen with a progress bar fed by the "LoadingProgress" string message.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class UIContainerLoadingSimple : UIContainer
    {
        [SerializeField] private UIWidgetSDTLoadingBar m_loadingBar;
        [SerializeField] private UnityEvent m_onLoadingBarStarted;
        private readonly string m_loadingEventName = "LoadingProgress";
        private MessageExchange<string> m_loadingProgress;
        private bool m_loadingBarEventHasFired;

        // 0x060032fc (0x63B450)
        public void Awake()
        {
            m_loadingProgress = ProcessManager.GetSystemAutoCreate<MessageExchange<string>>();
            m_loadingProgress.SubscribeToMessage<float>(in m_loadingEventName, UpdateLoadingProgress);
        }

        // 0x060032fd (0x63B540)
        private void OnDestroy()
        {
            m_loadingProgress.UnsubscribeFromMessage<float>(in m_loadingEventName, UpdateLoadingProgress);
        }

        // 0x060032fe (0x63B5E0): the started event fires once, with the first progress message.
        public void UpdateLoadingProgress(in float loadingProgress)
        {
            if (!m_loadingBarEventHasFired)
            {
                if (m_onLoadingBarStarted != null)
                    m_onLoadingBarStarted.Invoke();
                m_loadingBarEventHasFired = true;
            }
            m_loadingBar.UpdateLoadingProgress(loadingProgress);
        }

        // 0x060032ff (0x63B620)
        public UIContainerLoadingSimple() { }
    }
}
