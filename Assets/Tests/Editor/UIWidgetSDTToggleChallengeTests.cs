using System;
using System.Collections.Generic;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // UIWidgetSDTToggleChallenge (Game.Runtime 0x020009ca, x86-64 0x6A2580..0x6A2680) and its nested parameters (0x020009cb, 0x6A2780..0x6A27C0)
    // as a real component on an inactive owned host over a real UIToggle and UIToggleGroupElement. The toggle's transition is absent, so
    // changing the toggle only raises its value event. The serialized toggle and group element references are authored data.
    public sealed class UIWidgetSDTToggleChallengeTests
    {
        private sealed class Rig
        {
            public UIWidgetSDTToggleChallenge Widget;
            public UIToggle Toggle;
            public UIToggleGroupElement Element;
            public readonly List<bool> Callbacks = new List<bool>();
        }

        private static void Run(Action<Rig> body) => Owned.Run(owned =>
        {
            var rig = new Rig();
            GameObject host = owned.Host("Lucid challenge toggle", false);
            rig.Widget = host.AddComponent<UIWidgetSDTToggleChallenge>();
            rig.Toggle = host.GetComponent<UIToggle>();
            rig.Element = host.AddComponent<UIToggleGroupElement>();
            Set(rig.Toggle, "m_groupElement", rig.Element); // Reflection: serialized reference the editor fills in
            Set(rig.Widget, "m_toggle", rig.Toggle); // Reflection: the editor Reset would fill this serialized reference
            body(rig);
        });

        private static UIWidgetSDTToggleChallenge.UIWidgetToggleChallengeParameters Parameters(Rig rig, bool isNew = false, bool isComplete = false,
            string label = "lucid label", bool isBonus = false, bool isShadow = false) =>
            new UIWidgetSDTToggleChallenge.UIWidgetToggleChallengeParameters(isNew, isComplete, label, rig.Callbacks.Add, isBonus, isShadow);

        // dump.cs TypeDefIndex 2506 and the DummyDll rows (fields 0x04002914..0x04002917, methods 0x06003864..0x06003866).
        [Test]
        public void TheWidgetDeclarationsMatchTheOriginalMetadata()
        {
            AssertMembers(typeof(UIWidgetSDTToggleChallenge), 0x00100101,
                new (string, int)[] { ("IsNew", 0x0026), ("IsComplete", 0x0026), ("IsBonus", 0x0026), ("IsShadow", 0x0026) },
                new (string, int)[] { ("get_GroupElement", 0x0886), ("Setup", 0x00c6), (".ctor", 0x1886) });
            AssertIl2CppOptions(typeof(UIWidgetSDTToggleChallenge), 1, 2);
            Assert.That(typeof(UIWidgetSDTToggleChallenge).BaseType, Is.EqualTo(typeof(UIWidgetToggle)));
        }

        // dump.cs TypeDefIndex 2505 and the DummyDll rows (backing fields 0x04002918..0x0400291b, methods 0x06003867..0x0600386b).
        [Test]
        public void TheParametersDeclarationsMatchTheOriginalMetadata()
        {
            Type parameters = typeof(UIWidgetSDTToggleChallenge.UIWidgetToggleChallengeParameters);
            AssertMembers(parameters, 0x00100102,
                new (string, int)[]
                {
                    ("<IsNew>k__BackingField", 0x0021), ("<IsComplete>k__BackingField", 0x0021), ("<IsBonus>k__BackingField", 0x0021),
                    ("<IsShadow>k__BackingField", 0x0021),
                },
                new (string, int)[] { ("get_IsNew", 0x0886), ("get_IsComplete", 0x0886), ("get_IsBonus", 0x0886), ("get_IsShadow", 0x0886), (".ctor", 0x1886) });
            AssertIl2CppOptions(parameters, 1, 2);
            Assert.That(parameters.BaseType, Is.EqualTo(typeof(UIWidgetToggleParameters)));
            var optional = parameters.GetConstructors()[0].GetParameters();
            Assert.That(optional[4].HasDefaultValue && optional[5].HasDefaultValue, Is.True, "isBonus and isShadow are optional");
            Assert.That(optional[4].DefaultValue, Is.False);
            Assert.That(optional[5].DefaultValue, Is.False);
        }

        // 0x6A27C0 passes label and callback to the UIWidgetToggleParameters constructor (0x1A49F90) and stores the four booleans in one
        // 32-bit write at +0x20..+0x23 in the order isNew, isComplete, isBonus, isShadow (the arguments arrive in the order isNew,
        // isComplete, label, callback, isBonus, isShadow).
        [Test]
        public void TheParametersKeepTheLabelTheCallbackAndEachFlag()
        {
            Action<bool> callback = _ => { };
            var parameters = new UIWidgetSDTToggleChallenge.UIWidgetToggleChallengeParameters(true, false, "label", callback, true, false);

            Assert.That(parameters.Label, Is.EqualTo("label"));
            Assert.That(parameters.Callback, Is.SameAs(callback));
            Assert.That(new[] { parameters.IsNew, parameters.IsComplete, parameters.IsBonus, parameters.IsShadow }, Is.EqualTo(new[] { true, false, true, false }));
        }

        // The bonus and shadow flags default to false; a single true flag must land only in its own property.
        [TestCase(0, TestName = "OnlyTheIsNewFlagReachesItsProperty")]
        [TestCase(1, TestName = "OnlyTheIsCompleteFlagReachesItsProperty")]
        [TestCase(2, TestName = "OnlyTheIsBonusFlagReachesItsProperty")]
        [TestCase(3, TestName = "OnlyTheIsShadowFlagReachesItsProperty")]
        public void ASingleTrueFlagReachesOnlyItsOwnProperty(int index)
        {
            var parameters = new UIWidgetSDTToggleChallenge.UIWidgetToggleChallengeParameters(index == 0, index == 1, null, null, index == 2, index == 3);

            Assert.That(new[] { parameters.IsNew, parameters.IsComplete, parameters.IsBonus, parameters.IsShadow },
                Is.EqualTo(new[] { index == 0, index == 1, index == 2, index == 3 }));
        }

        [Test]
        public void TheOptionalFlagsAreFalseWhenLeftOut()
        {
            var parameters = new UIWidgetSDTToggleChallenge.UIWidgetToggleChallengeParameters(true, true, "label", null);

            Assert.That(parameters.IsBonus, Is.False);
            Assert.That(parameters.IsShadow, Is.False);
        }

        // The constructor 0x6A2680 creates the four bindables in field order (+0x48, +0x50, +0x58, +0x60) before the UIWidgetToggle constructor.
        [Test]
        public void ConstructionBuildsFourIndependentBindables()
        {
            Run(rig =>
            {
                var bindables = new object[] { rig.Widget.IsNew, rig.Widget.IsComplete, rig.Widget.IsBonus, rig.Widget.IsShadow };

                Assert.That(bindables, Has.All.Not.Null);
                Assert.That(new HashSet<object>(bindables).Count, Is.EqualTo(4));
                Assert.That(new[] { rig.Widget.IsNew.Value, rig.Widget.IsComplete.Value, rig.Widget.IsBonus.Value, rig.Widget.IsShadow.Value },
                    Is.EqualTo(new[] { false, false, false, false }));
            });
        }

        // get_GroupElement 0x6A2580 loads the toggle (+0x20) and returns its group element (+0x48).
        [Test]
        public void TheGroupElementIsTheToggles()
        {
            Run(rig => Assert.That(rig.Widget.GroupElement, Is.SameAs(rig.Element)));
        }

        // Setup 0x6A2590 runs the toggle setup first (label and callback), then stores the four booleans in the bindables in field order.
        [Test]
        public void SetupPublishesTheLabelAndEachMarker()
        {
            Run(rig =>
            {
                rig.Widget.Setup(Parameters(rig, isNew: true, isComplete: false, label: "3", isBonus: true, isShadow: false));

                Assert.That(rig.Widget.Label.Value, Is.EqualTo("3"));
                Assert.That(rig.Widget.IsNew.Value, Is.True);
                Assert.That(rig.Widget.IsComplete.Value, Is.False);
                Assert.That(rig.Widget.IsBonus.Value, Is.True);
                Assert.That(rig.Widget.IsShadow.Value, Is.False);

                rig.Widget.Setup(Parameters(rig, isNew: false, isComplete: true, label: "4", isBonus: false, isShadow: true));

                Assert.That(rig.Widget.Label.Value, Is.EqualTo("4"));
                Assert.That(rig.Widget.IsNew.Value, Is.False);
                Assert.That(rig.Widget.IsComplete.Value, Is.True);
                Assert.That(rig.Widget.IsBonus.Value, Is.False);
                Assert.That(rig.Widget.IsShadow.Value, Is.True);
            });
        }

        // After the markers, 0x6A2643 calls IsSelected (0x1A49E40) and, for a selected toggle with a callback (test rax at 0x6A2650), invokes
        // the callback with true (mov esi, 1 at 0x6A2661; the delegate is entered by the tail call at 0x6A266A).
        [Test]
        public void ASelectedToggleReportsItselfSelectedThroughTheCallback()
        {
            Run(rig =>
            {
                rig.Toggle.SetIsOnWithoutNotify(true);

                rig.Widget.Setup(Parameters(rig));

                Assert.That(rig.Callbacks, Is.EqualTo(new[] { true }));
            });
        }

        [Test]
        public void AToggleThatIsNotSelectedDoesNotCallBack()
        {
            Run(rig =>
            {
                rig.Widget.Setup(Parameters(rig));

                Assert.That(rig.Callbacks, Is.Empty);
            });
        }

        // A selected toggle without a callback skips the call (the test at 0x6A2650 jumps to the return at 0x6A266C).
        [Test]
        public void ASelectedToggleWithoutACallbackSetsUpQuietly()
        {
            Run(rig =>
            {
                rig.Toggle.SetIsOnWithoutNotify(true);
                var parameters = new UIWidgetSDTToggleChallenge.UIWidgetToggleChallengeParameters(true, true, "label", null);

                Assert.That(() => rig.Widget.Setup(parameters), Throws.Nothing);

                Assert.That(rig.Widget.IsNew.Value, Is.True);
            });
        }

        // The cast of the parameters at 0x6A25C6..0x6A25D8 compares the exact class and raises an invalid cast otherwise (0x6A2674); the
        // toggle setup has already run by then (0x6A25C1 precedes the test), so the label and callback of the plain toggle parameters
        // were taken but no marker was published.
        [Test]
        public void ParametersOfAnotherClassFailTheCastAfterTheToggleSetup()
        {
            Run(rig =>
            {
                var plain = new UIWidgetToggleParameters("plain", rig.Callbacks.Add);

                Assert.That(() => rig.Widget.Setup(plain), Throws.TypeOf<InvalidCastException>());

                Assert.That(rig.Widget.Label.Value, Is.EqualTo("plain"));
                Assert.That(rig.Widget.IsNew.Value, Is.False);
            });
        }
    }
}
