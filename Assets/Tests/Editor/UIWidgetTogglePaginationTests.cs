using System;
using System.Collections.Generic;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // UIWidgetTogglePaginationParameters (Game.Runtime 0x020009da, constructor x86-64 0x6A52D0) and UIWidgetTogglePagination
    // (0x020009d9, 0x6A4E90..0x6A51E0) as a real component on an inactive owned host over a real UIToggle and
    // UIToggleGroupElement. The toggle's transition has an empty settings list, so queuing a state transition is a no-op, as in the
    // mission toggle fixture. The serialized toggle, group element, selected event and transition are authored data.
    // Not covered: OnEnable (it starts a coroutine that waits for the UI before Refresh, which Edit Mode does not advance) and the
    // unlock animation of Refresh (0x6A5138, Unlock 0x6A5188), which plays the clip through the toggle's Animation component and
    // needs an active host.
    public sealed class UIWidgetTogglePaginationTests
    {
        private sealed class Rig
        {
            public UIWidgetTogglePagination Widget;
            public UIToggle Toggle;
            public UIToggleGroupElement Element;
            public int Selected;
            public readonly List<bool> Callbacks = new List<bool>();
        }

        private static void Run(Action<Rig> body) => Owned.Run(owned =>
        {
            var rig = new Rig();
            GameObject host = owned.Host("Lucid pagination toggle", false);
            rig.Widget = host.AddComponent<UIWidgetTogglePagination>();
            rig.Toggle = host.GetComponent<UIToggle>();
            rig.Element = host.AddComponent<UIToggleGroupElement>();
            var transition = host.AddComponent<UITransitionAnimation>();
            Set(transition, "m_settings", new List<UITransitionAnimationSetting>()); // Reflection: authored (empty) transition settings
            Set(rig.Toggle, "m_transition", transition); // Reflection: serialized reference
            Set(rig.Widget, "m_toggle", rig.Toggle); // Reflection: the editor Reset would fill this serialized reference
            Set(rig.Widget, "m_toggleGroupElement", rig.Element); // Reflection: serialized reference
            var selected = new UnityEvent();
            selected.AddListener(() => rig.Selected++);
            Set(rig.Widget, "m_onSelected", selected); // Reflection: serialized event
            body(rig);
        });

        private static UIWidgetTogglePaginationParameters Parameters(Rig rig, bool isNew = false, bool locked = false, bool unlockSeen = true,
            bool isVisible = true) => new UIWidgetTogglePaginationParameters(rig.Callbacks.Add, isNew, locked, unlockSeen, isVisible);

        // dump.cs TypeDefIndex 2520 and the DummyDll rows (fields 0x0400297f..0x04002984, methods 0x060038af..0x060038b7).
        [Test]
        public void TheWidgetDeclarationsMatchTheOriginalMetadata()
        {
            AssertMembers(typeof(UIWidgetTogglePagination), 0x00100001,
                new (string, int)[]
                {
                    ("m_toggleGroupElement", 0x0001), ("m_onSelected", 0x0001), ("m_unlockAnimationClip", 0x0001),
                    ("IsNew", 0x0026), ("IsVisible", 0x0026), ("m_parameters", 0x0001),
                },
                new (string, int)[]
                {
                    ("get_ToggleGroupElement", 0x0886), ("Setup", 0x00c6), ("OnEnable", 0x0081), ("Refresh", 0x0081), ("Lock", 0x0081),
                    ("Unlock", 0x0081), ("RefreshIsNew", 0x0081), ("Action_OnValueChanged", 0x00c6), (".ctor", 0x1886),
                });
            AssertIl2CppOptions(typeof(UIWidgetTogglePagination), 1, 2);
            Assert.That(typeof(UIWidgetTogglePagination).BaseType, Is.EqualTo(typeof(UIWidgetToggle)));
            foreach (string field in new[] { "m_toggleGroupElement", "m_onSelected", "m_unlockAnimationClip" })
                Assert.That(AttributeTypes(typeof(UIWidgetTogglePagination).GetField(field, Declared)), Is.EqualTo(new[] { typeof(SerializeField) }), field);
        }

        // dump.cs TypeDefIndex 2521 and the DummyDll rows (backing fields 0x04002985..0x04002988, methods 0x060038b8..0x060038bc).
        [Test]
        public void TheParametersDeclarationsMatchTheOriginalMetadata()
        {
            AssertMembers(typeof(UIWidgetTogglePaginationParameters), 0x00100001,
                new (string, int)[]
                {
                    ("<IsNew>k__BackingField", 0x0021), ("<Locked>k__BackingField", 0x0021), ("<UnlockSeen>k__BackingField", 0x0021),
                    ("<IsVisible>k__BackingField", 0x0021),
                },
                new (string, int)[]
                {
                    ("get_IsNew", 0x0886), ("get_Locked", 0x0886), ("get_UnlockSeen", 0x0886), ("get_IsVisible", 0x0886), (".ctor", 0x1886),
                });
            AssertIl2CppOptions(typeof(UIWidgetTogglePaginationParameters), 1, 2);
            Assert.That(typeof(UIWidgetTogglePaginationParameters).BaseType, Is.EqualTo(typeof(UIWidgetToggleParameters)));
        }

        // 0x6A52D0 calls the UIWidgetToggleParameters constructor (0x1A49F90) with string.Empty (the static read at 0x6A534C..0x6A5353)
        // and the callback, then stores the four booleans in one 32-bit write at +0x20..+0x23 in argument order.
        [Test]
        public void TheParametersKeepTheCallbackAnEmptyLabelAndEachFlag()
        {
            Action<bool> callback = _ => { };
            var parameters = new UIWidgetTogglePaginationParameters(callback, true, false, true, false);

            Assert.That(parameters.Label, Is.EqualTo(string.Empty));
            Assert.That(parameters.Callback, Is.SameAs(callback));
            Assert.That(parameters.IsNew, Is.True);
            Assert.That(parameters.Locked, Is.False);
            Assert.That(parameters.UnlockSeen, Is.True);
            Assert.That(parameters.IsVisible, Is.False);
        }

        // The four booleans are packed into one register (0x6A5317..0x6A533D); a single true flag must land only in its own property.
        [TestCase(0, TestName = "OnlyTheIsNewFlagReachesItsProperty")]
        [TestCase(1, TestName = "OnlyTheLockedFlagReachesItsProperty")]
        [TestCase(2, TestName = "OnlyTheUnlockSeenFlagReachesItsProperty")]
        [TestCase(3, TestName = "OnlyTheIsVisibleFlagReachesItsProperty")]
        public void ASingleTrueFlagReachesOnlyItsOwnProperty(int index)
        {
            var parameters = new UIWidgetTogglePaginationParameters(null, index == 0, index == 1, index == 2, index == 3);

            Assert.That(new[] { parameters.IsNew, parameters.Locked, parameters.UnlockSeen, parameters.IsVisible },
                Is.EqualTo(new[] { index == 0, index == 1, index == 2, index == 3 }));
        }

        // The constructor 0x6A51E0 builds the two bindables in field order before the UIWidgetToggle constructor.
        [Test]
        public void ConstructionBuildsTwoIndependentBindables()
        {
            Run(rig =>
            {
                Assert.That(rig.Widget.IsNew, Is.Not.Null);
                Assert.That(rig.Widget.IsVisible, Is.Not.Null);
                Assert.That(rig.Widget.IsNew, Is.Not.SameAs(rig.Widget.IsVisible));
                Assert.That(rig.Widget.IsNew.Value, Is.False);
                Assert.That(rig.Widget.IsVisible.Value, Is.False);
            });
        }

        // get_ToggleGroupElement 0x6A4E90 returns the field at +0x48.
        [Test]
        public void TheToggleGroupElementIsTheAuthoredOne()
        {
            Run(rig => Assert.That(rig.Widget.ToggleGroupElement, Is.SameAs(rig.Element)));
        }

        // Setup 0x6A4EA0 runs the toggle setup first (label and callback), publishes IsVisible from byte +0x23 and IsNew as
        // "toggle not on (byte +0x40 == 0, sete) and the parameters' IsNew (byte +0x20)".
        [TestCase(true, false, true, true, TestName = "ANewPageThatIsNotSelectedIsMarkedNew")]
        [TestCase(true, true, true, false, TestName = "ANewPageThatIsAlreadySelectedIsNotMarkedNew")]
        [TestCase(false, false, false, false, TestName = "AnOldPageIsNotMarkedNewAndStaysHiddenWhenItIsNotVisible")]
        [TestCase(false, true, true, false, TestName = "AnOldSelectedPageIsNotMarkedNew")]
        public void SetupPublishesTheVisibilityAndTheNewMarker(bool isNew, bool toggleIsOn, bool isVisible, bool expectedNew)
        {
            Run(rig =>
            {
                rig.Toggle.SetIsOnWithoutNotify(toggleIsOn);

                rig.Widget.Setup(Parameters(rig, isNew: isNew, isVisible: isVisible));

                Assert.That(rig.Widget.IsNew.Value, Is.EqualTo(expectedNew));
                Assert.That(rig.Widget.IsVisible.Value, Is.EqualTo(isVisible));
                Assert.That(rig.Widget.Label.Value, Is.EqualTo(string.Empty), "the toggle setup ran");
            });
        }

        // Setup subscribes RefreshIsNew (0x6A5030) to the toggle's value event (UnityEvent<bool>.AddListener at 0x6A500E), which
        // sets IsNew to "parameters' IsNew and not the new value" (xor sil, 1 then and with byte +0x20).
        [Test]
        public void TheNewMarkerFollowsTheToggleOnceSetUp()
        {
            Run(rig =>
            {
                rig.Widget.Setup(Parameters(rig, isNew: true));
                Assert.That(rig.Widget.IsNew.Value, Is.True);

                rig.Toggle.IsOn = true;
                Assert.That(rig.Widget.IsNew.Value, Is.False, "selecting the page clears the marker");

                rig.Toggle.IsOn = false;
                Assert.That(rig.Widget.IsNew.Value, Is.True, "deselecting a page that was never seen brings it back");
            });
        }

        [Test]
        public void APageThatIsNotNewNeverShowsTheMarker()
        {
            Run(rig =>
            {
                rig.Widget.Setup(Parameters(rig, isNew: false));

                rig.Toggle.IsOn = true;
                rig.Toggle.IsOn = false;

                Assert.That(rig.Widget.IsNew.Value, Is.False);
            });
        }

        // Action_OnValueChanged 0x6A51A0 runs the toggle's (the stored callback), then raises the selected event only for a true
        // value (test r14d at 0x6A51B4) and only when the event exists (test rdi at 0x6A51C0).
        [Test]
        public void SelectingRaisesTheSelectedEventAndDeselectingDoesNot()
        {
            Run(rig =>
            {
                rig.Widget.Setup(Parameters(rig));

                rig.Widget.Action_OnValueChanged(true);
                rig.Widget.Action_OnValueChanged(false);
                rig.Widget.Action_OnValueChanged(true);

                Assert.That(rig.Callbacks, Is.EqualTo(new[] { true, false, true }));
                Assert.That(rig.Selected, Is.EqualTo(2));
            });
        }

        [Test]
        public void AMissingSelectedEventIsSkipped()
        {
            Run(rig =>
            {
                rig.Widget.Setup(Parameters(rig));
                Set(rig.Widget, "m_onSelected", null); // Reflection: serialized event left unassigned

                Assert.DoesNotThrow(() => rig.Widget.Action_OnValueChanged(true));
                Assert.That(rig.Callbacks, Is.EqualTo(new[] { true }));
            });
        }

        // Refresh 0x6A50F0 returns at once without parameters (test rax at 0x6A50FA).
        [Test]
        public void RefreshWithoutParametersChangesNothing()
        {
            Run(rig =>
            {
                Call(rig.Widget, "Refresh");

                Assert.That(rig.Toggle.IsInteractable(), Is.True);
                Assert.That(rig.Element.enabled, Is.True);
            });
        }

        // Refresh 0x6A50F0: a locked page (byte +0x21) is made not interactable (SetInteractable 0x1A49E80, Lock 0x6A5150) and
        // its group element is disabled (Behaviour.set_enabled with false).
        [Test]
        public void ALockedPageCannotBeInteractedWith()
        {
            Run(rig =>
            {
                rig.Widget.Setup(Parameters(rig, locked: true));

                Call(rig.Widget, "Refresh");

                Assert.That(rig.Toggle.IsInteractable(), Is.False);
                Assert.That(rig.Element.enabled, Is.False);
            });
        }

        // An unlocked page whose unlock has been seen (byte +0x22 set, 0x6A512B) is left as it is.
        [Test]
        public void AnUnlockedPageWhoseUnlockWasSeenIsLeftAlone()
        {
            Run(rig =>
            {
                rig.Widget.Setup(Parameters(rig, locked: false, unlockSeen: true));

                Call(rig.Widget, "Refresh");

                Assert.That(rig.Toggle.IsInteractable(), Is.True);
                Assert.That(rig.Element.enabled, Is.True);
            });
        }
    }
}
