using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // UIWidgetSliderPip (Game.Runtime 0x020009ce, x86-64 0x6A30F0..0x6A3160, constructor 0x6A3160) as a real component on an
    // inactive owned host. The parameters struct UIWidgetSliderPipParameters (0x020009cf) is the restored original.
    public sealed class UIWidgetSliderPipTests
    {
        // dump.cs TypeDefIndex 2509 and the DummyDll rows (field 0x0400292e, methods 0x06003876, 0x06003877).
        [Test]
        public void DeclarationsMatchTheOriginalMetadata()
        {
            AssertMembers(typeof(UIWidgetSliderPip), 0x00100001,
                new (string, int)[] { ("Colour", 0x0026) },
                new (string, int)[] { ("Setup", 0x00c6), (".ctor", 0x1886) });
            AssertIl2CppOptions(typeof(UIWidgetSliderPip), 1, 2);
            Assert.That(typeof(UIWidgetSliderPip).BaseType, Is.EqualTo(typeof(UIWidget)));
        }

        // Setup 0x6A30F0 runs the (empty) UIWidget setup, reads the struct out of the parameters and tail-calls the virtual
        // Bindable<Color>.set_Value (slot 9) with its Colour. The constructor 0x6A3160 builds the bindable before the base
        // constructor, so a fresh widget holds the default colour.
        [Test]
        public void SetupPublishesTheColourOfTheParameters()
        {
            Owned.Run(owned =>
            {
                var pip = owned.Host("Lucid slider pip", false).AddComponent<UIWidgetSliderPip>();
                Assert.That(pip.Colour, Is.Not.Null);
                Assert.That(pip.Colour.Value, Is.EqualTo(default(Color)));

                pip.Setup(new UIWidgetSliderPipParameters(new Color(0.25f, 0.5f, 0.75f, 1f)));
                Assert.That(pip.Colour.Value, Is.EqualTo(new Color(0.25f, 0.5f, 0.75f, 1f)));

                pip.Setup(new UIWidgetSliderPipParameters(new Color(1f, 0f, 0.125f, 0.5f)));
                Assert.That(pip.Colour.Value, Is.EqualTo(new Color(1f, 0f, 0.125f, 0.5f)), "a later setup replaces the colour");
            });
        }

        [Test]
        public void EachPipHasItsOwnColourBinding()
        {
            Owned.Run(owned =>
            {
                var first = owned.Host("Lucid first slider pip", false).AddComponent<UIWidgetSliderPip>();
                var second = owned.Host("Lucid second slider pip", false).AddComponent<UIWidgetSliderPip>();

                first.Setup(new UIWidgetSliderPipParameters(Color.red));

                Assert.That(second.Colour, Is.Not.SameAs(first.Colour));
                Assert.That(second.Colour.Value, Is.EqualTo(default(Color)));
            });
        }
    }
}
