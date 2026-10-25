using Hardlight;
using Hardlight.Analytics;
using Hardlight.Enums;
using Hardlight.UI.Binding;
using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;

namespace HardlightProject
{
    // Original Game.Runtime 0x02000896; bodies translated from the 1.10.1 x86-64 binary.
    // The collections screen: tabs for the ornament gallery, the jukebox and the dream power store.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public sealed class UIContainerCollections : UIContainer
    {
        [SerializeField] private LevelDefinition m_ornamentViewerLevelDefinition;
        [SerializeField] private UIToggleGroup m_tabsGroup;
        [SerializeField] private UIWidgetToggleCollections m_galleryToggle;
        [SerializeField] private UIWidgetToggleCollections m_musicToggle;
        [SerializeField] private UIWidgetToggleCollections m_dreamPowerToggle;
        [SerializeField] private UIContainerOrnamentGallery m_ornamentGalleryContainer;
        [SerializeField] private UIContainerCollectionsMusic m_musicCollectionsContainer;
        [SerializeField] private UIContainerCollectionsDreamPowerStore m_dreamPowerCollections;
        [SerializeField] private UIModernEvent m_galleryOpenAnalyticsEvent;
        [SerializeField] private UIModernEvent m_musicOpenAnalyticsEvent;
        [SerializeField] private UIModernEvent m_storeOpenAnalyticsEvent;
        public readonly Bindable<UICollectionsTab> CollectionsTab = new Bindable<UICollectionsTab>();
        public readonly Bindable<bool> SelectButtonShow = new Bindable<bool>();
        public readonly Bindable<Strings> SelectButtonPrompt = new Bindable<Strings>();
        public readonly Bindable<bool> NavigationEnabled = new Bindable<bool>();
        private App m_app;
        private OrnamentManager m_ornamentManager;
        private MusicTrackManager m_musicTrackManager;

        // 0x0600319d (0x625200): the toggles are set up and the badge/prompt callbacks hooked before the
        // tab opened last time is selected; Start then reports that tab's open analytics event.
        private void Start()
        {
            m_app = ProcessManager.GetSystem<App>();
            m_ornamentManager = ProcessManager.GetSystem<OrnamentManager>();
            m_musicTrackManager = ProcessManager.GetSystem<MusicTrackManager>();
            SetupGalleryToggle();
            SetupMusicToggle();
            SetupDreamPowerStoreToggle();
            m_ornamentGalleryContainer.OnNewOrnamentsCountChanged += OnNewOrnamentsCountChanged;
            m_musicCollectionsContainer.OnNewMusicTracksCountChanged += OnNewMusicTracksCountChanged;
            m_dreamPowerCollections.OnSelectionChanged += OnControllerPromptUpdated;
            UICollectionsTab lastOpenedTab = m_app.Storage.GetValue<UICollectionsTab>(AppFSMKeys.CollectionsTabLastOpened, default, true);
            m_dreamPowerToggle.SetSelected(lastOpenedTab == UICollectionsTab.DreamPower);
            m_musicToggle.SetSelected(lastOpenedTab == UICollectionsTab.Music);
            m_galleryToggle.SetSelected(lastOpenedTab == UICollectionsTab.Gallery);
            switch (lastOpenedTab)
            {
                case UICollectionsTab.Gallery:
                    AnalyticsEventCollector.ButtonPressedEvent(m_galleryOpenAnalyticsEvent);
                    break;
                case UICollectionsTab.Music:
                    AnalyticsEventCollector.ButtonPressedEvent(m_musicOpenAnalyticsEvent);
                    break;
                case UICollectionsTab.DreamPower:
                    AnalyticsEventCollector.ButtonPressedEvent(m_storeOpenAnalyticsEvent);
                    break;
            }
            ControlMapping.Subscribe(GameInput.UICollectionsTabBackwards, OnInputTabBackwards, InputTrigger.Down, -1);
            ControlMapping.Subscribe(GameInput.UICollectionsTabForwards, OnInputTabForwards, InputTrigger.Down, -1);
        }

        // 0x0600319e (0x625A40): the stored flag is copied into the binding only when it differs.
        private void Update()
        {
            bool unlockInProgress = m_app.Storage.GetValue<bool>(AppFSMKeys.CollectionsUnlockInProgress, false, true);
            if (unlockInProgress != NavigationEnabled.Value)
                NavigationEnabled.Value = unlockInProgress;
        }

        // 0x0600319f (0x625740)
        private void SetupGalleryToggle()
        {
            int newOrnamentsCount = m_ornamentManager.GetNewOrnamentsCount();
            m_galleryToggle.Setup(new UIWidgetToggleCollectionsParameters(OnGalleryToggle, newOrnamentsCount));
        }

        // 0x060031a0 (0x625810)
        private void SetupMusicToggle()
        {
            int newTracksCount = m_musicTrackManager.GetNewTracksCount();
            m_musicToggle.Setup(new UIWidgetToggleCollectionsParameters(OnMusicToggle, newTracksCount));
        }

        // 0x060031a1 (0x6258E0): the store tab shows no badge.
        private void SetupDreamPowerStoreToggle()
        {
            m_dreamPowerToggle.Setup(new UIWidgetToggleCollectionsParameters(OnDreamPowerToggle, 0));
        }

        // 0x060031a2 (0x625BA0): the ornament viewer scene is shown while the gallery is selected. Turning
        // the tab on from another tab reports it, and the tab is remembered for the next visit.
        private void OnGalleryToggle(bool isOn)
        {
            SetOrnamentGameObjectsActive(isOn);
            m_dreamPowerCollections.OnNavigatedAway();
            if (!isOn)
                return;
            if (CollectionsTab.Value != UICollectionsTab.Gallery)
                AnalyticsEventCollector.ButtonPressedEvent(m_galleryOpenAnalyticsEvent);
            CollectionsTab.Value = UICollectionsTab.Gallery;
            SelectButtonPrompt.Value = Strings.COMMON_MENU_SELECT;
            SelectButtonShow.Value = true;
            m_app.Storage.SetValue(AppFSMKeys.CollectionsTabLastOpened, UICollectionsTab.Gallery);
        }

        // 0x060031a3 (0x625FB0): turning the tab off stops the music player's track animations;
        // reselecting the current tab returns (cmp 1 at 0x62602E).
        private void OnMusicToggle(bool isOn)
        {
            m_dreamPowerCollections.OnNavigatedAway();
            if (!isOn)
            {
                m_musicCollectionsContainer.MusicPlayer.ResetPlayingAnimations();
                return;
            }
            if (CollectionsTab.Value == UICollectionsTab.Music)
                return;
            AnalyticsEventCollector.ButtonPressedEvent(m_musicOpenAnalyticsEvent);
            CollectionsTab.Value = UICollectionsTab.Music;
            SelectButtonPrompt.Value = Strings.COMMON_MENU_SELECT;
            SelectButtonShow.Value = true;
            m_app.Storage.SetValue(AppFSMKeys.CollectionsTabLastOpened, UICollectionsTab.Music);
        }

        // 0x060031a4 (0x6261A0): the select prompt of this tab follows the store's own selection
        // (OnControllerPromptUpdated), so it is not set here.
        private void OnDreamPowerToggle(bool isOn)
        {
            if (!isOn || CollectionsTab.Value == UICollectionsTab.DreamPower)
                return;
            AnalyticsEventCollector.ButtonPressedEvent(m_storeOpenAnalyticsEvent);
            CollectionsTab.Value = UICollectionsTab.DreamPower;
            m_app.Storage.SetValue(AppFSMKeys.CollectionsDreamPowerInfoReady, true);
            m_app.Storage.SetValue(AppFSMKeys.CollectionsTabLastOpened, UICollectionsTab.DreamPower);
        }

        // 0x060031a5 (0x625F80)
        private void OnDreamPowerStoreNavigatedAway()
        {
            m_dreamPowerCollections.OnNavigatedAway();
        }

        // 0x060031a6 (0x625D80): enables or disables every root object of the ornament viewer scene once
        // that scene has finished loading.
        private void SetOrnamentGameObjectsActive(bool isActive)
        {
            Dictionary<string, AsyncOperationHandle> loadedLevels = m_app.Storage.GetValue<Dictionary<string, AsyncOperationHandle>>(AppFSMKeys.LoadedLevels, null, true);
            if (loadedLevels.TryGetValue(m_ornamentViewerLevelDefinition.GetGUID(), out AsyncOperationHandle handle)
                && handle.IsValid() && handle.IsDone)
            {
                SceneInstance sceneInstance = (SceneInstance)handle.Result;
                foreach (GameObject rootGameObject in sceneInstance.Scene.GetRootGameObjects())
                    rootGameObject.SetActive(isActive);
            }
        }

        // 0x060031a7 (0x6263F0)
        private void OnNewOrnamentsCountChanged()
        {
            m_galleryToggle.UpdateBadge(m_ornamentManager.GetNewOrnamentsCount());
        }

        // 0x060031a8 (0x626420)
        private void OnNewMusicTracksCountChanged()
        {
            m_musicToggle.UpdateBadge(m_musicTrackManager.GetNewTracksCount());
        }

        // 0x060031a9 (0x626450): the select button is hidden while the store reports no prompt.
        private void OnControllerPromptUpdated(Strings buttonPrompt)
        {
            SelectButtonPrompt.Value = buttonPrompt;
            SelectButtonShow.Value = buttonPrompt != Strings.NONE;
        }

        // 0x060031aa (0x6264A0): an input below one half is ignored.
        private void OnInputTabBackwards(float value = 0f)
        {
            if (value < 0.5f)
                return;
            m_tabsGroup.SelectPrevious();
        }

        // 0x060031ab (0x6264C0)
        private void OnInputTabForwards(float value = 0f)
        {
            if (value < 0.5f)
                return;
            m_tabsGroup.SelectNext();
        }

        // 0x060031ac (0x6264E0)
        public UIContainerCollections() { }
    }
}
