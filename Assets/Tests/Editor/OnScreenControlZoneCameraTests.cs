using System;
using System.Collections;
using System.Reflection;
using System.Text.RegularExpressions;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static ProjectLucid.Tests.OnScreenControlTestServices;

namespace ProjectLucid.Tests
{
    // OnScreenControlZoneCamera (02000974) against its 1.10.1 x86-64 bodies (0x691200..0x691850). The zone is a real
    // component on an owned inactive object, with the real SaveManager, DataManager and CharacterManager of
    // OnScreenControlTestServices and an InputSystem without a mapping, so the zone never registers as a button
    // provider. The touch flow of the base zone drives the hooks (a multi-touch zone: the first pass of a touch has no
    // movement). Screen.width and Screen.height are read from the engine because the scaling depends on them. Not
    // covered: the skipping of a double tap input while a listed ability is in use (needs a current character with an
    // ability in use), and the one-frame press the started coroutine supplies (an inactive object refuses coroutines).
    public sealed class OnScreenControlZoneCameraTests
    {
        private static readonly Regex CoroutineRefused = new Regex("Coroutine couldn't be started");

        private static Touch TouchAt(int fingerId, float x, float y) =>
            new Touch { fingerId = fingerId, position = new Vector2(x, y), phase = TouchPhase.Moved };

        private static string Row(string kind, GameInput input, float value) => ButtonCallbackLog.Row(kind, 0, input, value, InputType.Touch);

        private static void WithZone(Action<Services, OnScreenControlZoneCamera> body) => With(services =>
        {
            var settings = new SaveDataSettings();
            ModernUITestSupport.Set(services.Save, "m_saveSettings", settings); // loaded by the save flow in the original
            var inputSystem = services.Owned.Host("Lucid owned input system", false).AddComponent<InputSystem>();
            ModernUITestSupport.Set(inputSystem, "m_controlMapping", null); // serialized in the original
            services.Owned.Register(inputSystem);
            body(services, services.Owned.Host("Lucid owned camera zone", false).AddComponent<OnScreenControlZoneCamera>());
        });

        private static T Field<T>(object zone, string name) => (T)ModernUITestSupport.Get(zone, name);

        private static void CacheCancelMagnitude(OnScreenControlZoneCamera zone, float magnitude)
        {
            ModernUITestSupport.Set(zone, "m_minimumCancelDoubleTapInputMagnitude", magnitude); // serialized in the original
            ModernUITestSupport.Call(zone, "UpdateCachedValues");
        }

        // A tap: the touch goes down for one pass and up again.
        private static void Tap(OnScreenControlZoneCamera zone)
        {
            zone.StartTrackingTouch(TouchAt(1, 40f, 60f));
            zone.SupplyTouchData();
            zone.StopTrackingTouch(TouchAt(1, 0f, 0f));
            zone.SupplyTouchData();
        }

        // The constructor 0x691790 stores the 8 bytes at 0x29A7B00 (75 and 125) over the sensitivities, 0x3E4CCCCD (0.2)
        // as the double tap time and turns the snap on; the cancel magnitude and the debug view are left alone.
        [Test]
        public void TheConstructorSetsTheSensitivitiesTheDoubleTapTimeAndAnEnabledSnap()
        {
            WithZone((services, zone) =>
            {
                Assert.That(Field<float>(zone, "m_xSensitivity"), Is.EqualTo(75f));
                Assert.That(Field<float>(zone, "m_ySensitivity"), Is.EqualTo(125f));
                Assert.That(Field<float>(zone, "m_cameraSnapDoubleTapTimeSeconds"), Is.EqualTo(0.2f));
                Assert.That(Field<bool>(zone, "m_cameraSnapEnabled"), Is.True);
                Assert.That(Field<float>(zone, "m_minimumCancelDoubleTapInputMagnitude"), Is.EqualTo(0f));
                Assert.That(Field<IList>(zone, "m_doubleTapInputs"), Is.Empty);
                Assert.That(Field<GameObject>(zone, "m_debugTouchZoneView"), Is.Null);
            });
        }

