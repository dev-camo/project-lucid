using System;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x02000998, all seven methods and five fields. The game's cross
    // promotion launch button: gated by level requirements and opening the game's own
    // cross promotion container.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class CrossPromoLaunchButtonSDT : CrossPromoLaunchButton
    {
        [SerializeField] private UIContainerIdentifier m_crossPromoContainerIdentifier;
        [SerializeField] private GameplayLevelDefinition m_unlockAtLevel;
        [SerializeField] private RequirementGameplayLevelBase[] m_unlockDefinitions = Array.Empty<RequirementGameplayLevelBase>();
        [SerializeField] private GameObject m_placeholderContent;
        private readonly SystemRef<UIManager> m_uiManagerRef = ProcessManager.GetSystemRef<UIManager>(null, true);

        // 0x06003787: when the cached data is to be used, show the placeholder in place of the icon.
        protected override void OnEnable()
        {
            base.OnEnable();
            if (ShouldUseCachedData())
                SetPlaceHolderContentActive(true);
        }

        // 0x06003788: false whenever local storage is usable; otherwise whether the system is enabled.
        private static bool ShouldUseCachedData()
        {
            if (SystemConfiguration.GetConfig<CrossPromoConfigurationAsset>().CanUseLocalStorage)
                return false;
            return ProcessManager.GetSystem<CrossPromo>(null, true).IsEnabled;
        }

        // 0x06003789: opens the cross promotion container on the requested ad.
        protected override void RequestFullScreenView(int adIndex)
        {
            m_uiManagerRef.Get().GetOrCreate<UIContainerCrossPromo>(
                m_crossPromoContainerIdentifier, new UIContainerCrossPromoParameters(adIndex));
        }

        // 0x0600378a: shown only while every unlock requirement is met.
        protected override bool CanShowLaunchButton()
        {
            return Requirements.AreMet(m_unlockDefinitions);
        }

        // 0x0600378b: the content stays active; the placeholder replaces the icon image.
        private void SetPlaceHolderContentActive(bool isActive)
        {
            m_content.SetActive(true);
            m_placeholderContent.SetActive(isActive);
            m_iconImage.gameObject.SetActive(!isActive);
        }

        // 0x0600378c: hides the base method. With cached data the first ad is requested through
        // the virtual route, without rotating; otherwise the base launch runs.
        public new void Action_LaunchCrossPromo()
        {
            if (ShouldUseCachedData())
                RequestFullScreenView(0);
            else
                base.Action_LaunchCrossPromo();
        }

        // 0x0600378d: the field initialisers above run before the base constructor.
        public CrossPromoLaunchButtonSDT() { }
    }
}
