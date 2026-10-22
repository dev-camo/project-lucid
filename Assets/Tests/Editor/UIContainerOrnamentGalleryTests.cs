using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using Hardlight;
using Hardlight.Enums;
using Hardlight.Utils;
using HardlightProject;
using NUnit.Framework;
using ProjectLucid.Tests.OnScreenControlsShared;
using ProjectLucid.Tests.UIContainerShared;
using UnityEngine;
using UnityEngine.ResourceManagement;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.TestTools;
using UnityEngine.UI;
using static ProjectLucid.Tests.ChallengeRewardTestSupport;
using static ProjectLucid.Tests.OrnamentGalleryFixtures;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // UIContainerOrnamentGallery (Game.Runtime 0x020008f8, x86-64 0x66D530..0x670100) as a real component on an inactive owned host,
    // wired like its scene: a real OrnamentManager, TimeManager and AddressableManager registered into their rows, the DataManager and
    // SaveManager (with an open save) of OwnedChallengeRewardServices, an owned touch zone, and the thumbnail rows built from real
    // toggle prefabs. OnEnable is called directly (Edit Mode sends no engine messages) and takes those services from the registry.
    // Every ornament's prefab handle is already completed, so no Addressables load is made. Edit Mode refuses Object.Destroy and a
    // coroutine on the inactive host with an error, so those requests are checked by their logged errors. Not covered: the
    // HLScrollRectSelection hover scroll, the challenge reward string picker of a locked challenge ornament, the Addressables load of
    // an uncached ornament, the release of live Addressables handles on destroy, the time-stepped scaling, and the 1.9/1.75 aspect
    // thresholds of the row length and offset (Screen cannot be set in Edit Mode; only the branch of the current Game view runs).
    public sealed class UIContainerOrnamentGalleryTests
    {
        private static readonly Regex RefusedDestroy = new Regex("^Destroy may not be called from edit mode");
        private static readonly Regex RefusedCoroutine = new Regex("Coroutine couldn't be started");

        private const int NameLocked = 9301, NameS0 = 9311, NameS1 = 9314, NameS2 = 9315, NameC0 = 9312, NameA0 = 9313;
        private const int LoreS0 = 9321, LoreS1 = 9324, LoreS2 = 9325, LoreC0 = 9323, LoreA0 = 9322;
        private const int TipS0 = 9330, TipS1 = 9332, TipC0 = 9331, TipA0 = 9333;

        private static Dictionary<Strings, string> Texts() => new Dictionary<Strings, string>
        {
            { (Strings)NameLocked, "Locked ornament" }, { (Strings)NameS0, "Name S0" }, { (Strings)NameS1, "Name S1" },
            { (Strings)NameS2, "Name S2" }, { (Strings)NameC0, "Name C0" }, { (Strings)NameA0, "Name A0" },
            { (Strings)LoreS0, "Lore S0" }, { (Strings)LoreS1, "Lore S1" }, { (Strings)LoreS2, "Lore S2" },
            { (Strings)LoreC0, "Lore C0" }, { (Strings)LoreA0, "Lore A0" },
            { (Strings)TipS0, "Unlock S0 in act {0}" }, { (Strings)TipS1, "Reach act {0}" }, { (Strings)TipC0, "Tip C0" },
            { (Strings)TipA0, "Tip A0" },
        };

        private sealed class Rig
        {
            public UIContainerOrnamentGallery Gallery;
            public OwnedChallengeRewardServices Services;
            public OrnamentManager Manager;
            public TimeManager Time;
            public AddressableManager Addressables;
            public OnScreenTouchZoneOrnamentProbe Zone;
            public UIWidgetOrnamentEntryRowComposer Composer;
            public UIWidgetCategoryToggle StoryTab, ChallengeTab, AchieverTab;
            public Sprite SelectedBackground, UnselectedBackground;
            public GameObject InfoPanel, InfoGlyph;
            public Button UiButton;
            public UIWidgetButtonImageCharacter InfoButton;
            public Dictionary<OrnamentIdentifier, OrnamentManager.OrnamentStatus> Story, Challenge, Achievement;
            public OrnamentDefinition S0, S1, S2, C0, A0;
        }

        private static Touch TouchAt(float x, float y) => new Touch { fingerId = 1, position = new Vector2(x, y), phase = TouchPhase.Moved };

        private static void Run(Action<Owned, Rig> body) => OwnedStringTable.Run(Texts(), () => GameInputScope.Run(scope => Owned.Run(owned =>
        {
            // The touch zone's base field initialiser caches the CharacterManager reference, and each row caches the navigation manager's.
            scope.Row<CharacterManager>();
            owned.LeaseRow<UINavigationManager>();
            var rig = new Rig { Services = owned.Lease(new OwnedChallengeRewardServices()) };
            // The manager constructor subscribes Initialise and Shutdown (unregistering removes those) and caches the MissionManager reference.
            owned.LeaseActions(SystemAction.Initialise, SystemAction.Shutdown);
            owned.LeaseRow<MissionManager>();
            rig.Manager = owned.Register(new OrnamentManager());
            rig.Addressables = owned.Register(owned.Host("Lucid gallery addressables", false).AddComponent<AddressableManager>());
            rig.Time = owned.Register(CharacterTriggerTestSupport.NewTimeManager(owned, TimeCategory.System, 1f));

            rig.S0 = NewDefinition(owned, "lucid-story-0", OrnamentCategory.Story, Identifier(0), out _, (Strings)NameS0, (Strings)LoreS0, (Strings)TipS0);
            rig.S1 = NewDefinition(owned, "lucid-story-1", OrnamentCategory.Story, Identifier(1), out _, (Strings)NameS1, (Strings)LoreS1, (Strings)TipS1, 3);
            rig.S2 = NewDefinition(owned, "lucid-story-2", OrnamentCategory.Story, Identifier(2), out _, (Strings)NameS2, (Strings)LoreS2);
            rig.C0 = NewDefinition(owned, "lucid-challenge-0", OrnamentCategory.Challenge, Identifier(3), out _, (Strings)NameC0, (Strings)LoreC0, (Strings)TipC0);
            rig.A0 = NewDefinition(owned, "lucid-achievement-0", OrnamentCategory.Achievement, Identifier(4), out _, (Strings)NameA0, (Strings)LoreA0, (Strings)TipA0);
            foreach (OrnamentDefinition definition in new[] { rig.S0, rig.S1, rig.S2, rig.C0, rig.A0 })
                rig.Services.Data.OrnamentDefinitions[definition.OrnamentIdentifier] = definition;
            Dictionary<OrnamentIdentifier, OrnamentManager.OrnamentStatus> Category() =>
                new Dictionary<OrnamentIdentifier, OrnamentManager.OrnamentStatus>(HardlightProject.HardlightEnumComparers.OrnamentIdentifierComparer);
            rig.Story = Category();
            rig.Story[rig.S0.OrnamentIdentifier] = Status(true, true, true);
            rig.Story[rig.S1.OrnamentIdentifier] = Status(false, false, false);
            rig.Story[rig.S2.OrnamentIdentifier] = Status(true, false, true);
            rig.Challenge = Category();
            rig.Challenge[rig.C0.OrnamentIdentifier] = Status(false, false, false);
            rig.Achievement = Category();
            rig.Achievement[rig.A0.OrnamentIdentifier] = Status(true, true, true);
            var collections = (Dictionary<OrnamentCategory, Dictionary<OrnamentIdentifier, OrnamentManager.OrnamentStatus>>)rig.Manager.OrnamentCollections;
            collections[OrnamentCategory.Story] = rig.Story;
            collections[OrnamentCategory.Challenge] = rig.Challenge;
            collections[OrnamentCategory.Achievement] = rig.Achievement;

            // The scene's widgets.
            rig.Gallery = owned.Host("Lucid ornament gallery", false).AddComponent<UIContainerOrnamentGallery>();
            Set(rig.Gallery, "m_containerNavigation", owned.Host("Lucid gallery navigation", false).AddComponent<UIContainerNavigation>()); // Reflection: serialized m_containerNavigation the editor assigns
            OrnamentToggleRig toggle = NewToggleRig(owned, null);
            UIWidgetOrnamentEntryRow row = owned.Host("Lucid gallery row prefab", false).AddComponent<UIWidgetOrnamentEntryRow>();
            Set(row, "m_thumbnailPrefab", toggle.Toggle); // Reflection: serialized m_thumbnailPrefab the editor assigns
            rig.Composer = owned.Host("Lucid gallery composer", false).AddComponent<UIWidgetOrnamentEntryRowComposer>();
            Set(rig.Composer, "m_rowPrefab", row); // Reflection: serialized m_rowPrefab the editor assigns
            Set(rig.Gallery, "m_ornamentEntryRowComposer", rig.Composer); // Reflection: serialized m_ornamentEntryRowComposer the editor assigns
            Set(rig.Gallery, "m_toggleGroup", owned.Host("Lucid gallery toggle group", false).AddComponent<UIToggleGroup>()); // Reflection: serialized m_toggleGroup the editor assigns
            rig.SelectedBackground = NewSprite(owned);
            rig.UnselectedBackground = NewSprite(owned);
            rig.StoryTab = NewTab(owned, rig, "m_storyTab");
            rig.ChallengeTab = NewTab(owned, rig, "m_challengeTab");
            rig.AchieverTab = NewTab(owned, rig, "m_achieverTab");
            rig.InfoPanel = owned.Host("Lucid gallery info panel", true);
            rig.InfoGlyph = owned.Host("Lucid gallery info glyph", true);
            GameObject buttonHost = owned.Host("Lucid gallery info button", false);
            rig.InfoButton = buttonHost.AddComponent<UIWidgetButtonImageCharacter>();
            rig.UiButton = buttonHost.AddComponent<Button>();
            Set(rig.InfoButton, "m_uiButton", rig.UiButton); // Reflection: serialized m_uiButton the editor assigns
            Set(rig.Gallery, "m_infoPanel", rig.InfoPanel); // Reflection: serialized m_infoPanel the editor assigns
            Set(rig.Gallery, "m_infoButton", rig.InfoButton); // Reflection: serialized m_infoButton the editor assigns
            Set(rig.Gallery, "m_infoGlyph", rig.InfoGlyph); // Reflection: serialized m_infoGlyph the editor assigns
            Set(rig.Gallery, "m_nameLocked", (Strings)NameLocked); // Reflection: serialized m_nameLocked the editor assigns
            Set(rig.Gallery, "m_ornamentsPerRowNotchedDevice", 2); // Reflection: serialized m_ornamentsPerRowNotchedDevice the editor assigns
            Set(rig.Gallery, "m_ornamentsPerRowNoNotchDevice", 2); // Reflection: serialized m_ornamentsPerRowNoNotchDevice the editor assigns
            var zoneHost = scope.Inactive("Lucid gallery touch zone");
            rig.Zone = zoneHost.AddComponent<OnScreenTouchZoneOrnamentProbe>();
            var area = scope.Inactive("Lucid gallery touch area", typeof(RectTransform)).GetComponent<RectTransform>();
            area.sizeDelta = new Vector2(160f, 40f);
            area.localScale = new Vector3(2f, 1f, 1f);
            area.position = new Vector3(500f, 20f, 0f);
            Set(rig.Zone, "m_validTouchTransform", area); // Reflection: serialized m_validTouchTransform the editor assigns
            Set(rig.Zone, "m_restrictToSingleTouch", true); // Reflection: serialized m_restrictToSingleTouch the editor assigns
            Set(rig.Gallery, "m_onScreenControlZone", rig.Zone); // Reflection: serialized m_onScreenControlZone the editor assigns

            // Every ornament's prefab handle is already finished, so LoadNewOrnament finds it cached.
            var resources = new ResourceManager();
            // Reflection: CallbackHooksEnabled is an internal field of the Addressables ResourceManager; switched off, a created
            // operation completes at once instead of through the hooks its update loop pumps.
            typeof(ResourceManager).GetField("CallbackHooksEnabled", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(resources, false);
            GameObject prefab = owned.Host("Lucid gallery ornament prefab", false);
            // Reflection: the gallery caches its prefab handles in a private dictionary, which the case fills with finished ones.
            var handles = Get<Dictionary<string, AsyncOperationHandle<GameObject>>>(rig.Gallery, "m_ornamentHandleByGuid");
            var created = new List<AsyncOperationHandle<GameObject>>();
            foreach (OrnamentDefinition definition in new[] { rig.S0, rig.S1, rig.S2, rig.C0, rig.A0 })
            {
                AsyncOperationHandle<GameObject> handle = resources.CreateCompletedOperation(prefab, null);
                created.Add(handle);
                handles[definition.GetGUID()] = handle;
            }
            owned.Lease(new CharacterTriggerTestSupport.ReleaseAction(() =>
            {
                foreach (AsyncOperationHandle<GameObject> handle in created)
                    if (handle.IsValid()) resources.Release(handle);
                resources.Dispose();
            }));

            Call(rig.Gallery, "OnEnable");
            body(owned, rig);
        })));

        private static UIWidgetCategoryToggle NewTab(Owned owned, Rig rig, string field)
        {
            var tab = owned.Host("Lucid gallery " + field, false).AddComponent<UIWidgetCategoryToggle>();
            Set(tab, "m_selectedBackground", rig.SelectedBackground); // Reflection: serialized m_selectedBackground the editor assigns
            Set(tab, "m_unselectedBackground", rig.UnselectedBackground); // Reflection: serialized m_unselectedBackground the editor assigns
            Set(tab, "m_selectedIcon", NewSprite(owned)); // Reflection: serialized m_selectedIcon the editor assigns
            Set(tab, "m_unselectedIcon", NewSprite(owned)); // Reflection: serialized m_unselectedIcon the editor assigns
            Set(rig.Gallery, field, tab); // Reflection: serialized tab reference of the field the caller names
            return tab;
        }

        private static List<UIWidgetOrnamentEntryRow> Rows(Rig rig)
        {
            var rows = new List<UIWidgetOrnamentEntryRow>();
            for (int i = 0; i < rig.Composer.transform.childCount; i++)
                rows.Add(rig.Composer.transform.GetChild(i).GetComponent<UIWidgetOrnamentEntryRow>());
            return rows;
        }

        private static List<UIWidgetOrnamentToggle> Toggles(UIWidgetOrnamentEntryRow row) => Get<List<UIWidgetOrnamentToggle>>(row, "m_entriesInThisRow");

        private static OrnamentDefinition Current(Rig rig) => Get<OrnamentDefinition>(rig.Gallery, "m_currentOrnamentDefinition");

        private static void SelectTab(Rig rig, OrnamentCategory category, bool active = true)
        {
            switch (category)
            {
                case OrnamentCategory.Story: rig.Gallery.Action_TabStory(active); break;
                case OrnamentCategory.Challenge: rig.Gallery.Action_TabChallenges(active); break;
                default: rig.Gallery.Action_TabAchiever(active); break;
            }
        }

        // The selected ornament's prefab: an active scene object carrying an Ornament with a counting rotator and a child camera.
        private static GameObject NewOrnamentPrefab(Owned owned, float yRotationOffset, bool pingPong, out OrnamentGalleryRotatorProbe rotator)
        {
            GameObject prefab = owned.Host("Lucid gallery ornament", true);
            rotator = prefab.AddComponent<OrnamentGalleryRotatorProbe>();
            rotator.enabled = false;
            Ornament ornament = prefab.AddComponent<Ornament>();
            Set(ornament, "m_transformRotator", rotator); // Reflection: serialized m_transformRotator the editor assigns
            Set(ornament, "m_yRotationOffset", yRotationOffset); // Reflection: serialized m_yRotationOffset the editor assigns
            if (pingPong)
            {
                Set(rotator, "m_idleAutomation", true); // Reflection: serialized m_idleAutomation the editor assigns
                Set(rotator, "m_pingPongWhenIdle", true); // Reflection: serialized m_pingPongWhenIdle the editor assigns
            }
            owned.Host("Lucid gallery ornament camera", true, prefab.transform).AddComponent<Camera>();
            return prefab;
        }

        // The ornament the gallery would show once the prefab has loaded (OnOrnamentLoaded starts this iterator). Its last request,
        // the scale-up coroutine, is refused on the inactive host.
        private static Ornament ShowOrnament(Owned owned, Rig rig, GameObject prefab)
        {
            var iterator = (IEnumerator)Call(rig.Gallery, "InstantiateOrnament", prefab);
            LogAssert.Expect(LogType.Error, RefusedCoroutine);
            Assert.That(iterator.MoveNext(), Is.False);
            Ornament ornament = Get<Ornament>(rig.Gallery, "m_currentOrnament");
            owned.Track(ornament.gameObject);
            return ornament;
        }

        // dump.cs TypeDefIndex 2299 and the DummyDll rows (fields 0x04002483..0x040024aa).
        [Test]
        public void DeclarationsMatchTheOriginalMetadata()
        {
            var serialized = new[] { typeof(SerializeField) };
            AssertMembers(typeof(UIContainerOrnamentGallery), 0x00100001,
                new (string, int)[]
                {
                    ("m_containerNavigation", 0x0001), ("m_lockedOrnament", 0x0001), ("m_scaleDurationSeconds", 0x0001), ("m_scaleCurve", 0x0001),
                    ("m_yRotationSpeed", 0x0001), ("m_yRotationAtTouchExtent", 0x0001), ("m_holdToSpinThreshold", 0x0001),
                    ("m_onScreenControlZone", 0x0001), ("m_nameLocked", 0x0001), ("m_ornamentsPerRowNotchedDevice", 0x0001),
                    ("m_ornamentsPerRowNoNotchDevice", 0x0001), ("m_scrollRectSelection", 0x0001), ("m_widescreenOffset", 0x0001),
                    ("m_ornamentEntryRowComposer", 0x0001), ("m_toggleGroup", 0x0001), ("m_storyTab", 0x0001), ("m_challengeTab", 0x0001),
                    ("m_achieverTab", 0x0001), ("m_infoPanel", 0x0001), ("m_infoButton", 0x0001), ("m_infoGlyph", 0x0001),
                    ("m_rewardTrackUnlockStringPicker", 0x0001), ("Title", 0x0026), ("OrnamentName", 0x0026), ("InfoText", 0x0026),
                    ("OnNewOrnamentsCountChanged", 0x0001), ("m_ornamentHandleByGuid", 0x0021), ("m_currentCategory", 0x0001),
                    ("m_currentOrnamentDefinition", 0x0001), ("m_currentOrnament", 0x0001), ("m_ornamentManager", 0x0001),
                    ("m_addressableManager", 0x0001), ("m_timeManager", 0x0001), ("m_ornamentWaitHandleCoroutine", 0x0001),
                    ("m_ornamentLoadingCoroutine", 0x0001), ("m_ornamentsPerRow", 0x0001), ("m_ornamentXOffset", 0x0001),
                    ("m_rotationFromButtonInput", 0x0001), ("m_dataManager", 0x0001), ("m_saveManager", 0x0001),
                },
                new (string, int)[]
                {
                    ("add_OnNewOrnamentsCountChanged", 0x0886), ("remove_OnNewOrnamentsCountChanged", 0x0886), ("Start", 0x0081),
                    ("OnEnable", 0x0081), ("LateUpdate", 0x0081), ("Action_RotateOrnament", 0x0086), ("OnDestroy", 0x0081),
                    ("PopulateUI", 0x0081), ("SetupCollectionTab", 0x0081), ("OrnamentSelectionChanged", 0x0081),
                    ("OrnamentHoverChanged", 0x0081), ("SetInfoPanelStatus", 0x0081), ("LoadNewOrnament", 0x0081),
                    ("WaitForOrnamentHandleLoadOrnament", 0x0081), ("OnOrnamentLoaded", 0x0081), ("InstantiateOrnament", 0x0081),
                    ("ScaleWithCurve", 0x0081), ("Action_ToggleInfoPanel", 0x0086), ("Action_TabStory", 0x0086),
                    ("Action_TabChallenges", 0x0086), ("Action_TabAchiever", 0x0086), (".ctor", 0x1886),
                });
            AssertIl2CppOptions(typeof(UIContainerOrnamentGallery), 1, 2);
            Assert.That(typeof(UIContainerOrnamentGallery).BaseType, Is.EqualTo(typeof(UIContainer)));
            Type[] nested = typeof(UIContainerOrnamentGallery).GetNestedTypes(BindingFlags.NonPublic);
            Assert.That(Array.ConvertAll(nested, type => type.Name),
                Is.EquivalentTo(new[] { "<>c", "<InstantiateOrnament>d__55", "<ScaleWithCurve>d__56", "<WaitForOrnamentHandleLoadOrnament>d__53" }),
                "the compiler-generated names follow the original member ordinals");
            foreach (string field in new[]
                { "m_containerNavigation", "m_scaleDurationSeconds", "m_scaleCurve", "m_yRotationSpeed", "m_onScreenControlZone", "m_scrollRectSelection", "m_toggleGroup" })
                Assert.That(AttributeTypes(typeof(UIContainerOrnamentGallery).GetField(field, Declared)), Is.EqualTo(serialized), field);
            Assert.That(AttributeTypes(typeof(UIContainerOrnamentGallery).GetField("m_nameLocked", Declared)),
                Is.EqualTo(new[] { typeof(HashEnumAttribute), typeof(HeaderAttribute), typeof(SerializeField) }));
            Assert.That(AttributeTypes(typeof(UIContainerOrnamentGallery).GetField("m_lockedOrnament", Declared)),
                Is.EqualTo(new[] { typeof(HeaderAttribute), typeof(SerializeField) }));
            Assert.That(AttributeTypes(typeof(UIContainerOrnamentGallery).GetField("m_infoPanel", Declared)),
                Is.EqualTo(new[] { typeof(HeaderAttribute), typeof(SerializeField) }));
        }

        // The constructor 0x66F500: the serialized defaults 4, 3 and 0.35 (0x3EB33333), the three bindings, an empty-bodied
        // OnNewOrnamentsCountChanged handler, an empty handle table, and the achievement category as the first one shown.
        [Test]
        public void TheConstructorSetsTheAuthoredDefaults()
        {
            Owned.Run(owned =>
            {
                var gallery = owned.Host("Lucid ornament gallery", false).AddComponent<UIContainerOrnamentGallery>();

                Assert.That(Get<int>(gallery, "m_ornamentsPerRowNotchedDevice"), Is.EqualTo(4));
                Assert.That(Get<int>(gallery, "m_ornamentsPerRowNoNotchDevice"), Is.EqualTo(3));
                Assert.That(Get<float>(gallery, "m_widescreenOffset"), Is.EqualTo(0.35f));
                Assert.That(Get<OrnamentCategory>(gallery, "m_currentCategory"), Is.EqualTo(OrnamentCategory.Achievement));
                Assert.That(gallery.Title, Is.Not.Null);
                Assert.That(gallery.OrnamentName, Is.Not.Null);
                Assert.That(gallery.InfoText, Is.Not.Null);
                Assert.That(Get<Dictionary<string, AsyncOperationHandle<GameObject>>>(gallery, "m_ornamentHandleByGuid"), Is.Empty);
                var handler = Get<Action>(gallery, "OnNewOrnamentsCountChanged");
                Assert.That(handler, Is.Not.Null);
                Assert.DoesNotThrow(() => handler());
            });
        }

        // OnEnable 0x66D6F0 takes the six services from their rows, publishes the title (Strings 0xBCC4A28B, GLOSSARY_24), initialises
        // the touch zone and shows the achievement category.
        [Test]
        public void EnablingTakesTheServicesAndShowsTheAchievementCategory()
        {
            Run((owned, rig) =>
            {
                Assert.That(Get<OrnamentManager>(rig.Gallery, "m_ornamentManager"), Is.SameAs(rig.Manager));
                Assert.That(Get<AddressableManager>(rig.Gallery, "m_addressableManager"), Is.SameAs(rig.Addressables));
                Assert.That(Get<TimeManager>(rig.Gallery, "m_timeManager"), Is.SameAs(rig.Time));
                Assert.That(Get<DataManager>(rig.Gallery, "m_dataManager"), Is.SameAs(rig.Services.Data));
                Assert.That(Get<SaveManager>(rig.Gallery, "m_saveManager"), Is.SameAs(rig.Services.Save));
                Assert.That(rig.Gallery.Title.Value, Is.EqualTo(Strings.GLOSSARY_24));
                Assert.That(Get<float>(rig.Zone, "m_touchSpan"), Is.EqualTo(320f), "the zone was initialised against its touch area");
                Assert.That(Current(rig), Is.SameAs(rig.A0));
                Assert.That(rig.Composer.transform.childCount, Is.EqualTo(1));
                Assert.That(Toggles(Rows(rig)[0]).Count, Is.EqualTo(1));
            });
        }

        [Test]
        public void TheRowLengthAndTheOffsetFollowTheScreenShape()
        {
            Run((owned, rig) =>
            {
                Set(rig.Gallery, "m_ornamentsPerRowNotchedDevice", 5); // Reflection: serialized m_ornamentsPerRowNotchedDevice the editor assigns
                Set(rig.Gallery, "m_ornamentsPerRowNoNotchDevice", 2); // Reflection: serialized m_ornamentsPerRowNoNotchDevice the editor assigns
                Set(rig.Gallery, "m_widescreenOffset", 0.35f); // Reflection: serialized m_widescreenOffset the editor assigns
                LogAssert.Expect(LogType.Error, RefusedDestroy); // the second OnEnable clears the row the first one made
                Call(rig.Gallery, "OnEnable");

                int perRow = Get<int>(rig.Gallery, "m_ornamentsPerRow");
                float offset = Get<float>(rig.Gallery, "m_ornamentXOffset");
                // Screen cannot be set in Edit Mode, so the branch follows whatever shape the Game view reports. Native thresholds:
                // 1.9f at 0x29A7AE0 (strict, ucomiss/setbe at 0x66D811) and 1.75f at 0x29A7AE4 (strict, ucomiss/jbe at 0x66D828); an
                // equal or NaN aspect takes the no-notch length and zero offset.
                float aspect = (float)Screen.width / (float)Screen.height;
                Assert.That(perRow, Is.EqualTo(aspect > 1.9f ? 5 : 2));
                Assert.That(offset, Is.EqualTo(aspect > 1.75f ? 0.35f : 0f));
            });
        }

        // PopulateUI 0x66D900 for each tab: the old rows are cleared (one refused destroy request for the one row OnEnable made), the
        // category's ornaments are grouped two to a row, the first ornament becomes current and is the default-selected toggle, and the
        // three tabs show their counts of available ornaments and which one is selected.
        [TestCase(OrnamentCategory.Story, 2, 2, 3, TestName = "ChoosingTheStoryTabListsItsThreeOrnamentsInTwoRows")]
        [TestCase(OrnamentCategory.Challenge, 1, 0, 1, TestName = "ChoosingTheChallengeTabListsItsOneOrnament")]
        [TestCase(OrnamentCategory.Achievement, 1, 1, 1, TestName = "ChoosingTheAchieverTabListsItsOneOrnament")]
        public void ChoosingATabListsThatCategoryAndSelectsItsFirstOrnament(OrnamentCategory category, int rowCount, int collected, int total)
        {
            Run((owned, rig) =>
            {
                Assert.That(rig.Composer.transform.childCount, Is.EqualTo(1));
                LogAssert.Expect(LogType.Error, RefusedDestroy);

                SelectTab(rig, category);

                Assert.That(Get<OrnamentCategory>(rig.Gallery, "m_currentCategory"), Is.EqualTo(category));
                // Edit Mode refused the destroy, so the earlier row is still the first child; the new rows follow it.
                List<UIWidgetOrnamentEntryRow> rows = Rows(rig);
                Assert.That(rows.Count, Is.EqualTo(1 + rowCount));
                OrnamentDefinition first = category == OrnamentCategory.Story ? rig.S0 : category == OrnamentCategory.Challenge ? rig.C0 : rig.A0;
                Assert.That(Current(rig), Is.SameAs(first));
                UIWidgetOrnamentToggle firstToggle = Toggles(rows[1])[0];
                Assert.That(Get<OrnamentDefinition>(firstToggle, "m_definition"), Is.SameAs(first));
                Assert.That(firstToggle.GetComponent<UIToggle>().IsOn, Is.True, "the current ornament is the default selection");
                UIWidgetCategoryToggle selectedTab = category == OrnamentCategory.Story ? rig.StoryTab : category == OrnamentCategory.Challenge ? rig.ChallengeTab : rig.AchieverTab;
                foreach (UIWidgetCategoryToggle tab in new[] { rig.StoryTab, rig.ChallengeTab, rig.AchieverTab })
                    Assert.That(tab.IsSelectedBackground.Value, Is.SameAs(tab == selectedTab ? rig.SelectedBackground : rig.UnselectedBackground));
                Assert.That(selectedTab.Collected.Value, Is.EqualTo(collected));
                Assert.That(selectedTab.Total.Value, Is.EqualTo(total));
            });
        }

        // The story tab's rows hold two toggles and then one, in the manager's order, and only the first toggle is selected.
        [Test]
        public void TheStoryOrnamentsFillTheRowsInOrder()
        {
            Run((owned, rig) =>
            {
                LogAssert.Expect(LogType.Error, RefusedDestroy);
                rig.Gallery.Action_TabStory(true);

                List<UIWidgetOrnamentEntryRow> rows = Rows(rig);
                Assert.That(rows.Count, Is.EqualTo(3));
                Assert.That(Toggles(rows[1]).Count, Is.EqualTo(2));
                Assert.That(Toggles(rows[2]).Count, Is.EqualTo(1));
                Assert.That(Get<OrnamentDefinition>(Toggles(rows[1])[0], "m_definition"), Is.SameAs(rig.S0));
                Assert.That(Get<OrnamentDefinition>(Toggles(rows[1])[1], "m_definition"), Is.SameAs(rig.S1));
                Assert.That(Get<OrnamentDefinition>(Toggles(rows[2])[0], "m_definition"), Is.SameAs(rig.S2));
                Assert.That(Toggles(rows[1])[0].GetComponent<UIToggle>().IsOn, Is.True);
                Assert.That(Toggles(rows[1])[1].GetComponent<UIToggle>().IsOn, Is.False);
                Assert.That(Toggles(rows[2])[0].GetComponent<UIToggle>().IsOn, Is.False);
            });
        }

        // A tab switched off (the toggle group sends false to the tab it leaves) changes nothing: no clearing and no new rows.
        [TestCase(OrnamentCategory.Story, TestName = "TheStoryTabSwitchedOffChangesNothing")]
        [TestCase(OrnamentCategory.Challenge, TestName = "TheChallengeTabSwitchedOffChangesNothing")]
        [TestCase(OrnamentCategory.Achievement, TestName = "TheAchieverTabSwitchedOffChangesNothing")]
        public void ATabSwitchedOffChangesNothing(OrnamentCategory category)
        {
            Run((owned, rig) =>
            {
                SelectTab(rig, category, false);

                Assert.That(rig.Composer.transform.childCount, Is.EqualTo(1));
                Assert.That(Current(rig), Is.SameAs(rig.A0));
                Assert.That(Get<OrnamentCategory>(rig.Gallery, "m_currentCategory"), Is.EqualTo(OrnamentCategory.Achievement));
            });
        }

        // The current ornament is forgotten when a tab is chosen (0x66F454): the new category does not keep an ornament of another one.
        [Test]
        public void ChoosingATabForgetsTheCurrentOrnament()
        {
            Run((owned, rig) =>
            {
                Assert.That(Current(rig), Is.SameAs(rig.A0));
                LogAssert.Expect(LogType.Error, RefusedDestroy);
                rig.Gallery.Action_TabChallenges(true);
                Assert.That(Current(rig), Is.SameAs(rig.C0));
            });
        }

        // PopulateUI shows the current ornament by its Unlocked flag (0x66DD79) but the default selection that follows reports its
        // availability (Unlocked and Collected): an unlocked, uncollected ornament ends up shown locked, with the story act tip.
        [Test]
        public void AnUncollectedOrnamentEndsUpShownLocked()
        {
            Run((owned, rig) =>
            {
                rig.Story[rig.S0.OrnamentIdentifier] = Status(true, true, false);
                LogAssert.Expect(LogType.Error, RefusedDestroy);
                var names = new List<Strings>();
                var texts = new List<string>();
                rig.Gallery.OrnamentName.AddListener(() => names.Add(rig.Gallery.OrnamentName.Value));
                rig.Gallery.InfoText.AddListener(() => texts.Add(rig.Gallery.InfoText.Value));

                rig.Gallery.Action_TabStory(true);

                // PopulateUI passes the Unlocked flag (0x66DD69: movzx of the first status byte), so the ornament is shown unlocked
                // first; the default selection then reports it unavailable and overwrites it with the locked view.
                Assert.That(names, Is.EqualTo(new[] { (Strings)NameS0, (Strings)NameLocked }));
                Assert.That(texts, Is.EqualTo(new[] { "Lore S0", "Unlock S0 in act 0" }));
                Assert.That(rig.Gallery.OrnamentName.Value, Is.EqualTo((Strings)NameLocked));
                Assert.That(rig.InfoPanel.activeSelf, Is.True);
                Assert.That(rig.UiButton.interactable, Is.False);
                Assert.That(rig.InfoGlyph.activeSelf, Is.False);
                Assert.That(rig.Gallery.InfoText.Value, Is.EqualTo("Unlock S0 in act 0"));
            });
        }

        // SetupCollectionTab 0x66E8F0 counts the category's available ornaments: unlocked ones that are not collected do not count.
        [Test]
        public void TheTabCountsOnlyAvailableOrnaments()
        {
            Run((owned, rig) =>
            {
                rig.Story[rig.S1.OrnamentIdentifier] = Status(true, true, false);
                Call(rig.Gallery, "SetupCollectionTab", rig.StoryTab, OrnamentCategory.Challenge, OrnamentCategory.Story);

                Assert.That(rig.StoryTab.Collected.Value, Is.EqualTo(2), "S0 and S2 are available; S1 is unlocked but not collected");
                Assert.That(rig.StoryTab.Total.Value, Is.EqualTo(3));
                Assert.That(rig.StoryTab.IsSelectedBackground.Value, Is.SameAs(rig.UnselectedBackground), "another category is active");
            });
        }

        // OrnamentSelectionChanged 0x66EC20: the ornament becomes current; only an available ornament that is new is marked seen in the
        // save and the manager and refreshes the tabs and the new-ornaments count.
        [TestCase(true, true, true, TestName = "SelectingANewAvailableOrnamentMarksItSeen")]
        [TestCase(true, false, false, TestName = "SelectingASeenOrnamentChangesNothing")]
        [TestCase(false, true, false, TestName = "SelectingANewUnavailableOrnamentChangesNothing")]
        [TestCase(false, false, false, TestName = "SelectingASeenUnavailableOrnamentChangesNothing")]
        public void SelectingAnOrnamentMarksOnlyANewAvailableOneSeen(bool available, bool isNew, bool marks)
        {
            Run((owned, rig) =>
            {
                rig.StoryTab.Setup(new UIWidgetCategoryToggleParameters(false, 2, 3, OrnamentCategory.Story));
                Assert.That(rig.StoryTab.HasNewUnlock.Value, Is.True, "S2 is available and unseen");
                int countChanges = 0;
                rig.Gallery.OnNewOrnamentsCountChanged += () => countChanges++;

                Call(rig.Gallery, "OrnamentSelectionChanged", rig.S2, available, isNew);

                Assert.That(Current(rig), Is.SameAs(rig.S2));
                Assert.That(rig.Services.Save.CurrentSave.GetOrCreateOrnamentData(rig.S2.GetGUID()).UnlockSeen, Is.EqualTo(marks));
                OrnamentManager.OrnamentStatus status = rig.Story[rig.S2.OrnamentIdentifier];
                Assert.That(status.Seen, Is.EqualTo(marks));
                Assert.That(status.Unlocked, Is.True);
                Assert.That(status.Collected, Is.True);
                Assert.That(rig.StoryTab.HasNewUnlock.Value, Is.EqualTo(!marks));
                Assert.That(countChanges, Is.EqualTo(marks ? 1 : 0));
                Assert.That(rig.Gallery.OrnamentName.Value, Is.EqualTo(available ? (Strings)NameS2 : (Strings)NameLocked));
            });
        }

        // The seen ornament keeps its collected flag unless it is a challenge reward still waiting to be collected (0x66ECC6..0x66ECD4).
        [TestCase(0, true, TestName = "ASeenOrnamentWithoutARewardRecordStaysCollected")]
        [TestCase(1, false, TestName = "ASeenOrnamentWhoseRewardIsNotCollectedYetIsNotCollected")]
        [TestCase(2, true, TestName = "ASeenOrnamentWhoseRewardIsCollectedStaysCollected")]
        public void TheSeenOrnamentTakesItsRewardsCollectedState(int reward, bool collected)
        {
            Run((owned, rig) =>
            {
                if (reward != 0)
                    rig.Services.AddRecord("lucid-gallery-track", rig.S2.GetGUID()).Collected = reward == 2;

                Call(rig.Gallery, "OrnamentSelectionChanged", rig.S2, true, true);

                OrnamentManager.OrnamentStatus status = rig.Story[rig.S2.OrnamentIdentifier];
                Assert.That(status.Seen, Is.True);
                Assert.That(status.Collected, Is.EqualTo(collected));
            });
        }

        // SetInfoPanelStatus 0x66E6D0. An unlocked ornament shows its own name and lore, the info glyph and a hidden panel; a locked one
        // shows the locked name, the panel and no glyph, with the tip of its category: the story tip takes the act number, the challenge
        // tip is the plain tip (no reward string picker is assigned) and so is the achievement tip.
        [TestCase(0, true, NameS0, "Lore S0", TestName = "AnUnlockedOrnamentShowsItsNameAndLore")]
        [TestCase(1, false, NameLocked, "Reach act 3", TestName = "ALockedStoryOrnamentShowsTheActTip")]
        [TestCase(3, false, NameLocked, "Tip C0", TestName = "ALockedChallengeOrnamentWithoutAPickerShowsThePlainTip")]
        [TestCase(4, false, NameLocked, "Tip A0", TestName = "ALockedAchievementOrnamentShowsThePlainTip")]
        public void TheInfoPanelFollowsTheOrnamentsLockState(int index, bool unlocked, int name, string text)
        {
            Run((owned, rig) =>
            {
                OrnamentDefinition definition = new[] { rig.S0, rig.S1, rig.S2, rig.C0, rig.A0 }[index];

                Call(rig.Gallery, "SetInfoPanelStatus", definition, unlocked);

                Assert.That(rig.Gallery.OrnamentName.Value, Is.EqualTo((Strings)name));
                Assert.That(rig.Gallery.InfoText.Value, Is.EqualTo(text));
                Assert.That(rig.InfoPanel.activeSelf, Is.EqualTo(!unlocked));
                Assert.That(rig.UiButton.interactable, Is.EqualTo(unlocked));
                Assert.That(rig.InfoGlyph.activeSelf, Is.EqualTo(unlocked));
            });
        }

        // Action_ToggleInfoPanel 0x66F3F0 flips the panel only while the info button is interactive.
        [Test]
        public void TheInfoButtonTogglesThePanelOnlyWhileItIsInteractive()
        {
            Run((owned, rig) =>
            {
                rig.UiButton.interactable = true;
                rig.InfoPanel.SetActive(false);
                rig.Gallery.Action_ToggleInfoPanel();
                Assert.That(rig.InfoPanel.activeSelf, Is.True);
                rig.Gallery.Action_ToggleInfoPanel();
                Assert.That(rig.InfoPanel.activeSelf, Is.False);

                rig.UiButton.interactable = false;
                rig.Gallery.Action_ToggleInfoPanel();
                Assert.That(rig.InfoPanel.activeSelf, Is.False, "not interactive");
                rig.InfoPanel.SetActive(true);
                rig.Gallery.Action_ToggleInfoPanel();
                Assert.That(rig.InfoPanel.activeSelf, Is.True, "not interactive");
            });
        }

        // Start 0x66D670 wires the info button's pointer-up to the panel toggle.
        [Test]
        public void StartConnectsTheInfoButtonToThePanelToggle()
        {
            Run((owned, rig) =>
            {
                rig.UiButton.interactable = true;
                rig.InfoPanel.SetActive(false);

                Call(rig.Gallery, "Start");
                rig.InfoButton.OnPointerUp(null);

                Assert.That(rig.InfoPanel.activeSelf, Is.True);
            });
        }

        // InstantiateOrnament 0x66F7F0 with no ornament yet: the prefab is cloned, placed at the x offset, turned by the prefab's y
        // offset (unless it ping-pongs), hidden at zero scale, given the idle spin and deactivated; the scale-up is requested. The
        // clone's cameras are then switched off: GetComponentsInChildren<Camera>() still returns the camera of the deactivated clone
        // (checked in a bare Unity 2022.3.54f1 Edit Mode project), and the prefab's own camera is left alone.
        [TestCase(false, TestName = "AnOrnamentIsPlacedTurnedByItsYOffsetAndHiddenAtZeroScale")]
        [TestCase(true, TestName = "APingPongingOrnamentIsNotTurnedByItsYOffset")]
        public void TheOrnamentIsPlacedTurnedAndHiddenBeforeItScalesUp(bool pingPong)
        {
            Run((owned, rig) =>
            {
                Set(rig.Gallery, "m_yRotationSpeed", 30f); // Reflection: serialized m_yRotationSpeed the editor assigns
                GameObject prefab = NewOrnamentPrefab(owned, 90f, pingPong, out OrnamentGalleryRotatorProbe prefabRotator);
                float xOffset = Get<float>(rig.Gallery, "m_ornamentXOffset");

                Ornament ornament = ShowOrnament(owned, rig, prefab);

                Assert.That(ornament.gameObject, Is.Not.SameAs(prefab));
                Assert.That(ornament.transform.position, Is.EqualTo(new Vector3(xOffset, 0f, 0f)));
                Quaternion expected = pingPong ? Quaternion.identity : Quaternion.Euler(0f, 90f, 0f);
                Assert.That(Quaternion.Angle(ornament.transform.localRotation, expected), Is.LessThan(0.001f));
                Assert.That(ornament.transform.localScale, Is.EqualTo(Vector3.zero));
                Assert.That(ornament.TransformRotator.enabled, Is.True, "the prefab's rotator was off");
                Assert.That(Get<Vector3>(ornament.TransformRotator, "m_idleRotationSpeed"), Is.EqualTo(new Vector3(0f, 30f, 0f)));
                Assert.That(ornament.gameObject.activeSelf, Is.False);
                Assert.That(ornament.GetComponentInChildren<Camera>(true).enabled, Is.False, "the clone's camera is switched off");
                Assert.That(prefab.GetComponentInChildren<Camera>(true).enabled, Is.True, "the prefab's camera is not");
                Assert.That(Get<Coroutine>(rig.Gallery, "m_ornamentLoadingCoroutine"), Is.Null);
            });
        }

        // With an ornament showing, the new one waits for the old one's scale-down (the iterator yields it), and only after that is the
        // old one destroyed (a refused request here) and replaced.
        [Test]
        public void ANewOrnamentWaitsForTheOldOnesScaleDownThenReplacesIt()
        {
            Run((owned, rig) =>
            {
                GameObject prefab = NewOrnamentPrefab(owned, 0f, false, out _);
                Ornament old = ShowOrnament(owned, rig, prefab);

                var iterator = (IEnumerator)Call(rig.Gallery, "InstantiateOrnament", prefab);
                Assert.That(iterator.MoveNext(), Is.True);
                Assert.That(iterator.Current.GetType().Name, Is.EqualTo("<ScaleWithCurve>d__56"));
                Assert.That(Get<Ornament>(rig.Gallery, "m_currentOrnament"), Is.SameAs(old), "still showing the old ornament");

                LogAssert.Expect(LogType.Error, RefusedDestroy);
                LogAssert.Expect(LogType.Error, RefusedCoroutine);
                Assert.That(iterator.MoveNext(), Is.False);
                Ornament replacement = Get<Ornament>(rig.Gallery, "m_currentOrnament");
                owned.Track(replacement.gameObject);
                Assert.That(replacement, Is.Not.SameAs(old));
            });
        }

        // ScaleWithCurve 0x66FCA0 with no time to run (the duration is zero): the ornament is switched on and ends at full size when
        // scaling up and at zero when scaling down, whatever size it had.
        [TestCase(true, 1f, TestName = "ScalingUpEndsAtFullSize")]
        [TestCase(false, 0f, TestName = "ScalingDownEndsAtZero")]
        public void ScalingEndsAtItsTargetSize(bool scaleUp, float expected)
        {
            Run((owned, rig) =>
            {
                Ornament ornament = ShowOrnament(owned, rig, NewOrnamentPrefab(owned, 0f, false, out _));
                ornament.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
                Assert.That(ornament.gameObject.activeSelf, Is.False);

                var iterator = (IEnumerator)Call(rig.Gallery, "ScaleWithCurve", scaleUp);

                Assert.That(iterator.MoveNext(), Is.False);
                Assert.That(ornament.gameObject.activeSelf, Is.True);
                Assert.That(ornament.transform.localScale, Is.EqualTo(new Vector3(expected, expected, expected)));
            });
        }

        // The first step of a timed scale (the clock is not advanced between steps): scaling up applies the curve at 0 times full size 1;
        // scaling down applies the curve at 1 (the progress is inverted) times the ornament's present x scale.
        [TestCase(true, 0f, 0.8f, 0.8f, TestName = "AScaleUpStepAppliesTheCurveAtTheStart")]
        [TestCase(false, 0.5f, 0.8f, 0.4f, TestName = "AScaleDownStepStartsFromThePresentScale")]
        public void ATimedScaleStepAppliesTheCurveToItsBound(bool scaleUp, float presentScale, float curveValue, float expected)
        {
            Run((owned, rig) =>
            {
                Set(rig.Gallery, "m_scaleDurationSeconds", 1000f); // Reflection: serialized m_scaleDurationSeconds the editor assigns
                Set(rig.Gallery, "m_scaleCurve", AnimationCurve.Constant(0f, 1f, curveValue)); // Reflection: serialized m_scaleCurve the editor assigns
                Ornament ornament = ShowOrnament(owned, rig, NewOrnamentPrefab(owned, 0f, false, out _));
                ornament.transform.localScale = new Vector3(presentScale, presentScale, presentScale);

                var iterator = (IEnumerator)Call(rig.Gallery, "ScaleWithCurve", scaleUp);

                Assert.That(iterator.MoveNext(), Is.True);
                Assert.That(iterator.Current, Is.Null);
                Assert.That(ornament.transform.localScale, Is.EqualTo(new Vector3(expected, expected, expected)));
            });
        }

        // A scale-down reads the curve at the inverted progress: with a curve rising from 0 to 0.5 the first step is at 0.5 (not at 0)
        // times the present scale 0.8.
        [Test]
        public void AScaleDownReadsTheCurveAtTheInvertedProgress()
        {
            Run((owned, rig) =>
            {
                Set(rig.Gallery, "m_scaleDurationSeconds", 1000f); // Reflection: serialized m_scaleDurationSeconds the editor assigns
                Set(rig.Gallery, "m_scaleCurve", AnimationCurve.Linear(0f, 0f, 1f, 0.5f)); // Reflection: serialized m_scaleCurve the editor assigns
                Ornament ornament = ShowOrnament(owned, rig, NewOrnamentPrefab(owned, 0f, false, out _));
                ornament.transform.localScale = new Vector3(0.8f, 0.8f, 0.8f);

                Assert.That(((IEnumerator)Call(rig.Gallery, "ScaleWithCurve", false)).MoveNext(), Is.True);

                Assert.That(ornament.transform.localScale.x, Is.EqualTo(0.4f).Within(1e-6f));
                Assert.That(ornament.transform.localScale.z, Is.EqualTo(0.4f).Within(1e-6f));
            });
        }

        // WaitForOrnamentHandleLoadOrnament 0x66FF90 yields the handle itself and, once it has resumed, shows the loaded prefab.
        [Test]
        public void WaitingForAHandleYieldsItThenShowsItsPrefab()
        {
            Run((owned, rig) =>
            {
                AsyncOperationHandle<GameObject> handle = Get<Dictionary<string, AsyncOperationHandle<GameObject>>>(rig.Gallery, "m_ornamentHandleByGuid")[rig.S0.GetGUID()];

                var iterator = (IEnumerator)Call(rig.Gallery, "WaitForOrnamentHandleLoadOrnament", handle);

                Assert.That(iterator.MoveNext(), Is.True);
                Assert.That(iterator.Current, Is.EqualTo(handle));
                Assert.That(iterator.MoveNext(), Is.False);
                Assert.That(Get<Coroutine>(rig.Gallery, "m_ornamentWaitHandleCoroutine"), Is.Null);
            });
        }

        // Action_RotateOrnament 0x66E3F0 only stores the speed. LateUpdate 0x66E080 with a positive stored speed hands it to the
        // ornament's rotator as its impulse speed, clears it and does nothing else.
        [Test]
        public void ButtonRotationBecomesTheRotatorsImpulseOnce()
        {
            Run((owned, rig) =>
            {
                Ornament ornament = ShowOrnament(owned, rig, NewOrnamentPrefab(owned, 0f, false, out _));
                rig.Gallery.Action_RotateOrnament(3.5f);
                Assert.That(Get<float>(rig.Gallery, "m_rotationFromButtonInput"), Is.EqualTo(3.5f));
                Assert.That(ornament.TransformRotator.CurrentImpulseSpeed, Is.EqualTo(0f));

                Call(rig.Gallery, "LateUpdate");

                Assert.That(ornament.TransformRotator.CurrentImpulseSpeed, Is.EqualTo(3.5f));
                Assert.That(Get<float>(rig.Gallery, "m_rotationFromButtonInput"), Is.EqualTo(0f));
                Assert.That(Get<bool>(ornament.TransformRotator, "m_idleAutomation"), Is.False, "the zone path was not taken");
            });
        }

        // 0x66E0E7..0x66E0ED is xorps; ucomiss; jbe: a zero, negative or NaN stored speed falls through to the touch-zone path and the
        // stored value is kept.
        [Test]
        public void ANegativeButtonRotationIsNotAnImpulse()
        {
            Run((owned, rig) =>
            {
                Ornament ornament = ShowOrnament(owned, rig, NewOrnamentPrefab(owned, 0f, false, out _));
                rig.Gallery.Action_RotateOrnament(-3.5f);

                Call(rig.Gallery, "LateUpdate");

                Assert.That(Get<float>(rig.Gallery, "m_rotationFromButtonInput"), Is.EqualTo(-3.5f));
                Assert.That(ornament.TransformRotator.CurrentImpulseSpeed, Is.EqualTo(0f));
                Assert.That(Get<bool>(ornament.TransformRotator, "m_idleAutomation"), Is.True, "the zone path ran with no drag");
            });
        }

        // LateUpdate without an ornament does nothing, even with a stored button speed.
        [Test]
        public void LateUpdateWithoutAnOrnamentDoesNothing()
        {
            Run((owned, rig) =>
            {
                rig.Gallery.Action_RotateOrnament(2f);
                Call(rig.Gallery, "LateUpdate");
                Assert.That(Get<float>(rig.Gallery, "m_rotationFromButtonInput"), Is.EqualTo(2f));
            });
        }

        // With no finger down (the drag amount is NaN) the rotator goes back to its idle automation and the finger's release impulse
        // is applied: a positive impulse reverses the spin, a negative one restores it, and the speeds add up.
        [Test]
        public void ReleasingTheFingerAppliesTheReleaseImpulseToTheRotator()
        {
            Run((owned, rig) =>
            {
                Ornament ornament = ShowOrnament(owned, rig, NewOrnamentPrefab(owned, 0f, false, out _));
                SmoothedTransformRotator rotator = ornament.TransformRotator;

                Call(rig.Gallery, "LateUpdate");
                Assert.That(Get<bool>(rotator, "m_idleAutomation"), Is.True, "automation resumes");
                Assert.That(Get<bool>(rotator, "m_smoothingEnabled"), Is.False);
                Assert.That(rotator.CurrentImpulseSpeed, Is.EqualTo(0f), "no impulse is applied for a zero release");

                rig.Zone.StartTrackingTouch(TouchAt(80f, 20f));
                rig.Zone.SupplyTouchData();
                rig.Zone.UpdateTouch(TouchAt(100f, 20f));
                rig.Zone.SupplyTouchData();
                rig.Zone.StopTrackingTouch(TouchAt(100f, 20f));
                rig.Zone.SupplyTouchData();
                Call(rig.Gallery, "LateUpdate");
                Assert.That(rotator.CurrentImpulseSpeed, Is.EqualTo(20f));
                Assert.That(Get<object>(rotator, "m_rotationDirection").ToString(), Is.EqualTo("Reversed"));

                rig.Zone.StartTrackingTouch(TouchAt(80f, 20f));
                rig.Zone.SupplyTouchData();
                rig.Zone.UpdateTouch(TouchAt(30f, 20f));
                rig.Zone.SupplyTouchData();
                rig.Zone.StopTrackingTouch(TouchAt(30f, 20f));
                rig.Zone.SupplyTouchData();
                Call(rig.Gallery, "LateUpdate");
                Assert.That(rotator.CurrentImpulseSpeed, Is.EqualTo(70f));
                Assert.That(Get<object>(rotator, "m_rotationDirection").ToString(), Is.EqualTo("Standard"));
            });
        }

        // A drag (0.25 of the touch area's width) below the hold-to-spin threshold turns the ornament directly: automation off, the
        // ornament turned by 2 * 90 * -0.25 = -45 degrees about the up axis, and the rotator updated once with that yaw (315).
        [Test]
        public void DraggingTurnsTheOrnamentDirectlyByTheDragAmount()
        {
            Run((owned, rig) =>
            {
                Set(rig.Gallery, "m_yRotationAtTouchExtent", 90f); // Reflection: serialized m_yRotationAtTouchExtent the editor assigns
                Set(rig.Gallery, "m_holdToSpinThreshold", 10f); // Reflection: serialized m_holdToSpinThreshold the editor assigns
                GameObject prefab = NewOrnamentPrefab(owned, 0f, false, out _);
                Ornament ornament = ShowOrnament(owned, rig, prefab);
                var rotator = (OrnamentGalleryRotatorProbe)ornament.TransformRotator;
                Call(rotator, "Awake"); // the engine message that caches the transform
                rig.Zone.StartTrackingTouch(TouchAt(80f, 20f));
                rig.Zone.SupplyTouchData();
                Assert.That(rig.Zone.DragAmount, Is.EqualTo(0.25f));

                Call(rig.Gallery, "LateUpdate");

                Assert.That(Get<bool>(rotator, "m_idleAutomation"), Is.False);
                Assert.That(Get<bool>(rotator, "m_smoothingEnabled"), Is.True);
                Assert.That(Quaternion.Angle(ornament.transform.rotation, Quaternion.AngleAxis(-45f, Vector3.up)), Is.LessThan(0.01f));
                Assert.That(Mathf.Abs(Mathf.DeltaAngle(ornament.transform.eulerAngles.y, 315f)), Is.LessThan(0.01f));
                Assert.That(rotator.SetRotationCalls, Is.EqualTo(1), "one rotator update");
            });
        }

        // 0x66E143..0x66E149: a drag only turns the ornament directly while the rotator's impulse speed is below the threshold; at or
        // above it the rotator keeps spinning (automation on, the release impulse polled).
        [TestCase(30f, 20f, true, TestName = "ASpinningOrnamentAboveTheThresholdIsNotDragged")]
        [TestCase(20f, 20f, true, TestName = "AnImpulseEqualToTheThresholdIsNotDragged")]
        [TestCase(10f, 20f, false, TestName = "AnImpulseBelowTheThresholdIsDragged")]
        public void ADragNeedsTheImpulseSpeedBelowTheHoldToSpinThreshold(float impulse, float threshold, bool spinning)
        {
            Run((owned, rig) =>
            {
                Set(rig.Gallery, "m_holdToSpinThreshold", threshold); // Reflection: serialized m_holdToSpinThreshold the editor assigns
                Ornament ornament = ShowOrnament(owned, rig, NewOrnamentPrefab(owned, 0f, false, out _));
                var rotator = (OrnamentGalleryRotatorProbe)ornament.TransformRotator;
                Call(rotator, "Awake");
                rotator.CurrentImpulseSpeed = impulse;
                rig.Zone.StartTrackingTouch(TouchAt(80f, 20f));
                rig.Zone.SupplyTouchData();

                Call(rig.Gallery, "LateUpdate");

                Assert.That(Get<bool>(rotator, "m_idleAutomation"), Is.EqualTo(spinning));
                Assert.That(rotator.SetRotationCalls, Is.EqualTo(spinning ? 0 : 1));
            });
        }

        // 0x66E14D..0x66E2DF: the drag branch switches the rotator's automation off before it asks whether the rotator ping-pongs when
        // idle (that property needs the automation), so the ping-pong extent check never applies and the rotator is updated whatever
        // the extent: a quirk of the shipped build.
        [TestCase(10f, TestName = "ADragIgnoresASmallPingPongExtent")]
        [TestCase(360f, TestName = "ADragIgnoresALargePingPongExtent")]
        public void ADragSwitchesTheAutomationOffSoThePingPongExtentNeverApplies(float extent)
        {
            Run((owned, rig) =>
            {
                Set(rig.Gallery, "m_yRotationAtTouchExtent", 90f); // Reflection: serialized m_yRotationAtTouchExtent the editor assigns
                Set(rig.Gallery, "m_holdToSpinThreshold", 10f); // Reflection: serialized m_holdToSpinThreshold the editor assigns
                GameObject prefab = NewOrnamentPrefab(owned, 0f, true, out OrnamentGalleryRotatorProbe prefabRotator);
                Set(prefabRotator, "m_pingPongToDegrees", extent); // Reflection: serialized m_pingPongToDegrees the editor assigns
                Ornament ornament = ShowOrnament(owned, rig, prefab);
                var rotator = (OrnamentGalleryRotatorProbe)ornament.TransformRotator;
                Call(rotator, "Awake");
                rig.Zone.StartTrackingTouch(TouchAt(80f, 20f));
                rig.Zone.SupplyTouchData();

                Call(rig.Gallery, "LateUpdate");

                Assert.That(Get<bool>(rotator, "m_idleAutomation"), Is.False, "switched off by the drag");
                Assert.That(Quaternion.Angle(ornament.transform.rotation, Quaternion.AngleAxis(-45f, Vector3.up)), Is.LessThan(0.01f));
                Assert.That(rotator.SetRotationCalls, Is.EqualTo(1));
            });
        }

        // OnDestroy 0x66E400 destroys the shown ornament (a refused request here), releases the cached handles and requests a save
        // (a refused coroutine here). The handles held here are empty ones: releasing a live Addressables handle is not covered.
        [Test]
        public void DestroyingTheGalleryDestroysTheOrnamentAndRequestsASave()
        {
            Run((owned, rig) =>
            {
                Ornament ornament = ShowOrnament(owned, rig, NewOrnamentPrefab(owned, 0f, false, out _));
                var handles = Get<Dictionary<string, AsyncOperationHandle<GameObject>>>(rig.Gallery, "m_ornamentHandleByGuid");
                foreach (string key in new List<string>(handles.Keys))
                    handles[key] = default;

                LogAssert.Expect(LogType.Error, RefusedDestroy);
                LogAssert.Expect(LogType.Error, RefusedCoroutine);
                Call(rig.Gallery, "OnDestroy");

                Assert.That(Get<Ornament>(rig.Gallery, "m_currentOrnament"), Is.Null);
                Assert.That(ornament != null, Is.True, "Edit Mode refused the destroy");
            });
        }

        // add/remove of OnNewOrnamentsCountChanged 0x66D530/0x66D5D0 add and remove handlers from one multicast delegate.
        [Test]
        public void TheCountChangedEventAddsAndRemovesHandlers()
        {
            Owned.Run(owned =>
            {
                var gallery = owned.Host("Lucid ornament gallery", false).AddComponent<UIContainerOrnamentGallery>();
                int calls = 0;
                Action handler = () => calls++;
                gallery.OnNewOrnamentsCountChanged += handler;
                Get<Action>(gallery, "OnNewOrnamentsCountChanged")();
                Assert.That(calls, Is.EqualTo(1));

                gallery.OnNewOrnamentsCountChanged -= handler;
                Get<Action>(gallery, "OnNewOrnamentsCountChanged")();
                Assert.That(calls, Is.EqualTo(1));
            });
        }
    }
}
