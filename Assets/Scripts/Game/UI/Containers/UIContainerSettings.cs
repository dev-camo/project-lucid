using Hardlight;
using Hardlight.UI.Binding;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x02000922; bodies translated from the 1.10.1 x86-64 binary.
    // The settings screen: publishes where the player is and whether graphics can be changed, and hides the debug page.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public sealed class UIContainerSettings : UIContainer
    {
        [SerializeField] private GameObject m_debugSettings;
        public readonly Bindable<bool> IsInMetaGame = new Bindable<bool>();
        public readonly Bindable<bool> IsInGameplay = new Bindable<bool>();
        public readonly Bindable<bool> GraphicsSettingsActive = new Bindable<bool>();

        // 0x060034a1 (0x6794C0): the debug page is switched off unconditionally in the retail build.
        private void OnEnable()
        {
            App app = ProcessManager.GetSystem<App>();
            IsInMetaGame.Value = app.IsMetaGameActive();
            IsInGameplay.Value = app.IsGameplayActive();
            GraphicsSettingsActive.Value = ProcessManager.GetSystem<VisualQualityManager_SDT>().DeviceCanChangeGraphicsQuality();
            m_debugSettings.SetActive(false);
        }

        // 0x060034a2 (0x6795D0)
        public UIContainerSettings() { }
    }
}
