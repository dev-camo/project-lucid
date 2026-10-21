using System;
using System.Linq;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // UIContainerLoadingSimple (Game.Runtime 0x020008d6, x86-64 0x63B450..0x63B670) as a real component on an inactive owned host
    // over a real UIWidgetSDTLoadingBar whose fill is an owned RectTransform and whose curve is linear, and the string
    // MessageExchange row registered by the fixture (Awake takes it with GetSystemAutoCreate). Awake is called directly; the serialized
    // bar and started event are authored data.
    public sealed class UIContainerLoadingSimpleTests
    {
        private const float Tolerance = 1e-5f;

        private sealed class Rig
        {
            public UIContainerLoadingSimple Container;
            public MessageExchange<string> Exchange;
            public RectTransform Fill;
            public int Started;
        }

        private static void Run(Action<Rig> body) => Owned.Run(owned =>
        {
            var rig = new Rig { Exchange = owned.Register(new MessageExchange<string>()) };
            rig.Container = owned.Host("Lucid loading simple", false).AddComponent<UIContainerLoadingSimple>();
            var bar = owned.Host("Lucid loading bar", false).AddComponent<UIWidgetSDTLoadingBar>();
            rig.Fill = owned.Track(new GameObject("Lucid loading bar fill", typeof(RectTransform))).GetComponent<RectTransform>();
            rig.Fill.anchorMax = new Vector2(0.3f, 0.4f);
            Set(bar, "m_loadingBarUI", rig.Fill);
            Set(bar, "m_curve", AnimationCurve.Linear(0f, 0f, 1f, 1f));
            var started = new UnityEvent();
            started.AddListener(() => rig.Started++);
            Set(rig.Container, "m_loadingBar", bar);
            Set(rig.Container, "m_onLoadingBarStarted", started);
            body(rig);
        });

        // dump.cs TypeDefIndex 2261 and the DummyDll rows (fields 0x04002353..0x04002357, methods 0x060032fc..0x060032ff).
        [Test]
        public void DeclarationsMatchTheOriginalMetadata()
        {
            AssertMembers(typeof(UIContainerLoadingSimple), 0x00100001,
                new (string, int)[]
                {
                    ("m_loadingBar", 0x0001), ("m_onLoadingBarStarted", 0x0001), ("m_loadingEventName", 0x0021),
                    ("m_loadingProgress", 0x0001), ("m_loadingBarEventHasFired", 0x0001),
                },
                new (string, int)[] { ("Awake", 0x0086), ("OnDestroy", 0x0081), ("UpdateLoadingProgress", 0x0086), (".ctor", 0x1886) });
            AssertIl2CppOptions(typeof(UIContainerLoadingSimple), 1, 2);
            Assert.That(typeof(UIContainerLoadingSimple).BaseType, Is.EqualTo(typeof(UIContainer)));
            foreach (string field in new[] { "m_loadingBar", "m_onLoadingBarStarted" })
                Assert.That(AttributeTypes(typeof(UIContainerLoadingSimple).GetField(field, Declared)), Is.EqualTo(new[] { typeof(SerializeField) }), field);
            var progress = typeof(UIContainerLoadingSimple).GetMethod("UpdateLoadingProgress").GetParameters().Single();
            Assert.That(progress.IsIn, Is.True, "the progress is an in parameter (flags 0x0001, IsReadOnly)");
        }

        // The constructor 0x63B620 stores the string literal "LoadingProgress" (0x63B645) in the readonly field.
        [Test]
        public void ConstructionNamesTheLoadingProgressMessage()
        {
            Run(rig => Assert.That(Get<string>(rig.Container, "m_loadingEventName"), Is.EqualTo("LoadingProgress")));
        }

        // Awake 0x63B450 takes the string exchange and subscribes UpdateLoadingProgress to that message with a float argument; the
        // bar's right anchor then follows the progress along its curve (linear here), the vertical anchor being 1.
        [Test]
        public void ProgressMessagesMoveTheBar()
        {
            Run(rig =>
            {
                rig.Container.Awake();
                Assert.That(Get<MessageExchange<string>>(rig.Container, "m_loadingProgress"), Is.SameAs(rig.Exchange));

                rig.Exchange.PublishMessage("LoadingProgress", 0.25f);
                Assert.That(rig.Fill.anchorMax.x, Is.EqualTo(0.25f).Within(Tolerance));
                Assert.That(rig.Fill.anchorMax.y, Is.EqualTo(1f));

                rig.Exchange.PublishMessage("LoadingProgress", 0.75f);
                Assert.That(rig.Fill.anchorMax.x, Is.EqualTo(0.75f).Within(Tolerance));
            });
        }

        [Test]
        public void AnotherMessageIsNotDelivered()
        {
            Run(rig =>
            {
                rig.Container.Awake();

                rig.Exchange.PublishMessage("Another message", 0.9f);

                Assert.That(rig.Fill.anchorMax, Is.EqualTo(new Vector2(0.3f, 0.4f)));
                Assert.That(rig.Started, Is.Zero);
            });
        }

        // UpdateLoadingProgress 0x63B5E0 raises the started event only while the fired flag (+0x58) is clear, then sets it (0x63B603).
        [Test]
        public void TheStartedEventFiresOnlyWithTheFirstProgress()
        {
            Run(rig =>
            {
                rig.Container.Awake();

                rig.Exchange.PublishMessage("LoadingProgress", 0f);
                rig.Exchange.PublishMessage("LoadingProgress", 0.5f);
                rig.Exchange.PublishMessage("LoadingProgress", 1f);

                Assert.That(rig.Started, Is.EqualTo(1));
            });
        }

        // The event is tested for null (0x63B5F7) but the flag is set whether or not it existed (0x63B603).
        [Test]
        public void ARunWithoutAStartedEventStillCountsAsStarted()
        {
            Run(rig =>
            {
                Set(rig.Container, "m_onLoadingBarStarted", null); // Reflection: serialized event left unassigned
                float progress = 0.5f;

                Assert.DoesNotThrow(() => rig.Container.UpdateLoadingProgress(in progress));
                Assert.That(Get<bool>(rig.Container, "m_loadingBarEventHasFired"), Is.True);
                Assert.That(rig.Fill.anchorMax.x, Is.EqualTo(0.5f).Within(Tolerance));
            });
        }

        // OnDestroy 0x63B540 unsubscribes the same callback from the same message.
        [Test]
        public void DestroyingTheContainerStopsTheProgressUpdates()
        {
            Run(rig =>
            {
                rig.Container.Awake();
                Call(rig.Container, "OnDestroy");

                rig.Exchange.PublishMessage("LoadingProgress", 0.5f);

                Assert.That(rig.Fill.anchorMax, Is.EqualTo(new Vector2(0.3f, 0.4f)));
                Assert.That(rig.Started, Is.Zero);
            });
        }
    }
}
