using HardlightProject;
using NUnit.Framework;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // The declarations of the dream power store and collections types against the original DummyDll metadata (type flags, fields and
    // methods with their flags in metadata order, so the compiler-generated closure classes and lambda names are included, and the
    // IL2CPP options in attribute order). These are emission checks, separate from the behaviour tests.
    public sealed class DreamPowerStoreDeclarationTests
    {
        [Test]
        public void TheUIContainerCollectionsDeclarationMatchesTheOriginalMetadata()
        {
            System.Type type = typeof(UIContainerCollections);
            AssertMembers(type, 0x00100101,
                new (string, int)[]
                {
                    ("m_ornamentViewerLevelDefinition", 0x0001),
                    ("m_tabsGroup", 0x0001),
                    ("m_galleryToggle", 0x0001),
                    ("m_musicToggle", 0x0001),
                    ("m_dreamPowerToggle", 0x0001),
                    ("m_ornamentGalleryContainer", 0x0001),
                    ("m_musicCollectionsContainer", 0x0001),
                    ("m_dreamPowerCollections", 0x0001),
                    ("m_galleryOpenAnalyticsEvent", 0x0001),
                    ("m_musicOpenAnalyticsEvent", 0x0001),
                    ("m_storeOpenAnalyticsEvent", 0x0001),
                    ("CollectionsTab", 0x0026),
                    ("SelectButtonShow", 0x0026),
                    ("SelectButtonPrompt", 0x0026),
                    ("NavigationEnabled", 0x0026),
                    ("m_app", 0x0001),
                    ("m_ornamentManager", 0x0001),
                    ("m_musicTrackManager", 0x0001),
                },
                new (string, int)[]
                {
                    ("Start", 0x0081),
                    ("Update", 0x0081),
                    ("SetupGalleryToggle", 0x0081),
                    ("SetupMusicToggle", 0x0081),
                    ("SetupDreamPowerStoreToggle", 0x0081),
                    ("OnGalleryToggle", 0x0081),
                    ("OnMusicToggle", 0x0081),
                    ("OnDreamPowerToggle", 0x0081),
                    ("OnDreamPowerStoreNavigatedAway", 0x0081),
                    ("SetOrnamentGameObjectsActive", 0x0081),
                    ("OnNewOrnamentsCountChanged", 0x0081),
                    ("OnNewMusicTracksCountChanged", 0x0081),
                    ("OnControllerPromptUpdated", 0x0081),
                    ("OnInputTabBackwards", 0x0081),
                    ("OnInputTabForwards", 0x0081),
                    (".ctor", 0x1886),
                });
            AssertIl2CppOptions(type, 2, 1);
        }

        [Test]
        public void TheUIContainerCollectionsDreamPowerStoreDeclarationMatchesTheOriginalMetadata()
        {
            System.Type type = typeof(UIContainerCollectionsDreamPowerStore);
            AssertMembers(type, 0x00100101,
                new (string, int)[]
                {
                    ("m_infoButton", 0x0001),
                    ("m_infoGlyph", 0x0001),
                    ("Title", 0x0026),
                    ("OnSelectionChanged", 0x0001),
                },
                new (string, int)[]
                {
                    ("add_OnSelectionChanged", 0x0886),
                    ("remove_OnSelectionChanged", 0x0886),
                    ("OnEnable", 0x00c4),
                    ("UpdateTitle", 0x0081),
                    ("OnDreamPowerChanged", 0x00c4),
                    ("OnNavigatedAway", 0x0086),
                    (".ctor", 0x1886),
                    ("<UpdateTitle>b__7_0", 0x0081),
                });
            AssertIl2CppOptions(type, 1, 2);
        }

        [Test]
        public void TheCollectionsDreamPowerStoreClosureCacheClassDeclarationMatchesTheOriginalMetadata()
        {
            System.Type type = typeof(UIContainerCollectionsDreamPowerStore).GetNestedType("<>c", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            AssertMembers(type, 0x00102103,
                new (string, int)[]
                {
                    ("<>9", 0x0036),
                    ("<>9__10_0", 0x0016),
                },
                new (string, int)[]
                {
                    (".cctor", 0x1891),
                    (".ctor", 0x1886),
                    ("<.ctor>b__10_0", 0x0083),
                });
        }

        [Test]
        public void TheUIContainerDreamPowerListDeclarationMatchesTheOriginalMetadata()
        {
            System.Type type = typeof(UIContainerDreamPowerList);
            AssertMembers(type, 0x00100101,
                new (string, int)[]
                {
                    ("m_powerDescription1", 0x0001),
                    ("m_powerDescription2", 0x0001),
                    ("m_powerDescription3", 0x0001),
                    ("Visible", 0x0026),
                    ("m_dataManager", 0x0001),
                    ("m_saveManager", 0x0001),
                    ("m_missionManager", 0x0001),
                    ("m_dreamPowerStoreManager", 0x0001),
                },
                new (string, int)[]
                {
                    ("Start", 0x0081),
                    ("LoadDreamPowerSlots", 0x0081),
                    ("PopulateSlot", 0x0081),
                    (".ctor", 0x1886),
                });
            AssertIl2CppOptions(type, 1, 2);
        }

        [Test]
        public void TheUIContainerDreamPowerStoreDeclarationMatchesTheOriginalMetadata()
        {
            System.Type type = typeof(UIContainerDreamPowerStore);
            AssertMembers(type, 0x00100001,
                new (string, int)[]
                {
                    ("m_allowEquipping", 0x0001),
                    ("m_collectableCounter", 0x0001),
                    ("m_dreamPowerPrefab", 0x0001),
                    ("m_dreamPowerPadderPrefab", 0x0001),
                    ("m_blueCoinCost", 0x0001),
                    ("m_dreamPowersToggleGroup", 0x0001),
                    ("m_slotButtons", 0x0001),
                    ("m_dreamPowerRows", 0x0001),
                    ("m_dreamPowerRectAnimation", 0x0001),
                    ("m_dreamPowerRectBrowseClip", 0x0001),
                    ("m_dreamPowerRectSlotClip", 0x0001),
                    ("m_counterStringFormat", 0x0001),
                    ("m_scrollRectSelection", 0x0001),
                    ("m_actionLockedString", 0x0001),
                    ("m_actionEquippableString", 0x0001),
                    ("m_actionEquippedString", 0x0001),
                    ("m_insufficientCurrencyString", 0x0001),
                    ("m_actionPurchasableString", 0x0001),
                    ("m_lockedByArchetypeString", 0x0001),
                    ("m_rewardUnlockStringPicker", 0x0001),
                    ("m_combinedArchetypeIcons", 0x0001),
                    ("Available", 0x0026),
                    ("HasArchetype", 0x0026),
                    ("LockedDescription", 0x0026),
                    ("ShowDescriptionArchetype", 0x0026),
                    ("Browsing", 0x0026),
                    ("ActionAvailable", 0x0026),
                    ("CharacterArchetypeImage", 0x0026),
                    ("CharacterArchetypeSilhouette", 0x0026),
                    ("DescriptionArchetypeImage", 0x0026),
                    ("ActionLabel", 0x0026),
                    ("DreamPowerName", 0x0026),
                    ("DreamPowerDescription", 0x0026),
                    ("DreamPowerLockedDescription", 0x0026),
                    ("DreamPowerIcon", 0x0026),
                    ("CounterFormatString", 0x0026),
                    ("TotalAmount", 0x0026),
                    ("BoughtAmount", 0x0026),
                    ("Purchasable", 0x0026),
                    ("BlueCoinCost", 0x0026),
                    ("m_characterArchetype", 0x0004),
                    ("m_dataManager", 0x0004),
                    ("m_unlockedDreamPowerBands", 0x0021),
                    ("m_widgetByDefinition", 0x0021),
                    ("m_lastArchetypeIcon", 0x0001),
                    ("m_dreamPowerStoreManager", 0x0001),
                    ("m_missionManager", 0x0001),
                    ("m_saveManager", 0x0001),
                    ("m_progressionManager", 0x0001),
                    ("m_slots", 0x0001),
                    ("m_currentSelectedDefinition", 0x0001),
                    ("m_uiNavigationManager", 0x0001),
                },
                new (string, int)[]
                {
                    ("Awake", 0x0081),
                    ("OnEnable", 0x01c4),
                    ("SetCharacterArchetype", 0x0081),
                    ("SetUnlockedBands", 0x0081),
                    ("UpdateTitle", 0x0081),
                    ("SetupDreamPowers", 0x0081),
                    ("RefreshDreamPowers", 0x0081),
                    ("GetBlueCoinsAvailable", 0x0081),
                    ("CreateWidgetParameters", 0x0081),
                    ("GetUIItemState", 0x0081),
                    ("UpdateSlots", 0x0081),
                    ("UpdateCounter", 0x0081),
                    ("OnDreamPowerChanged", 0x01c4),
                    ("SetSlotStatus", 0x0081),
                    ("UpdateLockedStatus", 0x0081),
                    ("UpdateAvailableDescription", 0x0081),
                    ("UpdateAction", 0x0081),
                    ("Action_DoAction", 0x0086),
                    ("ClearSelectedPower", 0x0086),
                    ("StartEquip", 0x0081),
                    ("SetAllToggles", 0x0086),
                    ("Action_FinishEquip", 0x0086),
                    ("Action_ExitEquip", 0x0086),
                    ("ReselectCurrentPower", 0x0081),
                    ("Purchase", 0x0081),
                    (".ctor", 0x1886),
                });
            AssertIl2CppOptions(type, 2, 1);
        }

        [Test]
        public void TheUIContainerDreamPowerStoreCombinedArchetypeIconDeclarationMatchesTheOriginalMetadata()
        {
            System.Type type = typeof(UIContainerDreamPowerStore).GetNestedType("CombinedArchetypeIcon", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            AssertMembers(type, 0x00102003,
                new (string, int)[]
                {
                    ("m_archetypes", 0x0001),
                    ("m_iconReference", 0x0001),
                    ("<Icon>k__BackingField", 0x0001),
                    ("ArchetypeSet", 0x00a6),
                },
                new (string, int)[]
                {
                    ("get_Icon", 0x0886),
                    ("set_Icon", 0x0881),
                    ("SetUp", 0x0086),
                    ("ArchetypesMatch", 0x0086),
                    (".ctor", 0x1886),
                });
            AssertIl2CppOptions(type, 2, 1);
        }

        [Test]
        public void TheWidgetParametersClosureClassDeclarationMatchesTheOriginalMetadata()
        {
            System.Type type = typeof(UIContainerDreamPowerStore).GetNestedType("<>c__DisplayClass61_0", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            AssertMembers(type, 0x00100103,
                new (string, int)[]
                {
                    ("<>4__this", 0x0006),
                    ("itemState", 0x0006),
                },
                new (string, int)[]
                {
                    (".ctor", 0x1886),
                    ("<CreateWidgetParameters>b__0", 0x0083),
                });
        }

        [Test]
        public void TheDreamPowerChangedClosureClassDeclarationMatchesTheOriginalMetadata()
        {
            System.Type type = typeof(UIContainerDreamPowerStore).GetNestedType("<>c__DisplayClass65_0", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            AssertMembers(type, 0x00100103,
                new (string, int)[]
                {
                    ("<>4__this", 0x0006),
                    ("itemState", 0x0006),
                },
                new (string, int)[]
                {
                    (".ctor", 0x1886),
                    ("<OnDreamPowerChanged>b__0", 0x0083),
                });
        }

        [Test]
        public void TheUIContainerDreamPowerStoreBlueCoinCostDeclarationMatchesTheOriginalMetadata()
        {
            System.Type type = typeof(UIContainerDreamPowerStoreBlueCoinCost);
            AssertMembers(type, 0x00100101,
                new (string, int)[]
                {
                    ("ShowCost", 0x0026),
                    ("Cost", 0x0026),
                },
                new (string, int)[]
                {
                    ("Setup", 0x00c6),
                    (".ctor", 0x1886),
                });
            AssertIl2CppOptions(type, 2, 1);
        }

        [Test]
        public void TheUIContainerDreamPowerStoreInfoDeclarationMatchesTheOriginalMetadata()
        {
            System.Type type = typeof(UIContainerDreamPowerStoreInfo);
            AssertMembers(type, 0x00100101,
                new (string, int)[]
                {
                    ("m_animation", 0x0001),
                    ("m_closeClip", 0x0001),
                    ("m_closeEvent", 0x0001),
                    ("m_app", 0x0001),
                    ("m_saveManager", 0x0001),
                },
                new (string, int)[]
                {
                    ("Start", 0x0081),
                    ("Action_Close", 0x0086),
                    ("Action_AnimationCloseFinish", 0x0086),
                    (".ctor", 0x1886),
                });
            AssertIl2CppOptions(type, 2, 1);
        }

        [Test]
        public void TheUIWidgetToggleCollectionsDeclarationMatchesTheOriginalMetadata()
        {
            System.Type type = typeof(UIWidgetToggleCollections);
            AssertMembers(type, 0x00100101,
                new (string, int)[]
                {
                    ("m_target", 0x0001),
                    ("m_imageWhenOff", 0x0001),
                    ("m_imageWhenOn", 0x0001),
                    ("m_alertBadge", 0x0001),
                },
                new (string, int)[]
                {
                    ("Setup", 0x00c6),
                    ("Action_OnValueChanged", 0x00c6),
                    ("UpdateBadge", 0x0086),
                    (".ctor", 0x1886),
                });
            AssertIl2CppOptions(type, 2, 1);
        }

        [Test]
        public void TheUIWidgetToggleCollectionsParametersDeclarationMatchesTheOriginalMetadata()
        {
            System.Type type = typeof(UIWidgetToggleCollectionsParameters);
            AssertMembers(type, 0x00100101,
                new (string, int)[]
                {
                    ("DisplayNumber", 0x0026),
                },
                new (string, int)[]
                {
                    (".ctor", 0x1886),
                });
            AssertIl2CppOptions(type, 1, 2);
        }

        [Test]
        public void TheUIWidgetToggleDreamPowerDeclarationMatchesTheOriginalMetadata()
        {
            System.Type type = typeof(UIWidgetToggleDreamPower);
            AssertMembers(type, 0x00100101,
                new (string, int)[]
                {
                    ("m_toggle", 0x0001),
                    ("m_groupElement", 0x0001),
                    ("m_transitionAnimation", 0x0001),
                    ("m_lockedByArchetypeAnimation", 0x0001),
                    ("m_lockedByProgression", 0x0001),
                    ("m_purchasableAnimation", 0x0001),
                    ("m_selectedAnimation", 0x0001),
                    ("m_clearSelectedAnimation", 0x0001),
                    ("m_availableAnimation", 0x0001),
                    ("m_equippedAnimation", 0x0001),
                    ("m_uiWidgetNavigation", 0x0001),
                    ("m_onSelected", 0x0001),
                    ("Icon", 0x0026),
                    ("DebugName", 0x0026),
                    ("Cost", 0x0026),
                    ("ShowEquipped", 0x0026),
                    ("ShowCost", 0x0026),
                    ("ShowSelected", 0x0026),
                    ("TierName", 0x0026),
                    ("TierIndex", 0x0026),
                    ("BackgroundImage", 0x0026),
                    ("m_parameters", 0x0001),
                    ("m_bandDefinition", 0x0001),
                    ("s_dreamPowerStoreManagerRef", 0x0031),
                },
                new (string, int)[]
                {
                    ("get_GroupElement", 0x0886),
                    ("get_UIWidgetNavigation", 0x0886),
                    ("Setup", 0x00c6),
                    ("SetVisualState", 0x0081),
                    ("OnDestroy", 0x0081),
                    ("Action_OnToggleIsOn", 0x0086),
                    ("OnSelected", 0x0081),
                    ("Action_OnNavigatedTo", 0x0086),
                    ("Action_OnNavigatedAway", 0x0086),
                    (".ctor", 0x1886),
                    (".cctor", 0x1891),
                    ("<Setup>b__28_0", 0x0081),
                });
            AssertIl2CppOptions(type, 1, 2);
        }
    }
}
