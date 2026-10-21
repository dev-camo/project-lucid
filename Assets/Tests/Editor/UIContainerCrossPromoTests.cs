using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.CrossPromoGameScene;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // UIContainerCrossPromo (x86-64 0x62A1C0..0x62A7E0, closure <>c__DisplayClass5_0 0x62A380 and 0x62A7E0) over
    // a real CrossPromo and a real UIManager whose container manager records opens and closes, with a complete
    // full screen view. The container is added to an inactive host, so no Unity message runs.
    public sealed class UIContainerCrossPromoTests
    {
        // dump.cs TypeDefIndex 2211 and the DummyDll rows (fields 0x04002242..0x04002246).
        [Test]
        public void DeclarationsMatchTheOriginalMetadata()
        {
            AssertMembers(typeof(UIContainerCrossPromo), 0x00100001,
                new (string, int)[]
                {
                    ("m_fullScreenView", 0x0001),
                    ("m_hideContainersWhilstOpen", 0x0001),
                    ("m_crossPromoRef", 0x0021),
                    ("m_uiManagerRef", 0x0021),
                    ("m_hiddenContainers", 0x0021),
                },
                new (string, int)[]
                {
                    ("Setup", 0x00c6),
                    ("Show", 0x0081),
                    ("Action_Close", 0x0086),
                    (".ctor", 0x1886),
                });
            AssertIl2CppOptions(typeof(UIContainerCrossPromo), 2, 1);
            Assert.That(typeof(UIContainerCrossPromo).BaseType, Is.EqualTo(typeof(UIContainer)));
            const BindingFlags all = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            FieldInfo view = typeof(UIContainerCrossPromo).GetField("m_fullScreenView", all);
            FieldInfo hidden = typeof(UIContainerCrossPromo).GetField("m_hideContainersWhilstOpen", all);
            Assert.That(AttributeTypes(view), Is.EqualTo(new[] { typeof(SerializeField), typeof(TooltipAttribute) }));
            Assert.That(Tooltip(view), Is.EqualTo("The Full Screen View visual component."));
            Assert.That(AttributeTypes(hidden), Is.EqualTo(new[] { typeof(TooltipAttribute), typeof(SerializeField) }));
            Assert.That(Tooltip(hidden), Is.EqualTo("Specify containers to hide whilst this container is open."));
            Type closure = typeof(UIContainerCrossPromo).GetNestedTypes(BindingFlags.NonPublic).Single(type => type.Name.StartsWith("<>c__DisplayClass"));
            Assert.That((int)closure.Attributes, Is.EqualTo(0x00100103), "NestedPrivate|Sealed|BeforeFieldInit");
            Assert.That(closure.GetFields().Select(field => field.FieldType), Is.EqualTo(new[] { typeof(UIContainerCrossPromo), typeof(UIContainerCrossPromoParameters) }));
        }

        // The constructor 0x62A620 sets the shared empty array at +0x40, takes the CrossPromo (+0x48) and UIManager
        // (+0x50) references from the registry and allocates the hidden list (+0x58), then runs UIContainer's.
        [Test]
        public void ConstructorTakesTheRegistryReferencesAndAnEmptyHiddenList()
        {
            Owned.Run(owned =>
            {
                var scene = new CrossPromoGameScene(owned, true, "a");
                var container = owned.Host("Lucid container", false).AddComponent<UIContainerCrossPromo>();
                Assert.That(Get<UIContainerIdentifier[]>(container, "m_hideContainersWhilstOpen"), Is.SameAs(Array.Empty<UIContainerIdentifier>()));
                Assert.That(Get<List<UIContainerIdentifier>>(container, "m_hiddenContainers"), Is.Empty);
                Assert.That(Get<SystemRef<CrossPromo>>(container, "m_crossPromoRef").Get(), Is.SameAs(scene.Promo));
                Assert.That(Get<SystemRef<UIManager>>(container, "m_uiManagerRef").Get(), Is.SameAs(scene.Manager));
            });
        }

        // Setup 0x62A1C0: the base setup, then for each authored identifier in order UIManager.IsOpen (0x62A2c9) and,
        // when open, Close (0x62A2da) and AddUnique to the hidden list (0x62A2f0); a container that stays open when
        // closed is closed again by a repeated identifier but listed once. Then GetAs and InvokeOnValid on the
        // CrossPromo reference (0x62A305, 0x62A36c): the system is valid, so the closure runs at once, subscribes
        // Show to FullScreenViewRequested and requests the parameters' image index, which reaches Show and so the
        // full screen view.
        [Test]
        public void SetupClosesTheOpenContainersItHidesAndShowsTheRequestedAd()
        {
            Owned.Run(owned =>
            {
                var scene = new CrossPromoGameScene(owned, true, "a", "b", "c");
                UIContainerIdentifier first = scene.NewHideable("first");
                UIContainerIdentifier closed = scene.NewIdentifier("not-open");
                UIContainerIdentifier second = scene.NewHideable("second");
                scene.Containers.CloseKeepsOpen = true;
                UIContainerCrossPromo container = scene.NewContainer("promo", first, closed, second, first);
                scene.Log.Clear();

                container.Setup(new UIContainerCrossPromoParameters(2));

                Assert.That(scene.Log, Is.EqualTo(new[] { "close:first", "close:second", "close:first" }),
                    "each open container is closed in authored order; a closed one is left alone");
                Assert.That(Names(Get<List<UIContainerIdentifier>>(container, "m_hiddenContainers")), Is.EqualTo(new[] { "first", "second" }),
                    "remembered once each, in order");
                var view = Get<CrossPromoFullScreenView>(container, "m_fullScreenView");
                Assert.That(CurrentAd(view), Is.EqualTo(2), "the image index reaches the view");
                Assert.That(view.gameObject.activeSelf, Is.True);
                Assert.That(Get<object>(view, "m_crossPromo"), Is.SameAs(scene.Promo));
                Assert.That(Get<CrossPromoAd[]>(view, "m_ads"), Is.SameAs(scene.Ads));
                Assert.That(CrossPromoUiAccess.Handlers(scene.Promo, "FullScreenViewRequested").Select(handler => handler.Target),
                    Is.EqualTo(new object[] { container }), "Show is subscribed");
            });
        }

        // The closure 0x62A7E0 adds its handler first (0x62A853) and only then reads the parameters (0x62A858), so a
        // null parameter object faults after the subscription is made.
        [Test]
        public void SetupWithoutParametersSubscribesBeforeItFaults()
        {
            Owned.Run(owned =>
            {
                var scene = new CrossPromoGameScene(owned, true, "a");
                UIContainerCrossPromo container = scene.NewContainer("promo");
                Assert.Throws<NullReferenceException>(() => container.Setup(null));
                Assert.That(CrossPromoUiAccess.Handlers(scene.Promo, "FullScreenViewRequested").Select(handler => handler.Target),
                    Is.EqualTo(new object[] { container }));
            });
        }

        // Show 0x62A390 hands the full screen view the CrossPromo from its reference, the metadata it was given and the
        // ad index; the metadata's ads, not the system's, are what the view shows.
        [Test]
        public void ShowForwardsTheSystemTheMetadataAndTheAdIndexToTheView()
        {
            Owned.Run(owned =>
            {
                var scene = new CrossPromoGameScene(owned, true, "a", "b");
                UIContainerCrossPromo container = scene.NewContainer("promo");
                CrossPromoAd[] other = CrossPromoUiAccess.Ads("x", "y", "z");
                var metaData = new CrossPromoMetaData { IsSystemEnabled = true, Ads = other };
                Call(container, "Show", metaData, 1);
                var view = Get<CrossPromoFullScreenView>(container, "m_fullScreenView");
                Assert.That(Get<object>(view, "m_crossPromo"), Is.SameAs(scene.Promo));
                Assert.That(Get<CrossPromoAd[]>(view, "m_ads"), Is.SameAs(other));
                Assert.That(CurrentAd(view), Is.EqualTo(1));
                Assert.That(view.gameObject.activeSelf, Is.True);
            });
        }

        // Action_Close 0x62A3F0: with the system present it raises FullScreenViewClosed (0x62A4cf), leaves
        // FullScreenViewRequested (0x62A505), reopens every hidden container through UIManager.GetOrCreate in list
        // order (0x62A569), clears the list and closes the container's own identifier (0x62A5ae). A second close has
        // nothing to reopen but still announces and closes.
        [Test]
        public void CloseAnnouncesReopensTheHiddenContainersAndClosesItself()
        {
            Owned.Run(owned =>
            {
                var scene = new CrossPromoGameScene(owned, true, "a", "b");
                UIContainerIdentifier first = scene.NewHideable("first");
                UIContainerIdentifier second = scene.NewHideable("second");
                UIContainerCrossPromo container = scene.NewContainer("promo", first, second);
                container.Setup(new UIContainerCrossPromoParameters(0));
                scene.Promo.FullScreenViewClosed += () => scene.Log.Add("closed-event");
                scene.Manager.OnContainerClosed += identifier => scene.Log.Add("container-closed:" + identifier.Name);
                scene.Log.Clear();

                container.Action_Close();

                Assert.That(scene.Log, Is.EqualTo(new[] { "closed-event", "open:first", "open:second", "close:promo", "container-closed:promo" }));
                Assert.That(Get<List<UIContainerIdentifier>>(container, "m_hiddenContainers"), Is.Empty);
                Assert.That(CrossPromoUiAccess.Handlers(scene.Promo, "FullScreenViewRequested"), Is.Empty, "Show is unsubscribed");

                scene.Log.Clear();
                container.Action_Close();
                Assert.That(scene.Log, Is.EqualTo(new[] { "closed-event", "close:promo", "container-closed:promo" }));
            });
        }

        // Without a CrossPromo (the reference's IsNull, 0x62A4a7) Action_Close returns at once: nothing is announced,
        // reopened or closed, and the hidden containers stay remembered.
        [Test]
        public void CloseDoesNothingWithoutACrossPromoSystem()
        {
            Owned.Run(owned =>
            {
                var scene = new CrossPromoGameScene(owned, true, "a");
                UIContainerIdentifier first = scene.NewHideable("first");
                UIContainerCrossPromo container = scene.NewContainer("promo", first);
                container.Setup(new UIContainerCrossPromoParameters(0));
                scene.Log.Clear();
                // A detached reference over no system stands in for a CrossPromo that is not registered.
                Set(container, "m_crossPromoRef", new SystemRef<CrossPromo>("lucid-absent-crosspromo", null));
                container.Action_Close();
                Assert.That(scene.Log, Is.Empty);
                Assert.That(Names(Get<List<UIContainerIdentifier>>(container, "m_hiddenContainers")), Is.EqualTo(new[] { "first" }));
            });
        }
    }
}