        // Initialise 0x691200 (after the base Initialise) and OnValidate 0x691750 cache the square of the cancel
        // magnitude (+0xFC squared into +0x118).
        [Test]
        public void InitialiseAndValidateCacheTheSquareOfTheCancelMagnitude()
        {
            WithZone((services, zone) =>
            {
                var constants = services.Owned.Track(ScriptableObject.CreateInstance<GlobalConstantDefinition_Input>());
                ProcessManager.GetSystem<DataManager>().GlobalConstantDefinitions[GlobalConstantType.Input] = constants;
                ModernUITestSupport.Set(zone, "m_minimumCancelDoubleTapInputMagnitude", 3f); // serialized in the original
                zone.Initialise();
                Assert.That(Field<float>(zone, "m_minimumCancelDoubleTapInputMagnitudeSqr"), Is.EqualTo(9f));

                ModernUITestSupport.Set(zone, "m_minimumCancelDoubleTapInputMagnitude", 4f);
                ModernUITestSupport.Call(zone, "OnValidate");
                Assert.That(Field<float>(zone, "m_minimumCancelDoubleTapInputMagnitudeSqr"), Is.EqualTo(16f));
                zone.Shutdown(false);
            });
        }

        // OnInput 0x691270 stores the movement, scales x by the x sensitivity over the screen height and y by the y
        // sensitivity over the screen width, and adds the squared length of the result to the total. The first touch of a
        // single-touch zone moves from the origin, so the pass supplies the scaled touch position.
        [Test]
        public void TheCameraInputIsTheMovementScaledByTheSensitivityOverTheScreenSize()
        {
            WithZone((services, zone) =>
            {
                ModernUITestSupport.Set(zone, "m_restrictToSingleTouch", true); // serialized in the original
                var log = new ButtonCallbackLog();
                log.Attach(zone);
                // Equal sizes would hide a swapped height/width pairing.
                Assume.That(Screen.width, Is.Not.EqualTo(Screen.height));
                float xScale = 75f / (float)Screen.height;
                float yScale = 125f / (float)Screen.width;
                float x = xScale * 30f;
                float y = yScale * 40f;
                float firstSqr = x * x + y * y;

                zone.StartTrackingTouch(TouchAt(1, 30f, 40f));
                zone.SupplyTouchData();
                Assert.That(log.Calls, Is.EqualTo(new[] { Row("held", GameInput.Right, x), Row("held", GameInput.Up, y) }));
                Assert.That(Field<Vector2>(zone, "m_controllerInput"), Is.EqualTo(new Vector2(x, y)));
                Assert.That(Field<float>(zone, "m_totalMovementAmountSqr"), Is.EqualTo(firstSqr));

                log.Calls.Clear();
                zone.UpdateTouch(TouchAt(1, 10f, 80f));
                zone.SupplyTouchData();
                float x2 = xScale * -20f;
                float y2 = yScale * 40f;
                float secondSqr = x2 * x2 + y2 * y2;
                float expectedTotal = secondSqr + firstSqr;
                Assert.That(log.Calls, Is.EqualTo(new[] { Row("held", GameInput.Left, x2), Row("held", GameInput.Up, y2) }));
                Assert.That(Field<float>(zone, "m_totalMovementAmountSqr"), Is.EqualTo(expectedTotal), "the total adds up");
            });
        }

        // OnTouchRelease 0x6913D0: a pass with a cancel magnitude of 0 (the default) never counts a tap, because the
        // total (0) is not below the cached square (0): the comparison at 0x691455 jumps on above-or-equal.
        [Test]
        public void ADefaultZoneNeverCountsATap()
        {
            WithZone((services, zone) =>
            {
                Tap(zone);
                Tap(zone);
                Assert.That(Field<int>(zone, "m_snapTapCount"), Is.EqualTo(0));
            });
        }

        // A release without movement counts a tap while the total stays below the cached square; the second tap reaches
        // DoubleTapCount (2) and resets the count and the timer (0x69156B, 0x691587). A release after enough movement
        // resets both at once.
        [Test]
        public void ATapCountsTowardsADoubleTapAndMovementCancelsIt()
        {
            WithZone((services, zone) =>
            {
                CacheCancelMagnitude(zone, 0.5f);

                Tap(zone);
                Assert.That(Field<int>(zone, "m_snapTapCount"), Is.EqualTo(1));
                Tap(zone);
                Assert.That((Field<int>(zone, "m_snapTapCount"), Field<float>(zone, "m_doubleTapTimerSeconds")), Is.EqualTo((0, 0f)), "double tap");

                Tap(zone);
                Assert.That(Field<int>(zone, "m_snapTapCount"), Is.EqualTo(1));
                zone.StartTrackingTouch(TouchAt(1, 10f, 10f));
                zone.SupplyTouchData();
                zone.UpdateTouch(TouchAt(1, 200f, 10f));
                zone.SupplyTouchData();
                Assert.That(Field<float>(zone, "m_totalMovementAmountSqr"), Is.GreaterThan(0.25f), "enough movement to cancel");
                zone.StopTrackingTouch(TouchAt(1, 0f, 0f));
                zone.SupplyTouchData();
                Assert.That(Field<int>(zone, "m_snapTapCount"), Is.EqualTo(0), "movement cancels the taps");
                Assert.That(Field<float>(zone, "m_totalMovementAmountSqr"), Is.EqualTo(0f), "the total is reset by every release");
            });
        }

