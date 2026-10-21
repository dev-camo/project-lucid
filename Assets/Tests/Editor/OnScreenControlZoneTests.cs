using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using ProjectLucid.Tests.OnScreenControlsShared;
using UnityEngine;
using static ProjectLucid.Tests.OnScreenControlTestServices;

namespace ProjectLucid.Tests
{
    // OnScreenControlZone (02000973) against its 1.10.1 x86-64 bodies (0x68F1D0..0x691100). The zone is a probe on an
    // owned inactive object, with the real SaveManager, DataManager and CharacterManager of OnScreenControlTestServices
    // and an InputSystem holding an owned ControlMapping. A zone registered as a button provider would send its inputs
    // into the shared ControlMapping tables, so every case that supplies input first calls Shutdown (which
    // unregisters it) or never registers. Not covered: touches from the player loop.
    public sealed class OnScreenControlZoneTests
    {
        // The Editor raises Reset when it creates a ScriptableObject, which the probe counts, so a new modifier starts
        // counting the zone's own resets from zero.
        private static OnScreenControlZoneModifierProbe NewModifier(Services services)
        {
            var modifier = services.Owned.Track(ScriptableObject.CreateInstance<OnScreenControlZoneModifierProbe>());
            modifier.Resets = 0;
            return modifier;
        }

        private static readonly GameInput Tap = GameInput.Tap;

        private static string Row(string kind, GameInput input, float value) => ButtonCallbackLog.Row(kind, 0, input, value, InputType.Touch);

        // The saved settings the zone reads the stick sensitivity from (loaded by the save flow in the original).
        private static SaveDataSettings WithSettings(Services services)
        {
            var settings = new SaveDataSettings();
            ModernUITestSupport.Set(services.Save, "m_saveSettings", settings);
            return settings;
        }

        private static void WithZone(bool withMapping, Action<Services, ControlMapping, OnScreenControlZoneProbe> body) => With(services =>
        {
            ControlMapping mapping = withMapping ? services.Owned.Track(ScriptableObject.CreateInstance<ControlMapping>()) : null;
            var inputSystem = services.Owned.Host("Lucid owned input system", false).AddComponent<InputSystem>();
            ModernUITestSupport.Set(inputSystem, "m_controlMapping", mapping); // serialized in the original
            services.Owned.Register(inputSystem);
            body(services, mapping, services.Owned.Host("Lucid owned control zone", false).AddComponent<OnScreenControlZoneProbe>());
        });

        private static Touch TouchAt(int fingerId, float x, float y) =>
            new Touch { fingerId = fingerId, position = new Vector2(x, y), phase = TouchPhase.Moved };

