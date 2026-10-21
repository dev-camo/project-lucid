using System;
using System.Collections;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // UIContainerGameCenterAccessPoint (Game.Runtime 020008bb, x86-64 0x6317F0..0x6323C0, iterators 020008bc and
    // 020008bd) as a real component on an owned inactive host over a real GameCenterAccessPointManager. No
    // GameCenter system is registered, so the manager reports the access point unavailable without calling the
    // platform plugin: every path below that would show, hide or measure the access point stops there. Not covered:
    // Update's positioning of the content (it needs an available access point), Action_ShowGameCenter (the App and
    // the plugin), and the later steps of the glyph fade, which follow the frame time.
    public sealed class UIContainerGameCenterAccessPointTests
    {
        private sealed class Rig
        {
            public UIContainerGameCenterAccessPoint Container;
            public GameCenterAccessPointManager Manager;
            public RectTransform Content;
            public CanvasGroup Group;
            public CanvasScaler Scaler;
        }

        // The manager's constructor subscribes Update and Shutdown and its field initialiser takes the GameCenter
        // reference; the container's field initialisers take the App reference. A case without a manager leases the
        // manager row instead of registering one.
        private static void WithContainer(bool registerManager, Action<Owned, Rig> body) => Owned.Run(owned =>
        {
            owned.LeaseRow<GameCenter>();
            owned.LeaseRow<App>();
            var rig = new Rig();
            if (registerManager)
            {
                owned.LeaseActions(SystemAction.Update, SystemAction.Shutdown);
                rig.Manager = owned.Register(new GameCenterAccessPointManager());
            }
            else
            {
                owned.LeaseRow<GameCenterAccessPointManager>();
            }
            GameObject host = owned.Host("Lucid game center access point", false);
            rig.Container = host.AddComponent<UIContainerGameCenterAccessPoint>();
            rig.Content = owned.Host("Lucid game center content", false).AddComponent<RectTransform>();
            rig.Group = owned.Host("Lucid game center glyph", false).AddComponent<CanvasGroup>();
            rig.Scaler = owned.Host("Lucid game center scaler", false).AddComponent<CanvasScaler>();
            // Serialized authored references.
            Set(rig.Container, "m_groupContent", rig.Content);
            Set(rig.Container, "m_canvasGroup", rig.Group);
            Set(rig.Container, "m_canvasScaler", rig.Scaler);
            body(owned, rig);
        });

        // dump.cs TypeDefIndex 2236/2234/2235 and the DummyDll rows (fields 0x040022b0..0x040022c2).
        [Test]
        public void DeclarationsMatchTheOriginalMetadata()
        {
            AssertMembers(typeof(UIContainerGameCenterAccessPoint), 0x00100101,
                new (string, int)[]
                {
                    ("m_location", 0x0001),
                    ("m_showHighlights", 0x0001),
                    ("m_groupContent", 0x0001),
                    ("m_canvasGroup", 0x0001),
                    ("m_canvasScaler", 0x0001),
                    ("Visible", 0x0026),
                    ("Available", 0x0026),
                    ("ShouldDisplayGlyph", 0x0026),
                    ("DelayAppearanceForSeconds", 0x8051),
                    ("GlyphFadeInTime", 0x8051),
                    ("m_appRef", 0x0021),
                    ("m_gameCenterAccessPointRef", 0x0021),
                    ("m_gameCenterRef", 0x0021),
                    ("m_delayedDisplayTriggered", 0x0001),
                    ("m_delayedDisplayRoutine", 0x0001),
                    ("m_delayCallToGCAP", 0x0021),
                    ("m_fadeInGlyph", 0x0021),
                    ("m_cachedVisible", 0x0001),
                    ("m_cachedAvailable", 0x0001),
                },
                new (string, int)[]
                {
                    ("Start", 0x0081),
                    ("Update", 0x0081),
                    ("DelayAppearance", 0x0081),
                    ("ShowGlyph", 0x0081),
                    ("OnDestroy", 0x0081),
                    ("Action_ShowGameCenter", 0x0086),
                    ("ValidateAvailability", 0x0081),
                    (".ctor", 0x1886),
                });
            AssertIl2CppOptions(typeof(UIContainerGameCenterAccessPoint), 2, 1);
            Assert.That(typeof(UIContainerGameCenterAccessPoint).BaseType, Is.EqualTo(typeof(UIContainer)));
            Assert.That(Field(typeof(UIContainerGameCenterAccessPoint), "DelayAppearanceForSeconds").GetRawConstantValue(), Is.EqualTo(0.5f));
            Assert.That(Field(typeof(UIContainerGameCenterAccessPoint), "GlyphFadeInTime").GetRawConstantValue(), Is.EqualTo(0.25f));
        }

        // The constructor 0x631e90 turns highlights on (+0x3c), creates the three bindables, takes the three
        // references, and builds the half second wait (0x632037: 0x3f000000) and the end-of-frame wait.
        [Test]
        public void ConstructionStartsWithHighlightsAndTheTwoWaits()
        {
            WithContainer(true, (owned, rig) =>
            {
                Assert.That(Get<bool>(rig.Container, "m_showHighlights"), Is.True);
                Assert.That(rig.Container.Visible.Value, Is.False);
                Assert.That(rig.Container.Available.Value, Is.False);
                Assert.That(rig.Container.ShouldDisplayGlyph.Value, Is.False);
                Assert.That(Get<WaitForSeconds>(rig.Container, "m_delayCallToGCAP"), Is.Not.Null);
                Assert.That(Get<WaitForEndOfFrame>(rig.Container, "m_fadeInGlyph"), Is.Not.Null);
                Assert.That(Get<bool>(rig.Container, "m_delayedDisplayTriggered"), Is.False);
            });
        }

        // Start 0x6317f0 hides the container and activates the manager (byte +0x10 = 1), enables the canvas scaler
        // only on the macOS player (platform 1, 0x63187e) and displays the glyph only on iOS (8) and tvOS (0x1f). The
        // editor is none of those, so the scaler ends disabled whatever it was and the glyph stays off.
        [Test]
        public void StartActivatesTheManagerAndLeavesThePlatformExtrasOffInTheEditor()
        {
            WithContainer(true, (owned, rig) =>
            {
                rig.Container.Visible.Value = true;
                rig.Scaler.enabled = true;
                Assume.That(Application.platform, Is.Not.EqualTo(RuntimePlatform.OSXPlayer));

                Call(rig.Container, "Start");

                Assert.That(rig.Container.Visible.Value, Is.False);
                Assert.That(rig.Manager.IsActive, Is.True);
                Assert.That(rig.Scaler.enabled, Is.False);
                Assert.That(rig.Container.ShouldDisplayGlyph.Value, Is.False);
            });
        }

        // OnDestroy 0x631d70 stops the delayed routine, hides the access point (unavailable here, so no plugin call)
        // and clears the manager's active flag (byte +0x10 = 0).
        [Test]
        public void OnDestroyDeactivatesTheManager()
        {
            WithContainer(true, (owned, rig) =>
            {
                rig.Manager.IsActive = true;

                Call(rig.Container, "OnDestroy");

                Assert.That(rig.Manager.IsActive, Is.False);
            });
        }

        // Both Start and OnDestroy need the manager; OnDestroy tests it first (TryGet 0x631dc0) so its absence is quiet,
        // as is Update's IsNull test (0x631938) and Action_ShowGameCenter's (0x631e3b).
        [Test]
        public void WithoutAManagerOnDestroyUpdateAndShowGameCenterDoNothing()
        {
            WithContainer(false, (owned, rig) =>
            {
                rig.Container.Available.Value = false;

                Assert.DoesNotThrow(() => Call(rig.Container, "OnDestroy"));
                Assert.DoesNotThrow(() => Call(rig.Container, "Update"));
                Assert.DoesNotThrow(() => rig.Container.Action_ShowGameCenter());
                Assert.That(rig.Container.Available.Value, Is.False);
                Assert.That(Get<bool>(rig.Container, "m_delayedDisplayTriggered"), Is.False);
            });
        }

        // Update 0x6318f0 validates the availability first and stops while it is false: nothing is shown, the delayed
        // display is not started and the content keeps its position.
        [Test]
        public void UpdateDoesNothingWhileTheAccessPointIsUnavailable()
        {
            WithContainer(true, (owned, rig) =>
            {
                rig.Content.anchoredPosition = new Vector2(5f, 6f);

                Call(rig.Container, "Update");

                Assert.That(rig.Container.Available.Value, Is.False);
                Assert.That(Get<bool>(rig.Container, "m_delayedDisplayTriggered"), Is.False);
                Assert.That(Get<Coroutine>(rig.Container, "m_delayedDisplayRoutine"), Is.Null);
                Assert.That(rig.Content.anchoredPosition, Is.EqualTo(new Vector2(5f, 6f)));
            });
        }

        // ValidateAvailability 0x631b50: without a logged in GameCenter (no system here) a cached visible state is
        // cleared (0x631bcc..0x631be2); the availability is copied only when it differs from the cache and the bindable
        // is still false (0x631c0e). Nothing here makes the access point available.
        [Test]
        public void AnUnavailableAccessPointClearsTheCachedVisibleState()
        {
            WithContainer(true, (owned, rig) =>
            {
                Set(rig.Container, "m_cachedVisible", true);
                rig.Container.Visible.Value = true;

                Call(rig.Container, "ValidateAvailability", rig.Manager);

                Assert.That(rig.Container.Visible.Value, Is.False);
                Assert.That(Get<bool>(rig.Container, "m_cachedVisible"), Is.False);
                Assert.That(rig.Container.Available.Value, Is.False);
                Assert.That(Get<bool>(rig.Container, "m_cachedAvailable"), Is.False);
            });
        }

        [Test]
        public void ACachedAvailableStateIsKeptOnceAvailableIsSet()
        {
            WithContainer(true, (owned, rig) =>
            {
                rig.Container.Available.Value = true;
                Set(rig.Container, "m_cachedAvailable", true);

                Call(rig.Container, "ValidateAvailability", rig.Manager);

                Assert.That(rig.Container.Available.Value, Is.True, "never cleared once set");
                Assert.That(Get<bool>(rig.Container, "m_cachedAvailable"), Is.True);
            });
        }

        // DelayAppearance (MoveNext 0x6320c0): wait the stored half second, show the access point (a no-op while
        // unavailable), mark the container visible (+0x58 true, 0x63216b) with the cached flag cleared (0x632171),
        // then hand the glyph fade to the caller as the next yielded enumerator; a final step ends the routine.
        [Test]
        public void TheDelayedAppearanceWaitsThenShowsAndYieldsTheGlyphFade()
        {
            WithContainer(true, (owned, rig) =>
            {
                Set(rig.Container, "m_cachedVisible", true);
                var routine = (IEnumerator)Call(rig.Container, "DelayAppearance", rig.Manager);

                Assert.That(routine.MoveNext(), Is.True);
                Assert.That(routine.Current, Is.SameAs(Get<WaitForSeconds>(rig.Container, "m_delayCallToGCAP")));
                Assert.That(rig.Container.Visible.Value, Is.False, "nothing happens before the wait");

                Assert.That(routine.MoveNext(), Is.True);
                Assert.That(rig.Container.Visible.Value, Is.True);
                Assert.That(Get<bool>(rig.Container, "m_cachedVisible"), Is.False);
                Assert.That(routine.Current, Is.InstanceOf<IEnumerator>());

                Assert.That(routine.MoveNext(), Is.False);
            });
        }

        // ShowGlyph (MoveNext 0x632270): the first step writes the group's alpha from a zero timer, Lerp(0, 1, 0 / 0.25),
        // then waits for the end of the frame (the stored WaitForEndOfFrame).
        [Test]
        public void TheGlyphFadeStartsFromZeroAlphaAndWaitsForTheEndOfTheFrame()
        {
            WithContainer(true, (owned, rig) =>
            {
                rig.Group.alpha = 0.7f;
                var routine = (IEnumerator)Call(rig.Container, "ShowGlyph");

                Assert.That(routine.MoveNext(), Is.True);

                Assert.That(rig.Group.alpha, Is.Zero);
                Assert.That(routine.Current, Is.SameAs(Get<WaitForEndOfFrame>(rig.Container, "m_fadeInGlyph")));
            });
        }
    }
}
