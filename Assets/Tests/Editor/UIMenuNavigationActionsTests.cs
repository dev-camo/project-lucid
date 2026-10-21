using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.UiPublishingFixtures;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // UIMenuNavigationActions (Game.Runtime 0x02000847, x86-64 0x60B4C0..0x60B650) as a real component on an inactive owned host
    // over a real App. The two events are serialized authored data and are set directly.
    public sealed class UIMenuNavigationActionsTests
    {
        // dump.cs TypeDefIndex 2118 and the DummyDll rows (fields 0x04001fb2, 0x04001fb3).
        [Test]
        public void DeclarationsMatchTheOriginalMetadata()
        {
            AssertMembers(typeof(UIMenuNavigationActions), 0x00100001,
                new (string, int)[] { ("m_continueUIEvent", 0x0001), ("m_backUIEvent", 0x0001) },
                new (string, int)[] { ("Action_Continue", 0x0086), ("Action_GoBack", 0x0086), ("TriggerEvent", 0x0081), (".ctor", 0x1886) });
            AssertIl2CppOptions(typeof(UIMenuNavigationActions), 1, 2);
            Assert.That(typeof(UIMenuNavigationActions).BaseType, Is.EqualTo(typeof(MonoBehaviour)));
            foreach (string field in new[] { "m_continueUIEvent", "m_backUIEvent" })
                Assert.That(AttributeTypes(typeof(UIMenuNavigationActions).GetField(field, Declared)), Is.EqualTo(new[] { typeof(SerializeField) }), field);
        }

        // 0x60B4C0 reads the continue event (+0x20) and 0x60B5C0 the back event (+0x28); each publishes only its own, in call order.
        [Test]
        public void ContinueAndGoBackEachPublishTheirOwnEventInCallOrder()
        {
            Owned.Run(owned =>
            {
                App app = NewApp(owned);
                UIModernEvent proceed = NewEvent(owned, "continue"), back = NewEvent(owned, "back");
                var actions = owned.Host("Lucid menu navigation", false).AddComponent<UIMenuNavigationActions>();
                Set(actions, "m_continueUIEvent", proceed);
                Set(actions, "m_backUIEvent", back);

                actions.Action_GoBack();
                Assert.That(app.GetUIModernEvents(), Is.EqualTo(new[] { back }));
                actions.Action_Continue();
                Assert.That(app.GetUIModernEvents(), Is.EqualTo(new[] { back, proceed }));

                actions.Action_Continue();
                actions.Action_GoBack();
                Assert.That(app.GetUIModernEvents(), Is.EqualTo(new[] { back, proceed }), "AddUnique adds each event once");
            });
        }

        // TriggerEvent 0x60B540 has no null test: an unset event is handed to AddUniqueUIModernEvent as it is, which stores the null.
        [Test]
        public void AnUnsetEventIsPublishedAsNull()
        {
            Owned.Run(owned =>
            {
                App app = NewApp(owned);
                var actions = owned.Host("Lucid menu navigation", false).AddComponent<UIMenuNavigationActions>();

                actions.Action_Continue();

                Assert.That(app.GetUIModernEvents(), Is.EqualTo(new UIModernEvent[] { null }));
            });
        }
    }
}