        private static List<IInputButton> Providers(ControlMapping mapping) =>
            (List<IInputButton>)typeof(ControlMapping).GetField("m_buttonProviders", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(mapping);

        // The constructor 0x6906B0 stores the 16 bytes at 0x29A77F0 over the four axes: B30DF523, BC61485B, 128115E5,
        // D5958270 (left, right, up, down). The press input is not written, and the absolute flag is off.
        [Test]
        public void TheFourAxesDefaultToLeftRightUpAndDown()
        {
            WithZone(true, (services, mapping, zone) =>
            {
                Assert.That((int)ModernUITestSupport.Get(zone, "m_axisLeft"), Is.EqualTo(unchecked((int)0xB30DF523)));
                Assert.That((int)ModernUITestSupport.Get(zone, "m_axisRight"), Is.EqualTo(unchecked((int)0xBC61485B)));
                Assert.That((int)ModernUITestSupport.Get(zone, "m_axisUp"), Is.EqualTo(0x128115E5));
                Assert.That((int)ModernUITestSupport.Get(zone, "m_axisDown"), Is.EqualTo(unchecked((int)0xD5958270)));
                Assert.That((int)ModernUITestSupport.Get(zone, "m_onPress"), Is.EqualTo(0));
                Assert.That((bool)ModernUITestSupport.Get(zone, "m_provideAbsoluteInputValues"), Is.False);
                Assert.That(zone.ShowOnInputTypes, Is.Empty);
            });
        }

        // SupplyHeldGameInput 0x690D40: x picks right (x > 0, 0x690D83 seta) or left, y picks up (y > 0) or down; a
        // NaN is not above zero, so it takes left and down. Each axis reports a held input with the touch type
        // (0xFE1D10BD) at joystick 0, the value being the axis itself or, with the absolute flag, its magnitude.
        [Test]
        public void EachAxisHoldsTheInputOfItsSignWithTheValueOrItsMagnitude()
        {
            WithZone(true, (services, mapping, zone) =>
            {
                WithSettings(services);
                var log = new ButtonCallbackLog();
                log.Attach(zone);
                foreach ((float x, float y, GameInput horizontal, GameInput vertical) in new[]
                {
                    (3f, -2f, GameInput.Right, GameInput.Down), (-3f, 2f, GameInput.Left, GameInput.Up), (0f, 0f, GameInput.Left, GameInput.Down),
                    (float.NaN, float.NaN, GameInput.Left, GameInput.Down), (3f, 2f, GameInput.Right, GameInput.Up),
                })
                {
                    log.Calls.Clear();
                    zone.SupplyNow(new Vector2(x, y));
                    Assert.That(log.Calls, Is.EqualTo(new[] { Row("held", horizontal, x), Row("held", vertical, y) }), x + "," + y);
                }

                ModernUITestSupport.Set(zone, "m_provideAbsoluteInputValues", true); // serialized in the original
                foreach ((float x, float y, GameInput horizontal, GameInput vertical) in new[]
                {
                    (-3f, -2f, GameInput.Left, GameInput.Down), (3f, 2f, GameInput.Right, GameInput.Up),
                })
                {
                    log.Calls.Clear();
                    zone.SupplyNow(new Vector2(x, y));
                    Assert.That(log.Calls, Is.EqualTo(new[] { Row("held", horizontal, Math.Abs(x)), Row("held", vertical, Math.Abs(y)) }), "absolute " + x + "," + y);
                }
            });
        }

        // OnPressValueValid 0x690CA0 is true for any press input other than zero and None (0xB6FA1284). ProcessTouch
        // 0x68F850 presses it once with the first touch, before the movement is supplied; OnTouchRelease 0x690290
        // supplies the release of the movement, then releases the press, both with value 1 and the touch type.
        [Test]
        public void ThePressInputIsPressedWithTheFirstTouchAndReleasedAfterTheLastMovement()
        {
            foreach ((GameInput press, bool used) in new[] { (Tap, true), (GameInput.None, false), ((GameInput)0, false) })
            {
                WithZone(true, (services, mapping, zone) =>
                {
                    WithSettings(services);
                    ModernUITestSupport.Set(zone, "m_restrictToSingleTouch", true); // serialized in the original
                    ModernUITestSupport.Set(zone, "m_onPress", press); // serialized in the original
                    var log = new ButtonCallbackLog();
                    log.Attach(zone);

                    zone.StartTrackingTouch(TouchAt(1, 30f, -40f));
                    zone.SupplyTouchData();
                    var expected = new List<string>();
                    if (used) expected.Add(Row("down", press, 1f));
                    expected.Add(Row("held", GameInput.Right, 30f));
                    expected.Add(Row("held", GameInput.Down, -40f));
                    Assert.That(log.Calls, Is.EqualTo(expected), "first touch, press " + (int)press);

                    log.Calls.Clear();
                    zone.UpdateTouch(TouchAt(1, 35f, -50f));
                    zone.SupplyTouchData();
                    Assert.That(log.Calls, Is.EqualTo(new[] { Row("held", GameInput.Right, 5f), Row("held", GameInput.Down, -10f) }), "the press is only for the first touch");

                    log.Calls.Clear();
                    zone.StopTrackingTouch(TouchAt(1, 0f, 0f));
                    zone.SupplyTouchData();
                    expected.Clear();
                    expected.Add(Row("held", GameInput.Left, 0f));
                    expected.Add(Row("held", GameInput.Down, 0f));
                    if (used) expected.Add(Row("up", press, 1f));
                    Assert.That(log.Calls, Is.EqualTo(expected), "release, press " + (int)press);
                });
            }
        }

        // Initialise 0x68F1D0 registers the zone with the mapping of the registered InputSystem (the touch area also
        // starts empty); Shutdown 0x690970 unregisters it again. Without a mapping neither does anything else.
        [Test]
        public void InitialiseRegistersTheZoneAsAButtonProviderAndShutdownWithdrawsIt()
        {
            WithZone(true, (services, mapping, zone) =>
            {
                var constants = services.Owned.Track(ScriptableObject.CreateInstance<GlobalConstantDefinition_Input>());
                ProcessManager.GetSystem<DataManager>().GlobalConstantDefinitions[GlobalConstantType.Input] = constants;
                zone.StartTrackingTouch(TouchAt(1, 10f, 10f));

                zone.Initialise();
                Assert.That(Providers(mapping), Is.EqualTo(new IInputButton[] { zone }));
                Assert.That(zone.PointerIsDown, Is.False, "the base Initialise clears the touches");

                zone.StartTrackingTouch(TouchAt(1, 10f, 10f));
                zone.Shutdown(false);
                Assert.That(Providers(mapping), Is.Empty);
                Assert.That(zone.PointerIsDown, Is.False, "the base Shutdown clears the touches");
            });

            WithZone(false, (services, mapping, zone) =>
            {
                var constants = services.Owned.Track(ScriptableObject.CreateInstance<GlobalConstantDefinition_Input>());
                ProcessManager.GetSystem<DataManager>().GlobalConstantDefinitions[GlobalConstantType.Input] = constants;
                Assert.DoesNotThrow(() => zone.Initialise());
                Assert.DoesNotThrow(() => zone.Shutdown(false));
            });
        }

        // Initialise takes the modifier table of GlobalConstantDefinition_Input (+0x20). ApplyModifiers 0x690E00 looks up
        // the saved control stick sensitivity (SaveManager +0x90, +0x38), feeds the movement to each of its modifiers in
        // order through Modify(Vector3, deltaTime) with z 0 (slot 6), and keeps x and y. A sensitivity with no entry
        // leaves the movement alone; ResetAxisModifiers 0x690AF0 (the release) resets the modifiers of that sensitivity.
        [Test]
        public void TheModifiersOfTheSavedSensitivityShapeTheMovementAndResetOnRelease()
        {
            WithZone(true, (services, mapping, zone) =>
            {
                SaveDataSettings settings = WithSettings(services);
                var constants = services.Owned.Track(ScriptableObject.CreateInstance<GlobalConstantDefinition_Input>());
                var medium = NewModifier(services);
                var highFirst = NewModifier(services);
                var highSecond = NewModifier(services);
                medium.Scale = new Vector2(2f, 3f);
                highFirst.Scale = new Vector2(10f, 100f);
                highSecond.Scale = new Vector2(2f, 2f);
                constants.CompositeMovementModifierLookups[InputModifierType.Medium] = new List<InputModifier> { medium };
                constants.CompositeMovementModifierLookups[InputModifierType.High] = new List<InputModifier> { highFirst, highSecond };
                ProcessManager.GetSystem<DataManager>().GlobalConstantDefinitions[GlobalConstantType.Input] = constants;
                zone.Initialise();
                zone.Shutdown(false); // withdraws the provider; the table taken by Initialise stays

                Assert.That(settings.ControlStickSensitivity, Is.EqualTo(InputModifierType.Medium));
                Assert.That(zone.ApplyNow(new Vector2(4f, -5f)), Is.EqualTo(new Vector2(8f, -15f)));
                Assert.That(medium.Received, Is.EqualTo(new[] { new Vector3(4f, -5f, 0f) }));

                settings.ControlStickSensitivity = InputModifierType.High;
                Assert.That(zone.ApplyNow(new Vector2(1f, 1f)), Is.EqualTo(new Vector2(20f, 200f)), "both modifiers, in order");
                Assert.That(highSecond.Received, Is.EqualTo(new[] { new Vector3(10f, 100f, 0f) }), "the second one is given the first one's answer");

                settings.ControlStickSensitivity = InputModifierType.Low;
                Assert.That(zone.ApplyNow(new Vector2(1f, 1f)), Is.EqualTo(new Vector2(1f, 1f)), "no entry for this sensitivity");

                settings.ControlStickSensitivity = InputModifierType.High;
                ModernUITestSupport.Set(zone, "m_restrictToSingleTouch", true); // serialized in the original
                zone.StartTrackingTouch(TouchAt(1, 5f, 5f));
                zone.SupplyTouchData();
                zone.StopTrackingTouch(TouchAt(1, 0f, 0f));
                zone.SupplyTouchData();
                Assert.That((highFirst.Resets, highSecond.Resets, medium.Resets), Is.EqualTo((1, 1, 0)), "only the current sensitivity is reset");
            });
        }

        // The pointer is only reset on release when it was down (0x6902DE).
        [Test]
        public void ReleasingAZoneThatWasNeverTouchedResetsNothing()
        {
            WithZone(true, (services, mapping, zone) =>
            {
                WithSettings(services);
                var constants = services.Owned.Track(ScriptableObject.CreateInstance<GlobalConstantDefinition_Input>());
                var modifier = NewModifier(services);
                constants.CompositeMovementModifierLookups[InputModifierType.Medium] = new List<InputModifier> { modifier };
                ProcessManager.GetSystem<DataManager>().GlobalConstantDefinitions[GlobalConstantType.Input] = constants;
                zone.Initialise();
                zone.Shutdown(false);

                var log = new ButtonCallbackLog();
                log.Attach(zone);
                zone.ReleaseNow();
                Assert.That(modifier.Resets, Is.EqualTo(0));
                Assert.That(log.Calls, Is.EqualTo(new[] { Row("held", GameInput.Left, 0f), Row("held", GameInput.Down, 0f) }));
            });
        }
    }
}
