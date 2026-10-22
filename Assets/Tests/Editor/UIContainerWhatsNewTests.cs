using System;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.ProgressionContainerTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // UIContainerWhatsNew (Game.Runtime 02000950, x86-64 0x683AF0..0x683D30) as a real component on an owned inactive
    // host, with a real Animation of legacy clips and the real App of OwnedCharacterManagers. Start is called directly
    // because Edit Mode never runs it.
    public sealed class UIContainerWhatsNewTests
    {
        private sealed class Rig
        {
            public App App;
            public UIContainerWhatsNew Container;
            public Animation Animation;
            public AnimationClip Open, Close;
            public UIModernEvent CloseEvent;
        }

        private static void WithRig(Action<Rig> body) => WithApp((owned, app) =>
        {
            var rig = new Rig { App = app };
            rig.Container = NewContainer<UIContainerWhatsNew>(owned);
            rig.Animation = owned.Host("Lucid whats new animation", true).AddComponent<Animation>();
            rig.Open = NewClip(owned, "Lucid whats new open");
            rig.Close = NewClip(owned, "Lucid whats new close");
            rig.Animation.AddClip(rig.Open, rig.Open.name);
            rig.Animation.AddClip(rig.Close, rig.Close.name);
            rig.CloseEvent = NewEvent(owned);
            // Serialized authored references.
            Set(rig.Container, "m_animation", rig.Animation);
            Set(rig.Container, "m_openClip", rig.Open); // Reflection: serialized m_openClip the editor assigns
            Set(rig.Container, "m_closeClip", rig.Close); // Reflection: serialized m_closeClip the editor assigns
            Set(rig.Container, "m_closeEvent", rig.CloseEvent); // Reflection: serialized m_closeEvent the editor assigns
            body(rig);
        });

        // dump.cs TypeDefIndex 2383 and the DummyDll rows (fields 0x0400266c..0x04002670, methods 0x0600358b..0x06003590).
        [Test]
        public void DeclarationsMatchTheOriginalMetadata()
        {
            AssertMembers(typeof(UIContainerWhatsNew), 0x00100101,
                new (string, int)[]
                {
                    ("m_animation", 0x0001),
                    ("m_closeClip", 0x0001),
                    ("m_openClip", 0x0001),
                    ("m_closeEvent", 0x0001),
                    ("m_app", 0x0001),
                },
                new (string, int)[]
                {
                    ("Start", 0x0081),
                    ("Action_Close", 0x0086),
                    ("Action_AnimationCloseFinish", 0x0086),
                    ("AnimateIn", 0x0081),
                    ("Close", 0x0081),
                    (".ctor", 0x1886),
                });
            AssertIl2CppOptions(typeof(UIContainerWhatsNew), 1, 2);
            Assert.That(typeof(UIContainerWhatsNew).BaseType, Is.EqualTo(typeof(UIContainer)));
        }

        // Start 0x683AF0 stores the App (0x683B53) and tail-calls AnimateIn 0x683B70, which plays the open clip by name
        // (get_name 0x683BFF, Animation.Play 0x683C16).
        [Test]
        public void StartFetchesTheAppAndPlaysTheOpenClip()
        {
            WithRig(rig =>
            {
                Call(rig.Container, "Start"); // Reflection: Unity sends Start; Edit Mode does not
                Assert.That(Get<App>(rig.Container, "m_app"), Is.SameAs(rig.App));
                Assert.That(rig.Animation.IsPlaying(rig.Open.name), Is.True);
                Assert.That(rig.Animation.IsPlaying(rig.Close.name), Is.False);
                Assert.That(rig.App.GetUIModernEvents(), Is.Empty);
            });
        }

        // AnimateIn 0x683B70 returns without playing when either the animation (0x683BBC) or the open clip (0x683BE1) is
        // missing; Start still stores the App first.
        [Test]
        public void StartPlaysNothingWithoutAnAnimationOrAnOpenClip()
        {
            WithRig(rig =>
            {
                Set(rig.Container, "m_openClip", null); // Reflection: serialized m_openClip the editor assigns
                Call(rig.Container, "Start"); // Reflection: Unity sends Start; Edit Mode does not
                Assert.That(Get<App>(rig.Container, "m_app"), Is.SameAs(rig.App));
                Assert.That(rig.Animation.isPlaying, Is.False, "no open clip");

                Set(rig.Container, "m_openClip", rig.Open); // Reflection: serialized m_openClip the editor assigns
                Set(rig.Container, "m_animation", null); // Reflection: serialized m_animation the editor assigns
                Assert.DoesNotThrow(() => Call(rig.Container, "Start"), "no animation"); // Reflection: Unity sends Start; Edit Mode does not
                Assert.That(rig.Animation.isPlaying, Is.False);
            });
        }

        // Action_Close 0x683C20 plays the close clip by name (0x683CD4) when the animation and the open clip both exist, and
        // publishes nothing.
        [Test]
        public void ClosingPlaysTheCloseClipWhenTheScreenHasAnAnimation()
        {
            WithRig(rig =>
            {
                Call(rig.Container, "Start"); // Reflection: Unity sends Start; Edit Mode does not
                rig.Container.Action_Close();
                Assert.That(rig.Animation.IsPlaying(rig.Close.name), Is.True);
                Assert.That(rig.App.GetUIModernEvents(), Is.Empty);
            });
        }

        // The guard of Action_Close tests the animation (0x683C6C) and the OPEN clip (0x683C91), not the close clip it then
        // plays: with an open clip missing and a close clip present the event is published and the close clip stays
        // stopped. Without an animation the event is published too (0x683CAE, AddUniqueUIModernEvent 0x530D60).
        [Test]
        public void ClosingPublishesTheCloseEventWhenTheOpenClipOrTheAnimationIsMissing()
        {
            WithRig(rig =>
            {
                Set(rig.Container, "m_openClip", null); // Reflection: serialized m_openClip the editor assigns
                Call(rig.Container, "Start"); // Reflection: Unity sends Start; Edit Mode does not
                rig.Container.Action_Close();
                Assert.That(rig.App.GetUIModernEvents(), Is.EqualTo(new[] { rig.CloseEvent }));
                Assert.That(rig.Animation.IsPlaying(rig.Close.name), Is.False, "the close clip is not what the guard tests");
            });
            WithRig(rig =>
            {
                Call(rig.Container, "Start"); // Reflection: Unity sends Start; Edit Mode does not
                Set(rig.Container, "m_animation", null); // Reflection: serialized m_animation the editor assigns
                rig.Container.Action_Close();
                Assert.That(rig.App.GetUIModernEvents(), Is.EqualTo(new[] { rig.CloseEvent }));
            });
        }

        // Action_AnimationCloseFinish 0x683D00 and Close 0x683CE0 each tail-call AddUniqueUIModernEvent with the close
        // event, so repeating either adds nothing.
        [Test]
        public void TheCloseFinishAndTheCloseHelperPublishTheCloseEventOnce()
        {
            WithRig(rig =>
            {
                Call(rig.Container, "Start"); // Reflection: Unity sends Start; Edit Mode does not
                rig.Container.Action_AnimationCloseFinish();
                Assert.That(rig.App.GetUIModernEvents(), Is.EqualTo(new[] { rig.CloseEvent }));
                rig.Container.Action_AnimationCloseFinish();
                Call(rig.Container, "Close"); // Reflection: private UIContainerWhatsNew.Close
                Assert.That(rig.App.GetUIModernEvents(), Is.EqualTo(new[] { rig.CloseEvent }));
            });
            WithRig(rig =>
            {
                Call(rig.Container, "Start"); // Reflection: Unity sends Start; Edit Mode does not
                Call(rig.Container, "Close"); // Reflection: private UIContainerWhatsNew.Close
                Assert.That(rig.App.GetUIModernEvents(), Is.EqualTo(new[] { rig.CloseEvent }));
            });
        }
    }
}
