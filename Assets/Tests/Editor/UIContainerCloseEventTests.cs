using System;
using System.Collections.Generic;
using Hardlight;
using Hardlight.Enums;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AddressableAssets;
using static ProjectLucid.Tests.UiPublishingFixtures;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // UIContainerWithCloseEvent (Game.Runtime 0x02000951, x86-64 0x683D30..0x683DE0) and UIContainerActTransitionIntro
    // (0x02000877, 0x619570..0x6197C0) as real components on inactive owned hosts, over a real App and, for the intro, a real
    // UIManager with a scripted container manager.
    // Not covered: the zone icon being published. It needs an AssetReference whose operation handle is valid (0x6196ED), which only an
    // Addressables load of the icon gives; the negative branch (a valid key with no load) is covered.
    public sealed class UIContainerCloseEventTests
    {
        // dump.cs TypeDefIndex 2384 / 2166 and the DummyDll rows (fields 0x04002671, 0x040020e1..0x040020e3).
        [Test]
        public void DeclarationsMatchTheOriginalMetadata()
        {
            AssertMembers(typeof(UIContainerWithCloseEvent), 0x00100001,
                new (string, int)[] { ("m_closeEvent", 0x0001) },
                new (string, int)[] { ("SetCloseEvent", 0x0086), ("Action_Close", 0x01c6), (".ctor", 0x1886) });
            AssertIl2CppOptions(typeof(UIContainerWithCloseEvent), 2, 1);
            Assert.That(typeof(UIContainerWithCloseEvent).BaseType, Is.EqualTo(typeof(UIContainer)));
            Assert.That(AttributeTypes(typeof(UIContainerWithCloseEvent).GetField("m_closeEvent", Declared)), Is.Empty);

            AssertMembers(typeof(UIContainerActTransitionIntro), 0x00100101,
                new (string, int)[] { ("ZoneIconAsset", 0x0026), ("LevelTitle", 0x0026), ("LevelSubtitle", 0x0026) },
                new (string, int)[] { ("Start", 0x0081), ("OnLevelManager", 0x0081), ("Action_Close", 0x00c6), (".ctor", 0x1886) });
            AssertIl2CppOptions(typeof(UIContainerActTransitionIntro), 2, 1);
            Assert.That(typeof(UIContainerActTransitionIntro).BaseType, Is.EqualTo(typeof(UIContainerWithCloseEvent)));
        }

        // 0x683D50 publishes the event (App.AddUniqueUIModernEvent, 0x530D60) when one is set; a second close adds nothing.
        [Test]
        public void CloseEventIsPublishedOnceHoweverOftenTheContainerCloses()
        {
            Owned.Run(owned =>
            {
                App app = NewApp(owned);
                UIModernEvent closeEvent = NewEvent(owned, "closed");
                var container = owned.Host("Lucid close event container", false).AddComponent<UIContainerWithCloseEvent>();
                container.SetCloseEvent(closeEvent);

                container.Action_Close();
                container.Action_Close();

                Assert.That(app.GetUIModernEvents(), Is.EqualTo(new[] { closeEvent }));
            });
        }

        // 0x683D5F..0x683D98: the Unity null test of the stored event (ScriptableObjectWithGuid.op_Equality, 0x683D89) returns before
        // the App is fetched, so an unset or destroyed event needs no App at all (an absent App row would fault on the lookup).
        [Test]
        public void AnUnsetOrDestroyedCloseEventPublishesNothingAndNeverAsksForTheApp()
        {
            Owned.Run(owned =>
            {
                var container = owned.Host("Lucid close event container", false).AddComponent<UIContainerWithCloseEvent>();
                Assert.DoesNotThrow(container.Action_Close, "no event was ever set");

                UIModernEvent closeEvent = ScriptableObject.CreateInstance<UIModernEvent>();
                container.SetCloseEvent(closeEvent);
                UnityEngine.Object.DestroyImmediate(closeEvent);
                Assert.DoesNotThrow(container.Action_Close, "a destroyed event counts as unset");

                container.SetCloseEvent(null);
                Assert.DoesNotThrow(container.Action_Close, "the event was cleared");
            });
        }

        // 0x683D30 stores the argument (with a GC write barrier); the last event set is the one published.
        [Test]
        public void SetCloseEventReplacesThePreviousEvent()
        {
            Owned.Run(owned =>
            {
                App app = NewApp(owned);
                UIModernEvent first = NewEvent(owned, "first"), second = NewEvent(owned, "second");
                var container = owned.Host("Lucid close event container", false).AddComponent<UIContainerWithCloseEvent>();
                container.SetCloseEvent(first);
                container.SetCloseEvent(second);

                container.Action_Close();

                Assert.That(app.GetUIModernEvents(), Is.EqualTo(new[] { second }));
            });
        }

        // The constructor 0x6197C0 allocates the three bindables in field order before the base constructor; they start at
        // their defaults and are independent objects.
        [Test]
        public void TheIntroStartsWithThreeIndependentEmptyBindables()
        {
            Owned.Run(owned =>
            {
                var intro = owned.Host("Lucid act intro", false).AddComponent<UIContainerActTransitionIntro>();

                Assert.That(intro.ZoneIconAsset, Is.Not.Null.And.Not.SameAs(intro.LevelTitle));
                Assert.That(intro.LevelTitle, Is.Not.Null.And.Not.SameAs(intro.LevelSubtitle));
                Assert.That(intro.ZoneIconAsset.Value, Is.Null);
                Assert.That(intro.LevelTitle.Value, Is.EqualTo(default(Strings)));
                Assert.That(intro.LevelSubtitle.Value, Is.EqualTo(default(Strings)));
            });
        }

        // 0x619740: the base close runs first (publishing the close event), then the manager is asked to close this container's
        // own identifier. The scripted manager records how many events were already published when the close arrived.
        [Test]
        public void IntroCloseFirstPublishesTheCloseEventThenClosesItsOwnContainer()
        {
            Owned.Run(owned =>
            {
                App app = NewApp(owned);
                NewUiManager(owned, out ScriptedExclusivityContainerManager containers);
                UIContainerIdentifier identifier = NewIdentifier(owned, "act-intro");
                var intro = NewContainer<UIContainerActTransitionIntro>(owned, identifier);
                UIModernEvent closeEvent = NewEvent(owned, "intro-closed");
                intro.SetCloseEvent(closeEvent);
                int publishedAtClose = -1;
                containers.OnClose = () => publishedAtClose = app.GetUIModernEvents().Count;

                intro.Action_Close();

                Assert.That(containers.Log, Is.EqualTo(new[] { "close:act-intro" }));
                Assert.That(publishedAtClose, Is.EqualTo(1), "the event was published before the container was closed");
                Assert.That(app.GetUIModernEvents(), Is.EqualTo(new[] { closeEvent }));
            });
        }

        // Without a close event the base publishes nothing, yet the container is still closed (and no App is needed).
        [Test]
        public void IntroCloseWithoutACloseEventStillClosesItsOwnContainer()
        {
            Owned.Run(owned =>
            {
                NewUiManager(owned, out ScriptedExclusivityContainerManager containers);
                UIContainerIdentifier identifier = NewIdentifier(owned, "act-intro");
                var intro = NewContainer<UIContainerActTransitionIntro>(owned, identifier);

                intro.Action_Close();

                Assert.That(containers.Log, Is.EqualTo(new[] { "close:act-intro" }));
            });
        }

        // OnLevelManager 0x619650 asks for the current level first (LevelManager.TryGetCurrentLevel, 0x592640) and returns when
        // there is none, before any bindable is written.
        [Test]
        public void IntroLeavesItsBindablesAloneWhenTheLevelManagerHasNoCurrentLevel()
        {
            Owned.Run(owned =>
            {
                LeaseLevelManagerRows(owned);
                var intro = owned.Host("Lucid act intro", false).AddComponent<UIContainerActTransitionIntro>();
                var levelManager = owned.Host("Lucid level manager", false).AddComponent<LevelManager>();
                intro.LevelTitle.Value = Strings.DECIMAL_SEPARATOR;
                intro.LevelSubtitle.Value = Strings.CULTURE_NUMBER_BILLIONS;

                Call(intro, "OnLevelManager", levelManager);

                Assert.That(levelManager.CurrentLevelValid, Is.False);
                Assert.That(intro.LevelTitle.Value, Is.EqualTo(Strings.DECIMAL_SEPARATOR));
                Assert.That(intro.LevelSubtitle.Value, Is.EqualTo(Strings.CULTURE_NUMBER_BILLIONS));
                Assert.That(intro.ZoneIconAsset.Value, Is.Null);
            });
        }

        // OnLevelManager 0x619650 with a current level: the title is the level's DisplayName (+0x20) and the subtitle its ActName (+0x24),
        // and the zone icon is published only when IsValid() (0x6196ED) holds as well as RuntimeKeyIsValid (0x619708). The icon here has a
        // valid GUID key but was never loaded, so IsValid() is false and the icon stays unset. The current level and the level
        // configuration have no path in Edit Mode (they come from a loaded scene and Awake), so they are written directly.
        [Test]
        public void IntroPublishesTheLevelNamesButNotAZoneIconThatWasNeverLoaded()
        {
            Owned.Run(owned =>
            {
                LeaseLevelManagerRows(owned);
                owned.LeaseRow<LevelManager>(); // GameplayLevelCategory's field initialiser caches its reference
                var intro = owned.Host("Lucid act intro", false).AddComponent<UIContainerActTransitionIntro>();
                var levelManager = owned.Host("Lucid level manager", false).AddComponent<LevelManager>();
                var definition = owned.Track(ScriptableObject.CreateInstance<GameplayLevelDefinition>());
                Set(definition, "m_displayName", Strings.CULTURE_NUMBER_MILLIONS);
                Set(definition, "m_actName", Strings.CULTURE_NUMBER_THOUSANDS);
                var zone = owned.Track(ScriptableObject.CreateInstance<GameplayLevelCategory>());
                Set(zone, "m_gameplayEntries", new List<GameplayLevelUIEntry> { definition });
                Set(zone, "m_iconAsset", new AssetReferenceAtlasedSprite(Guid.NewGuid().ToString("N")));
                var levels = owned.Track(ScriptableObject.CreateInstance<GameLevels>());
                Set(levels, "m_gameplayEntries", new List<GameplayLevelUIEntry> { zone });
                typeof(LevelManager).GetProperty(nameof(LevelManager.GameLevels)).GetSetMethod(true).Invoke(levelManager, new object[] { levels });
                typeof(LevelManager).GetProperty(nameof(LevelManager.CurrentLevelValid)).GetSetMethod(true).Invoke(levelManager, new object[] { true });
                // Reflection: only a level load sets the current level, and Edit Mode cannot load one.
                Set(levelManager, "m_currentLevel", new LevelManagerLevel(definition, definition));

                Call(intro, "OnLevelManager", levelManager);

                Assert.That(intro.LevelTitle.Value, Is.EqualTo(Strings.CULTURE_NUMBER_MILLIONS));
                Assert.That(intro.LevelSubtitle.Value, Is.EqualTo(Strings.CULTURE_NUMBER_THOUSANDS));
                Assert.That(zone.IconAsset.RuntimeKeyIsValid(), Is.True);
                Assert.That(zone.IconAsset.IsValid(), Is.False);
                Assert.That(intro.ZoneIconAsset.Value, Is.Null);
            });
        }

        // Start 0x619570 subscribes OnLevelManager to the LevelManager reference (InvokeOnValid): with the row empty the callback is
        // queued on the reference, and registering a manager runs it and clears the queue.
        [Test]
        public void IntroStartQueuesItsCallbackUntilTheLevelManagerIsRegistered()
        {
            Owned.Run(owned =>
            {
                LeaseLevelManagerRows(owned);
                var row = owned.Lease(new VerificationFixtureOwnership.RegistryRow(ProcessManager.GetDefaultName<LevelManager>()));
                row.Register(null);
                var intro = owned.Host("Lucid act intro", false).AddComponent<UIContainerActTransitionIntro>();
                var levelManager = owned.Host("Lucid level manager", false).AddComponent<LevelManager>();

                Call(intro, "Start");
                SystemRef<LevelManager> reference = ProcessManager.GetSystemRef<LevelManager>();
                Assert.That(Get<object>(reference, "m_actionOnSystemValid"), Is.Not.Null, "the callback is queued on the empty row");

                row.Register(levelManager);
                Assert.That(Get<object>(reference, "m_actionOnSystemValid"), Is.Null, "registering the manager ran the queue");
            });
        }

        // LevelManager's field initialisers cache the references of these rows.
        private static void LeaseLevelManagerRows(Owned owned)
        {
            owned.LeaseRow<MissionManager>();
            owned.LeaseRow<AudioManager>();
            owned.LeaseRow<SaveManager>();
        }
    }
}
