using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.ChallengeRewardTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // UIWidgetCrossPromoPageSDT (Game.Runtime 0x020009af, x86-64 0x69D680..0x69D840), the game's cross promotion page, as a real
    // component on an inactive owned host. Every accessor is a bindable read or write; nothing here reaches a network or the
    // cross promotion system.
    public sealed class UIWidgetCrossPromoPageSdtTests
    {
        // dump.cs TypeDefIndex 2478 and the DummyDll rows (fields 0x04002893..0x04002895).
        [Test]
        public void DeclarationsMatchTheOriginalMetadata()
        {
            AssertMembers(typeof(UIWidgetCrossPromoPageSDT), 0x00100001,
                new (string, int)[] { ("Image", 0x0026), ("Text", 0x0026), ("IsReady", 0x0026) },
                new (string, int)[]
                {
                    ("SetSprite", 0x00c6), ("GetSprite", 0x00c6), ("SetText", 0x00c6), ("SetReadyState", 0x00c6), ("GetReadyState", 0x00c6),
                    (".ctor", 0x1886),
                });
            AssertIl2CppOptions(typeof(UIWidgetCrossPromoPageSDT), 2, 1);
            Assert.That(typeof(UIWidgetCrossPromoPageSDT).BaseType, Is.EqualTo(typeof(CrossPromoPageBase)));
        }

        // The constructor 0x69D720 builds the three bindables in field order (+0x20, +0x28, +0x30) before the base constructor.
        [Test]
        public void ConstructorBuildsThreeEmptyIndependentBindables()
        {
            Owned.Run(owned =>
            {
                var page = owned.Host("Lucid cross promotion page", false).AddComponent<UIWidgetCrossPromoPageSDT>();

                Assert.That(page.Image, Is.Not.Null.And.Not.SameAs(page.Text));
                Assert.That(page.Text, Is.Not.Null.And.Not.SameAs(page.IsReady));
                Assert.That(page.IsReady, Is.Not.Null);
                Assert.That(page.GetSprite(), Is.Null);
                Assert.That(page.Text.Value, Is.Null);
                Assert.That(page.GetReadyState(), Is.False);
            });
        }

        // 0x69D680..0x69D710: each setter writes its own bindable and each getter reads its own; the bindable is the only storage, so a
        // write made through the bindable shows in the getter.
        [Test]
        public void AccessorsReadAndWriteTheirOwnBindables()
        {
            Owned.Run(owned =>
            {
                var page = owned.Host("Lucid cross promotion page", false).AddComponent<UIWidgetCrossPromoPageSDT>();
                Sprite sprite = NewSprite(owned);

                page.SetSprite(sprite);
                page.SetText("Lucid ad text");
                page.SetReadyState(true);

                Assert.That(page.Image.Value, Is.SameAs(sprite));
                Assert.That(page.Text.Value, Is.EqualTo("Lucid ad text"));
                Assert.That(page.IsReady.Value, Is.True);
                Assert.That(page.GetSprite(), Is.SameAs(sprite));
                Assert.That(page.GetReadyState(), Is.True);

                Sprite other = NewSprite(owned);
                page.Image.Value = other;
                page.IsReady.Value = false;
                Assert.That(page.GetSprite(), Is.SameAs(other));
                Assert.That(page.GetReadyState(), Is.False);
                page.SetReadyState(false);
                Assert.That(page.IsReady.Value, Is.False);
            });
        }
    }
}
