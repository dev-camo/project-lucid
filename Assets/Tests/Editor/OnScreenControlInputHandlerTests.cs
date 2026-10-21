using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using ProjectLucid.Tests.OnScreenControlsShared;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using static ProjectLucid.Tests.OnScreenControlTestServices;

namespace ProjectLucid.Tests
{
    // OnScreenControlInputHandler (02000970) against its 1.10.1 x86-64 bodies (0x68D000..0x68F02F). The handler sits
    // on an owned inactive object with probe touch zones, a UIRuntimeConfiguration whose bridge answers the case's
    // last input type, and an InputSystem holding a real InputMonitor. InputBridge's static type action is taken
    // over for the case and restored. The touch cases fill the list of active touches of the provider's mouse
    // source (the touch source the handler reads), which the handler never refreshes itself. Not covered: touches
    // a player would produce (position, phase and finger ids from the engine) and a pointer over a UI object, which
    // needs an input module that is running.
    public sealed class OnScreenControlInputHandlerTests
    {
        private static readonly Vector2 Zero = Vector2.zero;

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
                // Destroying a monitor that was never initialised faults in the original too: OnDestroy (0x0600017e) runs
                // Shutdown, whose first call (BaseInputMonitor 0x06000188) asks IsActiveOnPlatform (0x06000180) for a platform
                // list that a new, unserialised monitor does not have. Unity logs the exception when the case releases it.
                LogAssert.Expect(LogType.Exception, new Regex(@"ArgumentNullException: Value cannot be null\.\s+Parameter name: array"));
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

        private const BindingFlags NonPublicInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        // The handler on a monitor whose touch source reports the returned list, two zones that accept touches
        // anywhere in a 100 by 100 area around the origin, and an EventSystem without an input module, which reports
        // no pointer over a UI object. Edit Mode runs no OnEnable or OnDisable for the EventSystem, so the case calls
        // them to join and leave the engine's list of event systems, and puts the previous current one back.
        private static void WithTouches(Action<Rig, List<Touch>> body) => WithHandler(rig =>
        {
            var owned = rig.Services.Owned;
            EventSystem priorSystem = EventSystem.current;
            var eventSystem = owned.Host("Lucid owned event system", true).AddComponent<EventSystem>();
            ModernUITestSupport.Call(eventSystem, "OnEnable");
            EventSystem.current = eventSystem;
            try
            {
                rig.Bridge.LastInputType = UIInputType.Touch;
                Call(rig, "OnEnable");
                ((SourceProvider)rig.Monitor.SourceProvider).Initialise(null);
                object source = rig.Monitor.SourceProvider.TouchSource;
                var touches = (List<Touch>)source.GetType().GetField("m_currentActiveTouches", NonPublicInstance).GetValue(source);
                foreach (OnScreenTouchZoneProbe zone in rig.Zones)
                {
                    var area = owned.Track(new GameObject("Lucid owned touch area", typeof(RectTransform))).GetComponent<RectTransform>();
                    area.sizeDelta = new Vector2(100f, 100f);
                    ModernUITestSupport.Set(zone, "m_validTouchTransform", area); // serialized in the original
                    zone.ActiveOverride = true;
                }
                body(rig, touches);
            }
            finally
            {
                ModernUITestSupport.Call(eventSystem, "OnDisable");
                if (priorSystem != null) EventSystem.current = priorSystem;
            }
        });

        private static Touch TouchIn(int fingerId, TouchPhase phase, float x, float y) =>
            new Touch { fingerId = fingerId, phase = phase, position = new Vector2(x, y) };

        private static bool[] Tracking(Rig rig, int fingerId) =>
            rig.Zones.Select(zone => zone.IsTrackingTouch(TouchAt(fingerId, 0f, 0f))).ToArray();

