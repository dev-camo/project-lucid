using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static ProjectLucid.Tests.OnScreenControlTestServices;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // OnScreenControlStickMovement (02000971) against its 1.10.1 x86-64 bodies (0x68F030..0x6908A0, jump table of
    // SetState at 0x68F750). The stick is a real component on an owned inactive object with real RectTransforms for its
    // container, origin, stick top and three outer graphics, the real SaveManager, DataManager, CharacterManager,
    // an InputSystem without a mapping, a VibrationManager and a CinemachineCameraManager registered before the stick is
    // built (its field initialisers cache those references). The container is 200 by 100 at scale 1, so the stick extent
    // is 100; the origin transform sits at (1000, 500). Touches are driven through the base touch flow. The haptic events
    // are observed through the VibrationManager: with its debug flag set it starts one coroutine per matching
    // definition, which an inactive object refuses with the engine error, so the errors count the definitions hit.
    // Not covered: the visibility rule of the container against the real last input type of ControlMapping (its static
    // is read, not set), and the one-frame press of a started haptic.
    public sealed class OnScreenControlStickMovementTests
    {
        private static readonly Regex CoroutineRefused = new Regex("Coroutine couldn't be started");

        private static Touch TouchAt(int fingerId, float x, float y) =>
            new Touch { fingerId = fingerId, position = new Vector2(x, y), phase = TouchPhase.Moved };

        private static string Row(GameInput input, float value) => ButtonCallbackLog.Row("held", 0, input, value, InputType.Touch);

        private sealed class StickRig
        {
            internal Services Services;
            internal SaveDataSettings Settings;
            internal VibrationManager Vibration;
            internal CameraManagerClientSupport.Rig Camera;
            internal OnScreenControlStickMovement Stick;
            internal RectTransform Origin, Container, Top, Mini, Solid, Rotating;
            internal ButtonCallbackLog Log = new ButtonCallbackLog();

            // The graphics shown: mini outer, full solid outer, rotating outer, stick top.
            internal (bool mini, bool solid, bool rotating, bool top) Graphics =>
                (Mini.gameObject.activeSelf, Solid.gameObject.activeSelf, Rotating.gameObject.activeSelf, Top.gameObject.activeSelf);

            internal string State => ModernUITestSupport.Get(Stick, "m_state").ToString();

            internal void Press(float x, float y)
            {
                Stick.StartTrackingTouch(TouchAt(1, x, y));
                Stick.SupplyTouchData();
            }

            internal void Move(float x, float y)
            {
                Stick.UpdateTouch(TouchAt(1, x, y));
                Stick.SupplyTouchData();
            }

            internal void Release()
            {
                Stick.StopTrackingTouch(TouchAt(1, 0f, 0f));
                Stick.SupplyTouchData();
            }
        }

        private static RectTransform Rect(Owned owned, string name) =>
            owned.Host(name, false).AddComponent<RectTransform>();

        private static void WithStick(Action<StickRig> body) => With(services =>
        {
            var rig = new StickRig { Services = services, Settings = new SaveDataSettings() };
            ModernUITestSupport.Set(services.Save, "m_saveSettings", rig.Settings); // loaded by the save flow in the original
            var inputSystem = services.Owned.Host("Lucid owned input system", false).AddComponent<InputSystem>();
            ModernUITestSupport.Set(inputSystem, "m_controlMapping", null); // serialized in the original
            services.Owned.Register(inputSystem);
            var constants = services.Owned.Track(ScriptableObject.CreateInstance<GlobalConstantDefinition_Input>());
            ProcessManager.GetSystem<DataManager>().GlobalConstantDefinitions[GlobalConstantType.Input] = constants;
            rig.Vibration = services.Owned.Host("Lucid owned vibration manager", false).AddComponent<VibrationManager>();
            services.Owned.Register(rig.Vibration);
            rig.Camera = CameraManagerClientSupport.NewRig(services.Owned, false, true);

            rig.Origin = Rect(services.Owned, "Lucid owned stick origin");
            rig.Origin.position = new Vector3(1000f, 500f, 0f);
            rig.Container = Rect(services.Owned, "Lucid owned stick container");
            rig.Container.sizeDelta = new Vector2(200f, 100f);
            rig.Container.position = new Vector3(50f, 60f, 0f);
            rig.Top = Rect(services.Owned, "Lucid owned stick top");
            rig.Mini = Rect(services.Owned, "Lucid owned stick mini outer");
            rig.Solid = Rect(services.Owned, "Lucid owned stick solid outer");
            rig.Rotating = Rect(services.Owned, "Lucid owned stick rotating outer");

            rig.Stick = services.Owned.Host("Lucid owned stick", false).AddComponent<OnScreenControlStickMovement>();
            // The authored references of the prefab.
            ModernUITestSupport.Set(rig.Stick, "m_originTransform", rig.Origin);
            ModernUITestSupport.Set(rig.Stick, "m_container", rig.Container);
            ModernUITestSupport.Set(rig.Stick, "m_stickTop", rig.Top);
            ModernUITestSupport.Set(rig.Stick, "m_miniOuter", rig.Mini);
            ModernUITestSupport.Set(rig.Stick, "m_fullSolidOuter", rig.Solid);
            ModernUITestSupport.Set(rig.Stick, "m_rotatingOuter", rig.Rotating);
            rig.Log.Attach(rig.Stick);
            body(rig);
        });

        private static T Field<T>(object target, string name) => (T)ModernUITestSupport.Get(target, name);

        private static void Near(Vector3 actual, float x, float y, float z, string message = null)
        {
            Assert.That(actual.x, Is.EqualTo(x).Within(0.001f), message + " x");
            Assert.That(actual.y, Is.EqualTo(y).Within(0.001f), message + " y");
            Assert.That(actual.z, Is.EqualTo(z).Within(0.001f), message + " z");
        }

        // The rotation about z by the given degrees, compared as rotations so that 0 and 360 agree.
        private static void AssertRotationAboutZ(Quaternion actual, float degrees, string message = null) =>
            Assert.That(Quaternion.Angle(actual, Quaternion.Euler(0f, 0f, degrees)), Is.LessThan(0.05f), message);

        private static void ExpectRefusedCoroutines(int count)
        {
            for (int i = 0; i < count; i++) LogAssert.Expect(LogType.Error, CoroutineRefused);
        }

        private static VibrationEventData Definition(StickRig rig, VibrationEvent vibrationEvent)
        {
            var data = rig.Services.Owned.Track(ScriptableObject.CreateInstance<VibrationEventData>());
            data.m_vibrationEvent = vibrationEvent;
            data.m_vibrationEvents = new VibrationEventData.Vibrations[0];
            return data;
        }

        // The debug flag makes the manager start definitions without a device (it is cleared by the player's Awake).
        private static void ArmHaptics(StickRig rig, params VibrationEventData[] definitions)
        {
            ModernUITestSupport.Set(rig.Vibration, "m_debugInEditor", true); // serialized in the original
            ModernUITestSupport.Set(rig.Vibration, "m_vibrationEventDefinitions", definitions); // serialized in the original
        }

        // The constructor 0x690590 stores Vector3.one (+0x120), 0x3D4CCCCD (0.05) as the tolerance and the 8 bytes at
        // 0x29A7B00 (75 and 125) as the secondary sensitivities.
        [Test]
        public void TheConstructorSetsTheDefaultScaleToleranceAndSecondarySensitivities()
        {
            WithStick(rig =>
            {
                Assert.That(Field<Vector3>(rig.Stick, "m_baseScaleMultiplier"), Is.EqualTo(Vector3.one));
                Assert.That(Field<float>(rig.Stick, "m_magnitudeTolerance"), Is.EqualTo(0.05f));
                Assert.That(Field<float>(rig.Stick, "m_secondaryXSensitivity"), Is.EqualTo(75f));
                Assert.That(Field<float>(rig.Stick, "m_secondaryYSensitivity"), Is.EqualTo(125f));
                Assert.That(rig.State, Is.EqualTo("HIDDEN"));
            });
        }

        // Initialise 0x68F030: the origin is taken from the origin transform (a fixed stick), the stick sits at its
        // held-centre state, the container is shown and the rotating and full solid outers are scaled by the base scale.
        [Test]
        public void InitialiseShowsTheContainerScalesTheOuterGraphicsAndSetsTheOrigin()
        {
            WithStick(rig =>
            {
                ModernUITestSupport.Set(rig.Stick, "m_baseScaleMultiplier", new Vector3(2f, 3f, 4f)); // serialized in the original
                Assert.That(rig.Container.gameObject.activeSelf, Is.False);

                rig.Stick.Initialise();

                Assert.That(rig.Container.gameObject.activeSelf, Is.True);
                Assert.That(rig.Rotating.localScale, Is.EqualTo(new Vector3(2f, 3f, 4f)));
                Assert.That(rig.Solid.localScale, Is.EqualTo(new Vector3(2f, 3f, 4f)));
                Assert.That(rig.Mini.localScale, Is.EqualTo(Vector3.one), "the mini outer is not scaled");
                Assert.That(Field<Vector2>(rig.Stick, "m_origin"), Is.EqualTo(new Vector2(1000f, 500f)));
                Assert.That(Field<Vector3>(rig.Stick, "m_originTransformCentrePrevious"), Is.EqualTo(new Vector3(1000f, 500f, 0f)));
                Assert.That(Field<Vector2>(rig.Stick, "m_visualDisplayAreaDimensions"), Is.EqualTo(new Vector2(200f, 100f)));
                Assert.That(rig.State, Is.EqualTo("HELD_CENTRE"));
                Near(rig.Container.position, 50f, 60f, 0f, "a fixed stick leaves its container where it is");
            });
        }

        // Initialise hands SetControllingCamera to the camera manager's secondary-movement event (the lambda
        // 0x6908A0 adds it) and OnDestroy 0x68F570 removes it again. A zone that is not restricted to a single
        // touch starts as the camera control (0x68F16D: the opposite of the base IsSingleTouch).
        [Test]
        public void TheStickFollowsTheSecondaryMovementOfTheCameraManagerUntilItIsDestroyed()
        {
            WithStick(rig =>
            {
                rig.Stick.Initialise();
                Assert.That(Field<bool>(rig.Stick, "m_isControllingCamera"), Is.True, "not restricted to a single touch");

                Action<bool> subscribed = Field<Action<bool>>(rig.Camera.Manager, "SecondaryMovementSubscribed");
                subscribed(false);
                Assert.That(Field<bool>(rig.Stick, "m_isControllingCamera"), Is.False);
                subscribed(true);
                Assert.That(Field<bool>(rig.Stick, "m_isControllingCamera"), Is.True);

                ModernUITestSupport.Call(rig.Stick, "OnDestroy");
                subscribed = Field<Action<bool>>(rig.Camera.Manager, "SecondaryMovementSubscribed");
                subscribed(false);
                Assert.That(Field<bool>(rig.Stick, "m_isControllingCamera"), Is.True, "the handler was removed");
            });
        }

        // A zone restricted to a single touch (a serialized flag of the base zone) does not start as the camera control.
        [Test]
        public void ASingleTouchStickDoesNotStartAsTheCameraControl()
        {
            WithStick(rig =>
            {
                ModernUITestSupport.Set(rig.Stick, "m_restrictToSingleTouch", true); // serialized in the original
                rig.Stick.Initialise();
                Assert.That(Field<bool>(rig.Stick, "m_isControllingCamera"), Is.False);
            });
        }

        // OnSaveDataLoaded 0x68F620 copies the saved dynamic stick flag (+0x35 of the settings) over the serialized one;
        // Initialise requests it before the origin is set, so a dynamic stick puts its container and stick top at the
        // origin transform's position (SetStickOrigin 0x68F4E2).
        [Test]
        public void TheDynamicFlagComesFromTheSaveDataAndAFlaggedStickMovesItsContainerToTheOrigin()
        {
            WithStick(rig =>
            {
                ModernUITestSupport.Set(rig.Stick, "m_dynamicPosition", true); // serialized in the original
                rig.Stick.Initialise();
                Assert.That(Field<bool>(rig.Stick, "m_dynamicPosition"), Is.False, "the saved flag wins");
                Near(rig.Container.position, 50f, 60f, 0f, "not moved");

                rig.Settings.DynamicMovementStick = true;
                ModernUITestSupport.Call(rig.Stick, "OnSaveDataLoaded", rig.Services.Save);
                Assert.That(Field<bool>(rig.Stick, "m_dynamicPosition"), Is.True);
                rig.Stick.TransformUpdated();
                Near(rig.Container.position, 1000f, 500f, 0f, "container");
                Near(rig.Top.position, 1000f, 500f, 0f, "stick top");
            });
        }

        // SetActive 0x68F520 shows or hides the container object.
        [Test]
        public void SetActiveShowsAndHidesTheContainer()
        {
            WithStick(rig =>
            {
                rig.Stick.SetActive(true);
                Assert.That(rig.Container.gameObject.activeSelf, Is.True);
                rig.Stick.SetActive(true);
                Assert.That(rig.Container.gameObject.activeSelf, Is.True);
                rig.Stick.SetActive(false);
                Assert.That(rig.Container.gameObject.activeSelf, Is.False);
            });
        }

        // SetStickOrigin 0x68F3D0 on the first touch of a press: a dynamic stick takes the touch point as its origin and
        // moves its container and stick top there; a fixed stick takes the origin transform's position and leaves them.
        [Test]
        public void TheFirstTouchOfAPressPlacesTheOriginAtTheTouchForADynamicStickAndOnTheTransformForAFixedOne()
        {
            WithStick(rig =>
            {
                ModernUITestSupport.Set(rig.Stick, "m_dynamicPosition", true); // set by the save data in the original
                rig.Press(1100f, 550f);
                Assert.That(Field<Vector2>(rig.Stick, "m_origin"), Is.EqualTo(new Vector2(1100f, 550f)));
                Near(rig.Container.position, 1100f, 550f, 0f, "container");
                Near(rig.Top.position, 1100f, 550f, 0f, "stick top");
                Assert.That(rig.State, Is.EqualTo("HELD_CENTRE"), "a touch on the origin leaves the stick at its centre");
            });

            WithStick(rig =>
            {
                rig.Press(1000f, 500f);
                Assert.That(Field<Vector2>(rig.Stick, "m_origin"), Is.EqualTo(new Vector2(1000f, 500f)));
                Near(rig.Container.position, 50f, 60f, 0f, "container");
            });
        }

        // The origin of a fixed stick is replaced only when the origin transform has moved since the last time
        // (the Vector3 inequality at 0x68F45D..0x68F463): after a dynamic press the previous position is the transform's,
        // so once the stick is fixed a later press keeps the origin of that press until the transform moves.
        [Test]
        public void AFixedStickKeepsAnEarlierOriginWhileItsOriginTransformStaysPut()
        {
            WithStick(rig =>
            {
                rig.Settings.DynamicMovementStick = true;
                ModernUITestSupport.Call(rig.Stick, "OnSaveDataLoaded", rig.Services.Save);
                rig.Press(77f, 88f);
                rig.Release();
                Assert.That(Field<Vector2>(rig.Stick, "m_origin"), Is.EqualTo(new Vector2(77f, 88f)));

                rig.Settings.DynamicMovementStick = false;
                ModernUITestSupport.Call(rig.Stick, "OnSaveDataLoaded", rig.Services.Save);
                rig.Press(300f, 300f);
                Assert.That(Field<Vector2>(rig.Stick, "m_origin"), Is.EqualTo(new Vector2(77f, 88f)), "the origin transform has not moved");
                rig.Release();

                rig.Origin.position = new Vector3(400f, 450f, 0f);
                rig.Press(300f, 300f);
                Assert.That(Field<Vector2>(rig.Stick, "m_origin"), Is.EqualTo(new Vector2(400f, 450f)), "it has moved");
            });
        }

        // The stick input 0x68F920: the direction of the touch from the origin times how far it is through half the
        // display width (100 here), through OnDrag 0x68FD30 which moves the stick top by the input times half the
        // display size, picks the state (fine control inside the tolerance of both ends, full tilt beyond) and turns the
        // rotating outer and stick top to the heading (the angle from up, positive when x < 0: RotateToHeading 0x68FEF0).
        // A movement below the tolerance (0.05) moves the stick top only.
        [Test]
        public void TheStickInputIsTheDirectionScaledByTheDistanceAndDrivesTheGraphics()
        {
            WithStick(rig =>
            {
                rig.Press(1060f, 580f);
                Assert.That(rig.Log.Calls, Is.EqualTo(new[] { Row(GameInput.Right, 0.6f), Row(GameInput.Up, 0.8f) }), "full extent");
                Near(rig.Top.position, 1060f, 540f, 0f, "stick top");
                Assert.That(rig.State, Is.EqualTo("FULL_TILT"));
                Assert.That(rig.Graphics, Is.EqualTo((false, false, true, false)));
                AssertRotationAboutZ(rig.Rotating.localRotation, -56.31f, "outer, -56.31 degrees");
                AssertRotationAboutZ(rig.Top.localRotation, -56.31f, "top");

                rig.Log.Calls.Clear();
                rig.Move(970f, 540f);
                Assert.That(rig.Log.Calls, Is.EqualTo(new[] { Row(GameInput.Left, -0.3f), Row(GameInput.Up, 0.4f) }), "half the extent");
                Near(rig.Top.position, 970f, 520f, 0f, "stick top");
                Assert.That(rig.State, Is.EqualTo("FINE_CONTROL"));
                Assert.That(rig.Graphics, Is.EqualTo((false, false, true, true)));
                AssertRotationAboutZ(rig.Rotating.localRotation, 56.31f, "+56.31 degrees");

                rig.Log.Calls.Clear();
                rig.Move(1001f, 500f);
                Assert.That(rig.Log.Calls, Is.EqualTo(new[] { Row(GameInput.Right, 0.01f), Row(GameInput.Down, 0f) }), "a hundredth of the extent");
                Near(rig.Top.position, 1001f, 500f, 0f, "stick top still follows");
                Assert.That(rig.State, Is.EqualTo("FINE_CONTROL"), "below the tolerance nothing else changes");
                AssertRotationAboutZ(rig.Rotating.localRotation, 56.31f, "unchanged");
            });
        }

        // OnDrag 0x68FD30 against the tolerance (0.05): a magnitude below it only moves the stick top; at it, the stick
        // is in fine control until the magnitude reaches 1 - 0.05 (0.95 in single precision, inclusive), where it is at
        // full tilt. Each change into or out of full tilt buzzes its haptic event (reaching: DefaultSelection with one
        // definition, exiting: LongPressSelection with two); a change between the other states makes none.
        [Test]
        public void TheStateAndTheHapticsFollowTheMagnitudeAgainstTheToleranceAtBothEnds()
        {
            WithStick(rig =>
            {
                ArmHaptics(rig, Definition(rig, VibrationEvent.DefaultSelection), Definition(rig, VibrationEvent.LongPressSelection),
                    Definition(rig, VibrationEvent.LongPressSelection));
                ModernUITestSupport.Set(rig.Stick, "m_hapticsReachingFullTilt", VibrationEvent.DefaultSelection); // serialized in the original
                ModernUITestSupport.Set(rig.Stick, "m_hapticsExitingFullTilt", VibrationEvent.LongPressSelection); // serialized in the original
                rig.Stick.Initialise();
                Assert.That(rig.State, Is.EqualTo("HELD_CENTRE"));
                Vector3 movement = new Vector3(0f, 10f, 0f);

                rig.Stick.OnDrag(movement, 0.04f);
                Assert.That(rig.State, Is.EqualTo("HELD_CENTRE"), "below the tolerance");

                ExpectRefusedCoroutines(1);
                rig.Stick.OnDrag(movement, 1f);
                Assert.That(rig.State, Is.EqualTo("FULL_TILT"), "reaching full tilt buzzes the reaching event once");
                Assert.That(rig.Graphics, Is.EqualTo((false, false, true, false)));

                rig.Stick.OnDrag(movement, 1f);
                Assert.That(rig.State, Is.EqualTo("FULL_TILT"));

                float justBelow = BitConverter.Int32BitsToSingle(BitConverter.SingleToInt32Bits(0.95f) - 1);
                ExpectRefusedCoroutines(2);
                rig.Stick.OnDrag(movement, justBelow);
                Assert.That(rig.State, Is.EqualTo("FINE_CONTROL"), "exiting full tilt buzzes the exiting event (two definitions)");
                Assert.That(rig.Graphics, Is.EqualTo((false, false, true, true)));

                rig.Stick.OnDrag(movement, 0.5f);
                Assert.That(rig.State, Is.EqualTo("FINE_CONTROL"));

                ExpectRefusedCoroutines(1);
                rig.Stick.OnDrag(movement, 0.95f);
                Assert.That(rig.State, Is.EqualTo("FULL_TILT"), "0.95 is already full tilt");

                ExpectRefusedCoroutines(2);
                rig.Stick.OnDrag(movement, 0.05f);
                Assert.That(rig.State, Is.EqualTo("FINE_CONTROL"), "the tolerance itself is not below it");

                rig.Stick.OnDrag(movement, 0.04f);
                Assert.That(rig.State, Is.EqualTo("FINE_CONTROL"));
            });
        }

        // The rotation about z is the unsigned angle from up to the movement, positive when the movement's x is below
        // zero and negative otherwise (0x690045 compares x with 0).
        [Test]
        public void TheHeadingRotatesTheOuterAndTheStickTopBySignedAngle()
        {
            WithStick(rig =>
            {
                rig.Stick.Initialise();
                foreach ((Vector3 movement, float degrees) in new[]
                {
                    (new Vector3(0f, 10f, 0f), 0f), (new Vector3(10f, 0f, 0f), -90f), (new Vector3(-10f, 0f, 0f), 90f),
                    (new Vector3(-10f, 10f, 0f), 45f), (new Vector3(10f, 10f, 0f), -45f),
                })
                {
                    rig.Stick.OnDrag(movement, 0.5f);
                    AssertRotationAboutZ(rig.Rotating.localRotation, degrees, "outer " + movement);
                    AssertRotationAboutZ(rig.Top.localRotation, degrees, "top " + movement);
                }
            });
        }

        // SetStickOrigin buzzes the select event (+0x138) once for every origin it sets: at Initialise, at a press and
        // when the transform is updated.
        [Test]
        public void EverySetOriginBuzzesTheSelectEvent()
        {
            WithStick(rig =>
            {
                ArmHaptics(rig, Definition(rig, VibrationEvent.DefaultSelection));
                ModernUITestSupport.Set(rig.Stick, "m_hapticsOnSelect", VibrationEvent.DefaultSelection); // serialized in the original

                ExpectRefusedCoroutines(1);
                rig.Stick.Initialise();
                ExpectRefusedCoroutines(1);
                rig.Stick.TransformUpdated();
                ExpectRefusedCoroutines(1);
                rig.Press(1000f, 500f);
            });
        }

        // TransformUpdated 0x690400 sets the origin again from the stored one: the display size is measured again
        // (container size times scale) and the stick goes back to its held-centre state.
        [Test]
        public void UpdatingTheTransformMeasuresTheDisplayAreaAgainAndCentresTheStick()
        {
            WithStick(rig =>
            {
                rig.Stick.Initialise();
                rig.Stick.OnDrag(new Vector3(0f, 10f, 0f), 1f);
                Assert.That(rig.State, Is.EqualTo("FULL_TILT"));

                rig.Container.sizeDelta = new Vector2(300f, 100f);
                rig.Container.localScale = new Vector3(2f, 1f, 1f);
                rig.Stick.TransformUpdated();

                Assert.That(Field<Vector2>(rig.Stick, "m_visualDisplayAreaDimensions"), Is.EqualTo(new Vector2(600f, 100f)));
                Assert.That(rig.State, Is.EqualTo("HELD_CENTRE"));
                Assert.That(Field<Vector2>(rig.Stick, "m_origin"), Is.EqualTo(new Vector2(1000f, 500f)));
            });
        }

        // OnTouchRelease 0x6900A0 (after the base release, which supplies the empty movement) puts a dynamic stick at
        // idle and a fixed one at its held centre, returns the stick top to its local origin, resets the heading and
        // shows the container only for the input types the zone is shown on.
        [Test]
        public void ReleasingRestsTheStickAndShowsTheContainerForTheLastInputType()
        {
            foreach (bool dynamic in new[] { false, true })
            {
                WithStick(rig =>
                {
                    rig.Settings.DynamicMovementStick = dynamic;
                    ModernUITestSupport.Call(rig.Stick, "OnSaveDataLoaded", rig.Services.Save);
                    Field<List<InputType>>(rig.Stick, "m_showOnInputTypes").Add(ControlMapping.LastInputType);
                    rig.Top.localPosition = new Vector3(7f, 8f, 0f);

                    rig.Press(1060f, 580f);
                    rig.Log.Calls.Clear();
                    rig.Release();

                    Assert.That(rig.Log.Calls, Is.EqualTo(new[] { Row(GameInput.Left, 0f), Row(GameInput.Down, 0f) }), "dynamic " + dynamic);
                    Assert.That(rig.State, Is.EqualTo(dynamic ? "IDLE" : "HELD_CENTRE"));
                    Assert.That(rig.Graphics, Is.EqualTo(dynamic ? (true, false, false, false) : (false, true, false, true)));
                    Near(rig.Top.localPosition, 0f, 0f, 0f, "stick top");
                    Assert.That(Quaternion.Angle(rig.Rotating.localRotation, Quaternion.identity), Is.LessThan(0.01f));
                    Assert.That(rig.Container.gameObject.activeSelf, Is.True, "shown for the last input type");

                    Field<List<InputType>>(rig.Stick, "m_showOnInputTypes").Clear();
                    rig.Press(1060f, 580f);
                    rig.Release();
                    Assert.That(rig.Container.gameObject.activeSelf, Is.False, "not listed");
                });
            }
        }

        // OnInputTypeChanged 0x690370 shows the container exactly for the listed input types.
        [Test]
        public void TheContainerIsShownForTheListedInputTypesOnly()
        {
            WithStick(rig =>
            {
                Field<List<InputType>>(rig.Stick, "m_showOnInputTypes").Add(InputType.Touch);
                rig.Stick.OnInputTypeChanged(InputType.Touch);
                Assert.That(rig.Container.gameObject.activeSelf, Is.True);
                rig.Stick.OnInputTypeChanged(InputType.Keyboard);
                Assert.That(rig.Container.gameObject.activeSelf, Is.False);
                rig.Stick.OnInputTypeChanged(InputType.Touch);
                Assert.That(rig.Container.gameObject.activeSelf, Is.True);
            });
        }

        // A stick that controls the camera supplies the touch movement scaled by the secondary sensitivities (75 over the
        // screen height for x, 125 over the screen width for y) instead of the stick input, while the stick top still
        // follows the touch; the container is then shown for InputType.Unsupported whatever the last input type is.
        [Test]
        public void ACameraControllingStickSuppliesTheScaledMovementAndShowsForUnsupported()
        {
            WithStick(rig =>
            {
                rig.Stick.Initialise();
                Field<List<InputType>>(rig.Stick, "m_showOnInputTypes").Add(InputType.Unsupported);

                rig.Press(1060f, 580f);
                Assert.That(rig.Log.Calls, Is.EqualTo(new[] { Row(GameInput.Left, 0f), Row(GameInput.Down, 0f) }), "no movement on the first pass");
                Assert.That(rig.Container.gameObject.activeSelf, Is.True);

                rig.Log.Calls.Clear();
                rig.Move(1100f, 600f);
                // Equal sizes would hide a swapped height/width pairing.
                Assume.That(Screen.width, Is.Not.EqualTo(Screen.height));
                float xScale = 75f / (float)Screen.height;
                float yScale = 125f / (float)Screen.width;
                float x = xScale * 40f;
                float y = yScale * 20f;
                Assert.That(rig.Log.Calls, Is.EqualTo(new[] { Row(GameInput.Right, x), Row(GameInput.Up, y) }));
                Near(rig.Top.position, 1070.7107f, 535.3553f, 0f, "the stick top follows the touch");
                Assert.That(Field<Vector2>(rig.Stick, "m_controllerInput"), Is.EqualTo(new Vector2(x, y)));
                Assert.That(Field<Vector2>(rig.Stick, "m_lastTouch"), Is.EqualTo(new Vector2(1100f, 600f)));

                Field<List<InputType>>(rig.Stick, "m_showOnInputTypes").Clear();
                Field<List<InputType>>(rig.Stick, "m_showOnInputTypes").Add(InputType.Touch);
                rig.Move(1101f, 600f);
                Assert.That(rig.Container.gameObject.activeSelf, Is.False, "Unsupported is not listed any more");
            });
        }

        // IsSingleTouch 0x690570 is the base answer (the serialized restriction) and not controlling the camera. The
        // answer shows in the base zone taking a new touch below its peak only when it is a single-touch zone.
        [Test]
        public void ASingleTouchZoneThatControlsTheCameraTakesSeveralTouches()
        {
            foreach ((bool restricted, bool controlsCamera, bool single) in new[]
            {
                (false, false, false), (false, true, false), (true, false, true), (true, true, false),
            })
            {
                WithStick(rig =>
                {
                    ModernUITestSupport.Set(rig.Stick, "m_restrictToSingleTouch", restricted); // serialized in the original
                    ModernUITestSupport.Call(rig.Stick, "SetControllingCamera", controlsCamera);
                    rig.Stick.StartTrackingTouch(TouchAt(1, 1f, 1f));
                    rig.Stick.StartTrackingTouch(TouchAt(2, 2f, 2f));
                    rig.Stick.StopTrackingTouch(TouchAt(1, 0f, 0f));
                    rig.Stick.StartTrackingTouch(TouchAt(3, 3f, 3f));
                    Assert.That(rig.Stick.IsTrackingTouch(TouchAt(3, 0f, 0f)), Is.EqualTo(single), "restricted " + restricted + ", camera " + controlsCamera);
                });
            }
        }

        // SetEditing 0x690540 turns multi-touch to the component's enabled state (the argument is not read).
        // CanAcceptNewTouch (0x68E740) takes a second touch only with multi-touch on.
        [Test]
        public void EditingFollowsTheEnabledStateAndNotTheArgument()
        {
            WithStick(rig =>
            {
                rig.Stick.gameObject.SetActive(true);
                rig.Stick.StartTrackingTouch(TouchAt(1, 1f, 1f));
                Assert.That(rig.Stick.CanAcceptNewTouch(), Is.False, "multi-touch is off");

                rig.Stick.enabled = false;
                rig.Stick.SetEditing(true);
                Assert.That(rig.Stick.CanAcceptNewTouch(), Is.False, "disabled component, editing on");

                rig.Stick.enabled = true;
                rig.Stick.SetEditing(false);
                Assert.That(rig.Stick.CanAcceptNewTouch(), Is.True, "enabled component, editing off");
            });
        }

        // GetTouchPosition 0x690430 answers the last touch while any touch is tracked and Vector2.zero otherwise.
        [Test]
        public void TheTouchPositionIsTheLastTouchWhileATouchIsTracked()
        {
            WithStick(rig =>
            {
                Assert.That(rig.Stick.GetTouchPosition(out Vector2 touch), Is.False);
                Assert.That(touch, Is.EqualTo(Vector2.zero));

                rig.Press(1060f, 580f);
                Assert.That(rig.Stick.GetTouchPosition(out touch), Is.True);
                Assert.That(touch, Is.EqualTo(new Vector2(1060f, 580f)));

                rig.Release();
                Assert.That(rig.Stick.GetTouchPosition(out touch), Is.False);
                Assert.That(touch, Is.EqualTo(Vector2.zero), "the stored touch is not reported without a touch");
            });
        }

        // GetPinchInfo 0x6904C0: the distances while a pinch is in progress (two touches 30 and 40 apart: the diagonal 50
        // is the minimum pinch distance), zero otherwise.
        [Test]
        public void PinchInfoIsReportedOnlyWhileAPinchIsInProgress()
        {
            WithStick(rig =>
            {
                Assert.That(rig.Stick.GetPinchInfo(out float pinch, out float initial), Is.False);
                Assert.That((pinch, initial), Is.EqualTo((0f, 0f)));

                rig.Stick.StartTrackingTouch(TouchAt(1, 0f, 0f));
                rig.Stick.StartTrackingTouch(TouchAt(2, 30f, 40f));
                rig.Stick.SupplyTouchData();
                Assert.That(rig.Stick.GetPinchInfo(out pinch, out initial), Is.True);
                Assert.That((pinch, initial), Is.EqualTo((50f, 50f)));
            });
        }

        // GetRectTransform 0x690420 answers the container.
        [Test]
        public void TheRectTransformIsTheContainer()
        {
            WithStick(rig => Assert.That(rig.Stick.GetRectTransform(), Is.SameAs(rig.Container)));
        }
    }
}
