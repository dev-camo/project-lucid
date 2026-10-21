using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // UIContainerControllerRequired (Game.Runtime 0200089e, x86-64 0x629020..0x6294B0) as a real component on an owned
    // inactive host. Not covered: Start and OnDestroy, which cast EventSystem.current's input module to an
    // HLInputModule and subscribe to its controller provider, and the visible branch of OnControllerConnectionUpdate,
    // which asks a started App about its controllers.
    public sealed class UIContainerControllerRequiredTests
    {
        // dump.cs TypeDefIndex 2205 and the DummyDll rows for the three fields and four methods.
        [Test]
        public void DeclarationsMatchTheOriginalMetadata()
        {
            AssertMembers(typeof(UIContainerControllerRequired), 0x00100101,
                new (string, int)[]
                {
                    ("m_continueButton", 0x0001),
                    ("m_appRef", 0x0021),
                    ("m_hlInputModule", 0x0001),
                },
                new (string, int)[]
                {
                    ("Start", 0x0081),
                    ("OnDestroy", 0x0081),
                    ("OnControllerConnectionUpdate", 0x0081),
                    (".ctor", 0x1886),
                });
            AssertIl2CppOptions(typeof(UIContainerControllerRequired), 2, 1);
            Assert.That(typeof(UIContainerControllerRequired).BaseType, Is.EqualTo(typeof(UIContainer)));
        }

        // OnControllerConnectionUpdate 0x629280 returns before touching the continue button while the App system is
        // missing (SystemRef.IsNull at 0x6292c0), so a button that is shown stays shown. The constructor 0x6294b0 takes
        // the App reference from the registry.
        [Test]
        public void WithoutAnAppTheContinueButtonIsLeftAlone()
        {
            Owned.Run(owned =>
            {
                owned.LeaseRow<App>();
                var container = owned.Host("Lucid controller required", false).AddComponent<UIContainerControllerRequired>();
                RectTransform button = owned.Host("Lucid continue button", true).AddComponent<RectTransform>();
                Set(container, "m_continueButton", button); // serialized authored reference
                Assert.That(Get<object>(container, "m_appRef"), Is.Not.Null);

                Call(container, "OnControllerConnectionUpdate");
                Assert.That(button.gameObject.activeSelf, Is.True);

                button.gameObject.SetActive(false);
                Call(container, "OnControllerConnectionUpdate");
                Assert.That(button.gameObject.activeSelf, Is.False, "it is not switched on either");
            });
        }
    }
}
