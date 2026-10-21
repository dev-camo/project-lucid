using System;
using System.Collections.Generic;
using System.Linq;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using ProjectLucid.Tests.OnScreenControlsShared;
using UnityEngine;
using static ProjectLucid.Tests.OnScreenControlTestServices;

namespace ProjectLucid.Tests
{
    // OnScreenControlInputHandler (02000970) against its 1.10.1 x86-64 bodies (0x68D000..0x68F02F). The handler sits
    // on an owned inactive object with probe touch zones, a UIRuntimeConfiguration whose bridge answers the case's
    // last input type, and an InputSystem holding a real InputMonitor. InputBridge's static type action is taken
    // over for the case and restored. Not covered: the distribution of real touches to the zones (the best zone
    // by score, ended and updated touches), which needs a touch source that reports touches; the monitor's source
    // reports none outside play mode.
    public sealed class OnScreenControlInputHandlerTests
    {
        private sealed class Rig
        {
            public Services Services;
            public OnScreenControlInputHandler Handler;
            public OnScreenTouchZoneProbe[] Zones;
            public GameObject[] Graphics;
            public InputMonitor Monitor;
            public OnScreenControlInputBridgeProbe Bridge;
        }

        private static object Call(Rig rig, string name, params object[] arguments) =>
            ModernUITestSupport.Call(typeof(OnScreenControlInputHandler), rig.Handler, name, arguments);

        private static void WithHandler(Action<Rig> body) => With(services =>
        {
            FastAction<UIInputType> prior = InputBridge.OnUpdateLastInputType;
            InputBridge.OnUpdateLastInputType = null;
            try
            {
                var owned = services.Owned;
                var rig = new Rig { Services = services };
                rig.Bridge = owned.Track(ScriptableObject.CreateInstance<OnScreenControlInputBridgeProbe>());
                var configuration = owned.Host("Lucid owned ui configuration", false).AddComponent<UIRuntimeConfiguration>();
                ModernUITestSupport.Set(configuration, "m_inputBridge", rig.Bridge); // serialized in the original
                owned.Register(configuration);
                rig.Monitor = owned.Track(ScriptableObject.CreateInstance<InputMonitor>());
                var inputSystem = owned.Host("Lucid owned input system", false).AddComponent<InputSystem>();
                ModernUITestSupport.Set(inputSystem, "m_inputMonitor", rig.Monitor); // serialized in the original
                owned.Register(inputSystem);
                rig.Zones = new[]
                {
                    owned.Host("Lucid owned touch zone one", false).AddComponent<OnScreenTouchZoneProbe>(),
                    owned.Host("Lucid owned touch zone two", false).AddComponent<OnScreenTouchZoneProbe>(),
                };
                rig.Graphics = new[] { owned.Track(new GameObject("Lucid owned graphic one")), owned.Track(new GameObject("Lucid owned graphic two")) };
                rig.Handler = owned.Host("Lucid owned input handler", false).AddComponent<OnScreenControlInputHandler>();
                ModernUITestSupport.Set(rig.Handler, "m_controlZones", rig.Zones); // serialized in the original
                ModernUITestSupport.Set(rig.Handler, "m_graphicsToToggle", rig.Graphics); // serialized in the original
                ModernUITestSupport.Set(rig.Handler, "m_uiInputTypesWhichToggleOn", new List<UIInputType> { UIInputType.Touch }); // serialized in the original
                body(rig);
            }
            finally
            {
                InputBridge.OnUpdateLastInputType = prior;
            }
        });

        private static Touch TouchAt(int fingerId, float x, float y) =>
            new Touch { fingerId = fingerId, position = new Vector2(x, y), phase = TouchPhase.Moved };

        private static bool[] GraphicsActive(Rig rig) => rig.Graphics.Select(graphic => graphic.activeSelf).ToArray();

        // OnEnable 0x68D000 takes the input monitor from the InputSystem (+0x28), shows the graphics when the bridge's
        // last input type is one of the serialized types, listens for input type changes, and initialises each zone.
        // OnInputTypeChanged 0x68D270 repeats the graphics rule; OnDisable 0x68D310 shuts each zone down with the
        // last rule's answer and stops listening.
        [Test]
        public void EnablingFollowsTheLastInputTypeAndDisablingShutsTheZonesDownWithIt()
        {
            WithHandler(rig =>
            {
                rig.Bridge.LastInputType = UIInputType.Mouse;
                Call(rig, "OnEnable");
                Assert.That(ModernUITestSupport.Get(rig.Handler, "m_inputMonitor"), Is.SameAs(rig.Monitor));
                Assert.That(GraphicsActive(rig), Is.EqualTo(new[] { false, false }), "Mouse is not listed");
                Assert.That(rig.Zones.Select(zone => zone.Initialised).ToArray(), Is.EqualTo(new[] { 1, 1 }));
                Assert.That(InputBridge.OnUpdateLastInputType.GetInvocationList().Select(action => action.Target).ToArray(), Is.EqualTo(new object[] { rig.Handler }));

                FastAction<UIInputType>.Invoke(InputBridge.OnUpdateLastInputType, UIInputType.Touch);
                Assert.That(GraphicsActive(rig), Is.EqualTo(new[] { true, true }), "Touch is listed");
                FastAction<UIInputType>.Invoke(InputBridge.OnUpdateLastInputType, UIInputType.Keyboard);
                Assert.That(GraphicsActive(rig), Is.EqualTo(new[] { false, false }));
                FastAction<UIInputType>.Invoke(InputBridge.OnUpdateLastInputType, UIInputType.Touch);

                Call(rig, "OnDisable");
                Assert.That(rig.Zones.Select(zone => zone.Shutdowns.ToArray()).ToArray(), Is.EqualTo(new[] { new[] { true }, new[] { true } }), "the last answer was valid");
                Assert.That(InputBridge.OnUpdateLastInputType == null || InputBridge.OnUpdateLastInputType.GetInvocationListCount() == 0, Is.True);
            });
        }