        // Update 0x68D430 gives a new touch to the zone with the lowest score among those that can take it and
        // contain it (0x68DEC9..0x68DEED: a zone is taken when none was found or the best is above its score), and
        // before the first update has completed (0x68DD0C..0x68DD1E) a Moved touch starts tracking too. Touches a
        // zone already follows go to UpdateTouch, or to StopTrackingTouch for Ended (3) and Canceled (4)
        // (0x68D970, 0x68DBDA). The search stops at the first zone scoring 0 or less (ucomiss, jae), so the later
        // zone scoring -1 loses to the earlier one scoring 0.
        [Test]
        public void TouchesGoToTheLowestScoringZoneAndFollowTheirPhaseAfterTheFirstUpdate()
        {
            WithTouches((rig, touches) =>
            {
                OnScreenTouchZoneProbe first = rig.Zones[0], second = rig.Zones[1];
                first.ScoreOverride = 5f;
                second.ScoreOverride = 2f;
                touches.Add(TouchIn(1, TouchPhase.Moved, 10f, 10f));
                Call(rig, "Update");
                Assert.That(Tracking(rig, 1), Is.EqualTo(new[] { false, true }), "the lower score takes the Moved touch of the first update");
                Assert.That(second.Processed, Is.EqualTo(new[] { (new Vector2(10f, 10f), Zero, 0f, 0f) }));
                Assert.That(first.Order, Is.Empty);
                Assert.That(ModernUITestSupport.Get(rig.Handler, "m_firstUpdateComplete"), Is.EqualTo(true));

                touches.Clear();
                touches.Add(TouchIn(1, TouchPhase.Moved, 20f, 30f));
                touches.Add(TouchIn(2, TouchPhase.Moved, 5f, 5f));
                Call(rig, "Update");
                Assert.That(second.Processed[1], Is.EqualTo((new Vector2(20f, 30f), new Vector2(10f, 20f), 0f, 0f)), "a followed touch is updated");
                Assert.That(Tracking(rig, 2), Is.EqualTo(new[] { false, false }), "a Moved touch is no longer a new touch");

                first.ScoreOverride = 0f;
                second.ScoreOverride = -1f;
                touches.Clear();
                touches.Add(TouchIn(1, TouchPhase.Ended, 20f, 30f));
                touches.Add(TouchIn(2, TouchPhase.Began, 5f, 5f));
                second.Order.Clear();
                Call(rig, "Update");
                Assert.That(Tracking(rig, 1), Is.EqualTo(new[] { false, false }), "an Ended touch is dropped");
                Assert.That(second.Processed.Count, Is.EqualTo(2), "and not updated");
                Assert.That(second.Order, Is.EqualTo(new[] { "release", "held" }));
                Assert.That(Tracking(rig, 2), Is.EqualTo(new[] { true, false }), "the search stops at the first zone scoring 0");

                touches.Clear();
                touches.Add(TouchIn(2, TouchPhase.Canceled, 5f, 5f));
                int processed = first.Processed.Count;
                Call(rig, "Update");
                Assert.That(Tracking(rig, 2), Is.EqualTo(new[] { false, false }), "a Canceled touch is dropped");
                Assert.That(first.Processed.Count, Is.EqualTo(processed), "and not updated");
                Assert.That(first.Order.Skip(first.Order.Count - 2).ToArray(), Is.EqualTo(new[] { "release", "held" }));
            });
        }

        // The first valid zone is taken even when its score is NaN: nothing is above or below it, and it does not
        // stop the search, so the later zone with a real score does not replace it (a version comparing with
        // "below the best" from the largest float would pick the later zone).
        [Test]
        public void AZoneWithANotANumberScoreIsKeptOverALaterZone()
        {
            WithTouches((rig, touches) =>
            {
                rig.Zones[0].ScoreOverride = float.NaN;
                rig.Zones[1].ScoreOverride = 3f;
                touches.Add(TouchIn(1, TouchPhase.Began, 10f, 10f));
                Call(rig, "Update");
                Assert.That(Tracking(rig, 1), Is.EqualTo(new[] { true, false }));
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