        // The tap bookkeeping runs only with the snap enabled and a touch that was down (0x691433, 0x691440); the base
        // release (the empty movement) is supplied either way.
        [Test]
        public void ADisabledSnapLeavesTheCountAndTheTotalAlone()
        {
            WithZone((services, zone) =>
            {
                CacheCancelMagnitude(zone, 0.5f);
                var log = new ButtonCallbackLog();
                log.Attach(zone);
                zone.SetCameraSnapEnabled(false);
                Assert.That(Field<bool>(zone, "m_cameraSnapEnabled"), Is.False);

                ModernUITestSupport.Set(zone, "m_restrictToSingleTouch", true); // serialized in the original
                zone.StartTrackingTouch(TouchAt(1, 10f, 10f));
                zone.SupplyTouchData();
                float total = Field<float>(zone, "m_totalMovementAmountSqr");
                Assert.That(total, Is.GreaterThan(0f));
                log.Calls.Clear();
                zone.StopTrackingTouch(TouchAt(1, 0f, 0f));
                zone.SupplyTouchData();
                Assert.That(Field<int>(zone, "m_snapTapCount"), Is.EqualTo(0));
                Assert.That(Field<float>(zone, "m_totalMovementAmountSqr"), Is.EqualTo(total), "not reset");
                Assert.That(log.Calls, Is.EqualTo(new[] { Row("held", GameInput.Left, 0f), Row("held", GameInput.Down, 0f) }));
            });
        }

        // The double tap supplies every listed input through SupplyGameInputSequence (a coroutine on the zone, which an
        // inactive object refuses with the engine error), so the errors count the inputs started.
        [Test]
        public void ADoubleTapStartsEveryListedInput()
        {
            WithZone((services, zone) =>
            {
                CacheCancelMagnitude(zone, 0.5f);
                Type entry = typeof(OnScreenControlZoneCamera).GetNestedType("GameInputCharacter", BindingFlags.NonPublic);
                var inputs = Field<IList>(zone, "m_doubleTapInputs");
                inputs.Add(Activator.CreateInstance(entry, true));
                inputs.Add(Activator.CreateInstance(entry, true));

                Tap(zone);
                Assert.That(Field<int>(zone, "m_snapTapCount"), Is.EqualTo(1), "nothing is started by the first tap");
                LogAssert.Expect(LogType.Error, CoroutineRefused);
                LogAssert.Expect(LogType.Error, CoroutineRefused);
                Tap(zone);
                Assert.That(Field<int>(zone, "m_snapTapCount"), Is.EqualTo(0));
            });
        }

        // Update 0x6916F0: with a tap counted the timer adds the unscaled frame time and the count and timer reset once the
        // timer reaches the double tap time (the comparison at 0x691723 skips only below it); without a tap the timer is 0.
        [Test]
        public void TheTapCountExpiresOnceTheDoubleTapTimeHasPassed()
        {
            WithZone((services, zone) =>
            {
                CacheCancelMagnitude(zone, 0.5f);
                ModernUITestSupport.Set(zone, "m_cameraSnapDoubleTapTimeSeconds", 1e9f); // serialized in the original
                Tap(zone);
                ModernUITestSupport.Call(zone, "Update");
                Assert.That(Field<int>(zone, "m_snapTapCount"), Is.EqualTo(1), "well inside the time");
                Assert.That(Field<float>(zone, "m_doubleTapTimerSeconds"), Is.EqualTo(Time.unscaledDeltaTime));

                ModernUITestSupport.Set(zone, "m_cameraSnapDoubleTapTimeSeconds", 0f);
                ModernUITestSupport.Call(zone, "Update");
                Assert.That((Field<int>(zone, "m_snapTapCount"), Field<float>(zone, "m_doubleTapTimerSeconds")), Is.EqualTo((0, 0f)), "a zero time has always passed");

                ModernUITestSupport.Call(zone, "Update");
                Assert.That(Field<float>(zone, "m_doubleTapTimerSeconds"), Is.EqualTo(0f), "no tap, no timer");
            });
        }