        // The listed answer at enabling time is what the zones are shut down with.
        [Test]
        public void ZonesAreShutDownAsNotValidWhenTheLastInputTypeIsNotListed()
        {
            WithHandler(rig =>
            {
                rig.Bridge.LastInputType = UIInputType.Mouse;
                Call(rig, "OnEnable");
                Call(rig, "OnDisable");
                Assert.That(rig.Zones.Select(zone => zone.Shutdowns.ToArray()).ToArray(), Is.EqualTo(new[] { new[] { false }, new[] { false } }));
            });
        }

        // Update 0x68D430 returns at once with no input monitor (OnEnable has not run), touching no zone.
        [Test]
        public void UpdateDoesNothingWithoutAnInputMonitor()
        {
            WithHandler(rig =>
            {
                rig.Zones[0].StartTrackingTouch(TouchAt(1, 10f, 10f));
                Call(rig, "Update");
                Assert.That(rig.Zones[0].PointerIsDown, Is.True);
                Assert.That(rig.Zones.SelectMany(zone => zone.Order).ToArray(), Is.Empty);
                Assert.That(ModernUITestSupport.Get(rig.Handler, "m_firstUpdateComplete"), Is.EqualTo(false));
            });
        }

        // With a monitor, Update still returns while the provider has no touch source (0x68D5C1..0x68D629: the
        // provider's TouchSource is null until the provider is initialised). Once it has one that reports no touches,
        // every zone is cleared (0x68D8C0), each active zone supplies its (now empty) state, which releases a
        // pointer that was down, and the first update is complete.
        [Test]
        public void UpdateWaitsForATouchSourceThenClearsAndSuppliesTheActiveZones()
        {
            WithHandler(rig =>
            {
                rig.Bridge.LastInputType = UIInputType.Touch;
                Call(rig, "OnEnable");
                foreach (OnScreenTouchZoneProbe zone in rig.Zones)
                {
                    ModernUITestSupport.Set(zone, "m_restrictToSingleTouch", true); // serialized in the original
                    zone.StartTrackingTouch(TouchAt(1, 10f, 10f));
                    zone.SupplyTouchData();
                    zone.Order.Clear();
                }
                rig.Zones[0].ActiveOverride = true;
                rig.Zones[1].ActiveOverride = false;

                Call(rig, "Update");
                Assert.That(rig.Zones.Select(zone => zone.PointerIsDown).ToArray(), Is.EqualTo(new[] { true, true }), "no touch source: nothing changes");
                Assert.That(ModernUITestSupport.Get(rig.Handler, "m_firstUpdateComplete"), Is.EqualTo(false));

                ((SourceProvider)rig.Monitor.SourceProvider).Initialise(null);
                Call(rig, "Update");
                Assert.That(rig.Zones.Select(zone => zone.PointerIsDown).ToArray(), Is.EqualTo(new[] { false, false }), "every zone is cleared");
                Assert.That(rig.Zones[0].Order, Is.EqualTo(new[] { "release", "held" }), "the active zone releases its pointer");
                Assert.That(rig.Zones[1].Order, Is.Empty, "the inactive zone supplies nothing");
                Assert.That(ModernUITestSupport.Get(rig.Handler, "m_firstUpdateComplete"), Is.EqualTo(true));
            });
        }

        // The constructor 0x68EF10 starts with an empty type list, the mouse flag set, and a touch list of capacity 5.
        [Test]
        public void TheConstructorSeedsTheTouchListAndTheMouseFlag()
        {
            WithHandler(rig =>
            {
                Assert.That((bool)ModernUITestSupport.Get(rig.Handler, "m_mouseShowsInEditor"), Is.True);
                var touches = (List<Touch>)ModernUITestSupport.Get(rig.Handler, "m_currentTouches");
                Assert.That((touches.Count, touches.Capacity), Is.EqualTo((0, 5)));
            });
        }
    }
}
