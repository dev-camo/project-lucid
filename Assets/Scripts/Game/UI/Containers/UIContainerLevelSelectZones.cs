using System;
using System.Collections.Generic;
using Hardlight;
using Hardlight.UI.Binding;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x020008cd; bodies translated from the 1.10.1 x86-64 binary.
    // The zone pager of the level select: one zone page per zone, a pagination toggle per zone, and the zone and level selection
    // callbacks. The compiler-generated closure name (<>c__DisplayClass16_0) follows member order.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public sealed class UIContainerLevelSelectZones : UIContainer
    {
        [SerializeField] private UIContainerForZone[] m_zoneContainers;
        [SerializeField] private UIToggleForZone[] m_zoneToggles;
        [SerializeField] private UIToggleGroup m_zoneToggleGroup;
        [SerializeField] private Animation m_paginationAnimation;
        [SerializeField] private List<PaginationAnimation> m_paginationAnimations;
        [SerializeField] private RequirementLevelCategoryBase m_hiddenRequirement;
        [SerializeField] private UIContainerOrbCollectableCounter m_collectableCounter;
        public readonly Bindable<bool> CanSelectPrevious = new Bindable<bool>();
        public readonly Bindable<bool> CanSelectNext = new Bindable<bool>();
        private UIContainerLevelSelectZonesParameters m_parameters;
        private LevelManager m_levelManager;
        private SaveManager m_saveManager;

        // 0x060032d6 (0x6399C0)
        public override void Setup(IUIContainerParameters parameters)
        {
            base.Setup(parameters);
            m_parameters = parameters.GetAs<UIContainerLevelSelectZonesParameters>();
            ProcessManager.GetSystemRef<LevelManager>(null, true).InvokeOnValid(OnLevelManagerValid);
        }

        // 0x060032d7 (0x639AE0): follows the zone toggle group, announces the refreshed level select and waits for the save manager.
        private void OnLevelManagerValid(LevelManager levelManager)
        {
            m_levelManager = levelManager;
            m_zoneToggleGroup.OnGroupChanged += OnZoneToggleGroupChanged;
            OnZoneToggleGroupChanged();
            m_levelManager.OnLevelSelectRefreshed();
            ProcessManager.GetSystemRef<SaveManager>(null, true).InvokeOnValid(OnSaveManagerValid);
        }

        // 0x060032d8 (0x639D20)
        private void OnSaveManagerValid(SaveManager saveManager)
        {
            m_saveManager = saveManager;
            SetupZones();
        }

        // 0x060032d9 (0x63A430): only the page of the selected zone has its level widgets navigable.
        public void UpdateZone(GameplayLevelCategory selected)
        {
            for (int i = 0; i < m_zoneContainers.Length; i++)
            {
                UIContainerForZone containerForZone = m_zoneContainers[i];
                containerForZone.Container.ToggleOn(selected == containerForZone.Zone);
            }
        }

        // 0x060032da (0x639D50): sets up each zone page and its pagination toggle, marks unlocked zones as seen, then selects the
        // zone of the meta-game level.
        private void SetupZones()
        {
            GameplayLevelDefinition firstProductionLevelLocked = m_levelManager.GetFirstProductionLevelLocked();
            IReadOnlyList<LevelSelectZone> zones = GetZones();
            HideLockedZones(zones);
            GameplayLevelDefinition metaGameLevel = m_levelManager.FindMetaGameLevel();
            SaveDataGame currentSave = m_saveManager.CurrentSave;
            foreach (LevelSelectZone levelSelectZone in zones)
            {
                GameplayLevelCategory zone = levelSelectZone.Zone;
                UIContainerLevelSelectZone container = levelSelectZone.Container;
                bool unlockSeen = currentSave.ZoneUnlockSeenGuids.Contains(zone.GetGUID());
                bool locked = levelSelectZone.Locked;
                bool hidden = zone.Requirements.Contains(m_hiddenRequirement);
                container.Setup(new UIContainerLevelSelectZoneParameters(metaGameLevel, firstProductionLevelLocked));
                container.AddValueChangeListener(SelectLevel);
                UIToggleForZone toggleForZone = m_zoneToggles.Find(x => x.Zone == zone);
                if (toggleForZone != null)
                    toggleForZone.Toggle.Setup(new UIWidgetTogglePaginationParameters(value => SelectZone(value, zone), container.HasAnyNewContent, locked, unlockSeen, !hidden));
                if (locked || unlockSeen)
                    continue;
                currentSave.AddZoneUnlockSeenGuid(zone.GetGUID());
                m_saveManager.RequestSave();
            }
            if (!m_levelManager.GameLevels.TryGetZoneForLevel(metaGameLevel, out GameplayLevelCategory metaGameZone))
                return;
            SelectZone(true, metaGameZone);
            m_parameters.OnLevelSelected?.Invoke(metaGameLevel);
            for (int i = 0; i < m_zoneContainers.Length; i++)
                m_zoneContainers[i].Container.UpdateDisabledLevelWidgets(metaGameLevel);
            AnimatePagination(zones);
        }

        // 0x060032db (0x63A4E0)
        private IReadOnlyList<LevelSelectZone> GetZones()
        {
            var zones = new List<LevelSelectZone>();
            for (int i = 0; i < m_zoneContainers.Length; i++)
            {
                UIContainerForZone containerForZone = m_zoneContainers[i];
                GameplayLevelCategory zone = containerForZone.Zone;
                bool locked = !zone.MeetsAllRequirements();
                zones.Add(new LevelSelectZone(zone, containerForZone.Container, locked));
            }
            return zones;
        }

        // 0x060032dc (0x63A660)
        private void HideLockedZones(IReadOnlyList<LevelSelectZone> zones)
        {
            foreach (LevelSelectZone zone in zones)
            {
                if (zone.Locked)
                    zone.Container.gameObject.SetActive(false);
            }
        }

        // 0x060032dd (0x63A920): selecting a zone toggles its pagination toggle on, makes only its page navigable and tells the
        // owner and the collectable counter. Every toggle of the zone is processed; there is no early exit.
        private void SelectZone(bool value, GameplayLevelCategory selected)
        {
            if (!value)
                return;
            for (int i = 0; i < m_zoneToggles.Length; i++)
            {
                UIToggleForZone toggleForZone = m_zoneToggles[i];
                GameplayLevelCategory zone = toggleForZone.Zone;
                if (zone != selected)
                    continue;
                for (int j = 0; j < m_zoneContainers.Length; j++)
                {
                    UIContainerForZone containerForZone = m_zoneContainers[j];
                    containerForZone.Container.ToggleOn(zone == containerForZone.Zone);
                }
                toggleForZone.Toggle.SetSelected(true);
                m_parameters.OnZoneSelected?.Invoke(zone);
                m_collectableCounter.UpdateValues(zone);
            }
        }

        // 0x060032de (0x63AB90): the level widgets of every other level are deselected quietly.
        private void SelectLevel(bool value, GameplayLevelDefinition levelDefinition)
        {
            if (!value)
                return;
            m_parameters.OnLevelSelected?.Invoke(levelDefinition);
            for (int i = 0; i < m_zoneContainers.Length; i++)
                m_zoneContainers[i].Container.UpdateDisabledLevelWidgets(levelDefinition);
        }

        // 0x060032df (0x63AC50): plays the pagination animation authored for the last unlocked zone.
        private void AnimatePagination(IReadOnlyList<LevelSelectZone> levelSelectZones)
        {
            for (int i = levelSelectZones.Count - 1; i >= 0; i--)
            {
                LevelSelectZone levelSelectZone = levelSelectZones[i];
                if (levelSelectZone.Locked)
                    continue;
                foreach (PaginationAnimation paginationAnimation in m_paginationAnimations)
                {
                    if (paginationAnimation.Zone == levelSelectZone.Zone)
                    {
                        m_paginationAnimation.Play(paginationAnimation.AnimationClip.name);
                        break;
                    }
                }
                return;
            }
        }

        // 0x060032e0 (0x639CA0)
        private void OnZoneToggleGroupChanged()
        {
            int firstActiveToggleIndex = m_zoneToggleGroup.GetFirstActiveToggleIndex();
            int toggleCount = m_zoneToggleGroup.ToggleCount;
            bool hasSeveralToggles = toggleCount >= 2;
            CanSelectPrevious.Value = (firstActiveToggleIndex != 0) & hasSeveralToggles;
            CanSelectNext.Value = (firstActiveToggleIndex != toggleCount - 1) & hasSeveralToggles;
        }

        // 0x060032e1
        public UIContainerLevelSelectZones() { }

        // Original Game.Runtime 0x020008ce
        [Il2CppSetOption(Option.NullChecks, false)]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        [Serializable]
        private sealed class UIToggleForZone
        {
            [SerializeField] private GameplayLevelCategory m_zone;
            [SerializeField] private UIWidgetTogglePagination m_toggle;

            // 0x060032e2
            public GameplayLevelCategory Zone => m_zone;

            // 0x060032e3
            public UIWidgetTogglePagination Toggle => m_toggle;
        }

        // Original Game.Runtime 0x020008cf
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        [Il2CppSetOption(Option.NullChecks, false)]
        [Serializable]
        private sealed class UIContainerForZone
        {
            [SerializeField] private GameplayLevelCategory m_zone;
            [SerializeField] private UIContainerLevelSelectZone m_container;

            // 0x060032e5
            public GameplayLevelCategory Zone => m_zone;

            // 0x060032e6
            public UIContainerLevelSelectZone Container => m_container;
        }

        // Original Game.Runtime 0x020008d0
        [Il2CppSetOption(Option.NullChecks, false)]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        private sealed class LevelSelectZone
        {
            // 0x060032e8
            public GameplayLevelCategory Zone { get; }

            // 0x060032e9
            public UIContainerLevelSelectZone Container { get; }

            // 0x060032ea
            public bool Locked { get; }

            // 0x060032eb (0x63AEE0)
            public LevelSelectZone(GameplayLevelCategory zone, UIContainerLevelSelectZone container, bool locked)
            {
                Zone = zone;
                Container = container;
                Locked = locked;
            }
        }

        // Original Game.Runtime 0x020008d1
        [Il2CppSetOption(Option.NullChecks, false)]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        [Serializable]
        private sealed class PaginationAnimation
        {
            [SerializeField] private GameplayLevelCategory m_zone;
            [SerializeField] private AnimationClip m_animationClip;

            // 0x060032ec
            public GameplayLevelCategory Zone => m_zone;

            // 0x060032ed
            public AnimationClip AnimationClip => m_animationClip;
        }
    }
}