        // Update 0x6916F0 adds the unscaled frame time to the timer already running (0x691716 addss of the old timer),
        // whatever the engine reports for the frame.
        [Test]
        public void TheDoubleTapTimerAccumulatesTheUnscaledFrameTime()
        {
            WithZone((services, zone) =>
            {
                CacheCancelMagnitude(zone, 0.5f);
                ModernUITestSupport.Set(zone, "m_cameraSnapDoubleTapTimeSeconds", 1e9f); // serialized in the original
                Tap(zone);
                ModernUITestSupport.Set(zone, "m_doubleTapTimerSeconds", 0.5f); // the timer is only written by Update and the resets

                ModernUITestSupport.Call(zone, "Update");

                Assert.That(Field<int>(zone, "m_snapTapCount"), Is.EqualTo(1));
                Assert.That(Field<float>(zone, "m_doubleTapTimerSeconds"), Is.EqualTo(0.5f + Time.unscaledDeltaTime));
            });
        }

        // The expiry compares with "greater or equal" (0x691723 ucomiss/jb skips only below the time): 1e9 plus any
        // frame time below 32 s rounds back to 1e9 in single precision, so the timer equals the time whatever the frame.
        [Test]
        public void TheTapCountExpiresWhenTheTimerEqualsTheDoubleTapTime()
        {
            WithZone((services, zone) =>
            {
                CacheCancelMagnitude(zone, 0.5f);
                ModernUITestSupport.Set(zone, "m_cameraSnapDoubleTapTimeSeconds", 1e9f); // serialized in the original
                Tap(zone);
                ModernUITestSupport.Set(zone, "m_doubleTapTimerSeconds", 1e9f); // the timer is only written by Update and the resets

                ModernUITestSupport.Call(zone, "Update");

                Assert.That((Field<int>(zone, "m_snapTapCount"), Field<float>(zone, "m_doubleTapTimerSeconds")), Is.EqualTo((0, 0f)));
            });
        }

        // ResetSnapValues 0x691620 clears the count and the timer with one 8-byte store.
        [Test]
        public void ResettingTheSnapValuesClearsTheCountAndTheTimer()
        {
            WithZone((services, zone) =>
            {
                CacheCancelMagnitude(zone, 0.5f);
                ModernUITestSupport.Set(zone, "m_cameraSnapDoubleTapTimeSeconds", 1e9f); // serialized in the original
                Tap(zone);
                ModernUITestSupport.Call(zone, "ResetSnapValues");
                Assert.That((Field<int>(zone, "m_snapTapCount"), Field<float>(zone, "m_doubleTapTimerSeconds")), Is.EqualTo((0, 0f)));
            });
        }

        // GetDebugTouchZoneViewText 0x691330 reads "Touch Zone View: " plus On or Off; ToggleDebugTouchZoneView 0x6913A0
        // flips the flag and shows or hides the serialized overlay object accordingly.
        [Test]
        public void TheDebugOverlayTogglesTheViewAndItsText()
        {
            WithZone((services, zone) =>
            {
                GameObject view = services.Owned.Host("Lucid owned touch zone view", false);
                ModernUITestSupport.Set(zone, "m_debugTouchZoneView", view); // serialized in the original
                Assert.That(ModernUITestSupport.Call(zone, "GetDebugTouchZoneViewText"), Is.EqualTo("Touch Zone View: Off"));

                ModernUITestSupport.Call(zone, "ToggleDebugTouchZoneView");
                Assert.That(view.activeSelf, Is.True);
                Assert.That(ModernUITestSupport.Call(zone, "GetDebugTouchZoneViewText"), Is.EqualTo("Touch Zone View: On"));

                ModernUITestSupport.Call(zone, "ToggleDebugTouchZoneView");
                Assert.That(view.activeSelf, Is.False);
                Assert.That(ModernUITestSupport.Call(zone, "GetDebugTouchZoneViewText"), Is.EqualTo("Touch Zone View: Off"));
            });
        }
    }
}
