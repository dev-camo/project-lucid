using System;
using System.Collections.Generic;
using System.Linq;
using Hardlight;
using Hardlight.Utils;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.UiPublishingFixtures;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // UIEventFromInput (Game.Runtime 0x02000844, x86-64 0x60AB80..0x60AEC0) as a real component on an inactive owned host over a
    // real App, a real UIManager with a scripted container manager and the owned AudioManager of OwnedAudioServices. What the
    // audio manager is asked to play is read from its own diagnostics, as in OriginalAudioComponentPlaybackTests: a clip sent to
    // a source that was never added reports the missing source twice. The serialized fields are authored data and are set directly.
    public sealed class UIEventFromInputTests
    {
        private const GameInput Input = GameInput.CharacterSelectNext;
        private static readonly HLAudioClipIdentifier Click = (HLAudioClipIdentifier)0x1A21;

        private static readonly string[] ClickAtMissingSfx =
        {
            "Cannot load clip, the source Sfx does not exist.:0",
            "Cannot play one shot of audio clip lucid-ui-click, the source Sfx does not exist.:0",
        };

        // The input is leased for the case; the audio services are taken first, so they are released last.
        private static void Run(Action<Owned, OwnedAudioServices> body) => ControlTableLease.Run(() =>
        {
            using (var audio = new OwnedAudioServices())
                Owned.Run(owned => body(owned, audio));
        }, Input);

        private static UIEventFromInput NewComponent(Owned owned, UIModernEvent eventToFire)
        {
            var component = owned.Host("Lucid ui event from input", false).AddComponent<UIEventFromInput>();
            Set(component, "m_eventToFire", eventToFire);
            Set(component, "m_input", Input);
            return component;
        }

        // dump.cs TypeDefIndex 2115 and the DummyDll rows (fields 0x04001fa6..0x04001fac).
        [Test]
        public void DeclarationsMatchTheOriginalMetadata()
        {
            AssertMembers(typeof(UIEventFromInput), 0x00100001,
                new (string, int)[]
                {
                    ("m_eventToFire", 0x0001), ("m_input", 0x0001), ("m_exclusiveContainers", 0x0001), ("m_ignoreOnPlatforms", 0x0001),
                    ("m_audioSourceIdentifier", 0x0001), ("m_audioClipIdentifier", 0x0001), ("m_audioManagerRef", 0x0021),
                },
                new (string, int)[]
                {
                    ("OnEnable", 0x0081), ("OnDisable", 0x0081), ("OnInputTriggered", 0x0081), ("PlayOneShotClip", 0x0081), (".ctor", 0x1886),
                });
            AssertIl2CppOptions(typeof(UIEventFromInput), 2, 1);
            Assert.That(typeof(UIEventFromInput).BaseType, Is.EqualTo(typeof(MonoBehaviour)));
            Assert.That(AttributeTypes(typeof(UIEventFromInput).GetField("m_exclusiveContainers", Declared)),
                Is.EqualTo(new[] { typeof(TooltipAttribute), typeof(SerializeField) }));
            Assert.That(Tooltip(typeof(UIEventFromInput).GetField("m_exclusiveContainers", Declared)),
                Is.EqualTo("Set if this should only trigger if only this list of containers are open."));
            Assert.That(AttributeTypes(typeof(UIEventFromInput).GetField("m_ignoreOnPlatforms", Declared)),
                Is.EqualTo(new[] { typeof(TooltipAttribute), typeof(SerializeField) }));
            Assert.That(Tooltip(typeof(UIEventFromInput).GetField("m_ignoreOnPlatforms", Declared)),
                Is.EqualTo("Add any platforms that this input is not relevant to."));
            var hash = typeof(UIEventFromInput).GetField("m_audioClipIdentifier", Declared).GetCustomAttributesData();
            Assert.That(hash.Select(data => data.AttributeType).ToArray(), Is.EqualTo(new[] { typeof(SerializeField), typeof(HashEnumAttribute) }));
            Assert.That(hash.Single(data => data.AttributeType == typeof(HashEnumAttribute)).ConstructorArguments[0].Value, Is.Null);
        }

        // The constructor 0x60AEC0 stores the shared empty platform array (+0x38) and the AudioManager reference (+0x48).
        [Test]
        public void ConstructorStartsWithNoIgnoredPlatformsAndTheRegistryAudioManagerReference()
        {
            Run((owned, audio) =>
            {
                UIEventFromInput component = NewComponent(owned, null);

                Assert.That(Get<RuntimePlatform[]>(component, "m_ignoreOnPlatforms"), Is.SameAs(Array.Empty<RuntimePlatform>()));
                Assert.That(Get<SystemRef<AudioManager>>(component, "m_audioManagerRef"), Is.SameAs(ProcessManager.GetSystemRef<AudioManager>()));
            });
        }

        // OnEnable 0x60AB80 subscribes OnInputTriggered to the input with Subscribe(edx = 1: a Down trigger, ecx = -1: any
        // joystick); OnDisable 0x60AC30 calls Unsubscribe(input, callback, -1) and leaves nothing bound.
        [Test]
        public void EnablingBindsTheInputAndDisablingUnbindsIt()
        {
            Run((owned, audio) =>
            {
                UIEventFromInput component = NewComponent(owned, null);
                Assert.That(ControlTables.Subscriptions(Input, component), Is.Empty);

                Call(component, "OnEnable");
                Assert.That(ControlTables.Subscriptions(Input, component),
                    Is.EqualTo(ControlTables.Expected(InputTrigger.Down, "UIEventFromInput.OnInputTriggered")));

                Call(component, "OnDisable");
                Assert.That(ControlTables.Subscriptions(Input, component), Is.Empty);
            });
        }

        // OnInputTriggered 0x60ACE0 never reads its value: any press publishes the configured event, once (AddUnique).
        [Test]
        public void AnyInputValuePublishesTheEventOnce()
        {
            Run((owned, audio) =>
            {
                App app = NewApp(owned);
                UIModernEvent clicked = NewEvent(owned, "clicked");
                UIEventFromInput component = NewComponent(owned, clicked);

                foreach (float value in new[] { 0f, 1f, -3.5f })
                    Call(component, "OnInputTriggered", value);

                Assert.That(app.GetUIModernEvents(), Is.EqualTo(new[] { clicked }));
            });
        }

        // 0x60AD42..0x60AD79: the current platform in the ignore list returns before the container test and the App lookup. With
        // an App present nothing is published; with no App row at all the call still does not fault.
        [Test]
        public void AnIgnoredPlatformPublishesNothingAndNeedsNoApp()
        {
            Run((owned, audio) =>
            {
                UIModernEvent clicked = NewEvent(owned, "clicked");
                UIEventFromInput component = NewComponent(owned, clicked);
                Set(component, "m_ignoreOnPlatforms", new[] { RuntimePlatform.Android, Application.platform });

                Assert.DoesNotThrow(() => Call(component, "OnInputTriggered", 1f));
            });
            Run((owned, audio) =>
            {
                App app = NewApp(owned);
                UIModernEvent clicked = NewEvent(owned, "clicked");
                UIEventFromInput component = NewComponent(owned, clicked);
                RuntimePlatform other = Application.platform == RuntimePlatform.Android ? RuntimePlatform.IPhonePlayer : RuntimePlatform.Android;
                Set(component, "m_ignoreOnPlatforms", new[] { other });
                Call(component, "OnInputTriggered", 1f);
                Assert.That(app.GetUIModernEvents(), Is.EqualTo(new[] { clicked }), "another platform is ignored, this one is not");

                Set(component, "m_ignoreOnPlatforms", new[] { Application.platform });
                app.GetUIModernEvents().Clear();
                Call(component, "OnInputTriggered", 1f);
                Assert.That(app.GetUIModernEvents(), Is.Empty);
            });
        }

        // 0x60AD90..0x60ADD7: a null or empty container list skips the UIManager altogether (none is registered here) and
        // publishes. A non-empty list is passed to UIManager.AreExclusivelyOpen (0x60ADD0) and publishes only when it answers true.
        [Test]
        public void ContainersGateThePublishOnlyWhenListedAndNotExclusivelyOpen()
        {
            Run((owned, audio) =>
            {
                App app = NewApp(owned);
                UIModernEvent clicked = NewEvent(owned, "clicked");
                UIEventFromInput component = NewComponent(owned, clicked);

                Set(component, "m_exclusiveContainers", null);
                Call(component, "OnInputTriggered", 1f);
                Assert.That(app.GetUIModernEvents(), Is.EqualTo(new[] { clicked }), "a null list");

                app.GetUIModernEvents().Clear();
                Set(component, "m_exclusiveContainers", new List<UIContainerIdentifier>());
                Call(component, "OnInputTriggered", 1f);
                Assert.That(app.GetUIModernEvents(), Is.EqualTo(new[] { clicked }), "an empty list");
            });
            Run((owned, audio) =>
            {
                App app = NewApp(owned);
                NewUiManager(owned, out ScriptedExclusivityContainerManager containers);
                UIModernEvent clicked = NewEvent(owned, "clicked");
                UIEventFromInput component = NewComponent(owned, clicked);
                var listed = new List<UIContainerIdentifier> { NewIdentifier(owned, "menu"), NewIdentifier(owned, "options") };
                Set(component, "m_exclusiveContainers", listed);

                containers.Exclusive = false;
                Call(component, "OnInputTriggered", 1f);
                Assert.That(containers.ExclusiveQuery, Is.SameAs(listed), "the authored list is what is asked");
                Assert.That(app.GetUIModernEvents(), Is.Empty, "other containers are open");

                containers.Exclusive = true;
                Call(component, "OnInputTriggered", 1f);
                Assert.That(app.GetUIModernEvents(), Is.EqualTo(new[] { clicked }));
            });
        }

        // PlayOneShotClip 0x60AE30 needs a clip (+0x44) and a source (+0x40) that are not None and a live AudioManager; it then
        // asks the manager to play the one shot at that source. Truth table (clip set, source set, manager present, plays).
        [TestCase(true, true, true, true, TestName = "AClipAndASourceWithAnAudioManagerPlayTheOneShot")]
        [TestCase(false, true, true, false, TestName = "NoClipPlaysNothing")]
        [TestCase(true, false, true, false, TestName = "NoSourcePlaysNothing")]
        [TestCase(true, true, false, false, TestName = "NoAudioManagerPlaysNothing")]
        public void TheClickSoundNeedsAClipASourceAndAnAudioManager(bool clip, bool source, bool manager, bool plays)
        {
            Run((owned, audio) =>
            {
                App app = NewApp(owned);
                if (manager)
                {
                    audio.RegisterAudioManager();
                    audio.Clip(Click, "lucid-ui-click");
                }
                UIModernEvent clicked = NewEvent(owned, "clicked");
                UIEventFromInput component = NewComponent(owned, clicked);
                Set(component, "m_audioClipIdentifier", clip ? Click : HLAudioClipIdentifier.None);
                Set(component, "m_audioSourceIdentifier", source ? HLAudioSourceIdentifier.Sfx : HLAudioSourceIdentifier.None);

                Assert.DoesNotThrow(() => Call(component, "OnInputTriggered", 1f));

                Assert.That(app.GetUIModernEvents(), Is.EqualTo(new[] { clicked }), "the event is published whatever the sound does");
                Assert.That(audio.Log.Errors, Is.EqualTo(plays ? ClickAtMissingSfx : new string[0]));
            });
        }
    }
}
