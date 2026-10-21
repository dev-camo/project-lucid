using System;
using System.Collections.Generic;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using ProjectLucid.Tests.CharCoreShared;
using UnityEngine;
using UnityEngine.TestTools.Utils;
using static ProjectLucid.Tests.CharacterAbilityTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // UITargetIndicatorController (TypeDef 0x0200084c, 0x60BAD0..0x60D2E0). The indicator parts are transforms on
    // owned hosts, the character is a real component with downward gravity, and the line renderer is a real component.
    // The expected placements are read from the 1.10.1 x86-64 bodies cited per test. Not run here: UpdateProjections
    // with projections to trace (ProjectionPlacement needs ray casts along ballistic arcs; only the empty list and the
    // non-air route are exercised).
    public sealed class UITargetIndicatorControllerTests
    {
        private const float Tolerance = 1e-3f;
        private static readonly Vector3EqualityComparer Vec = new Vector3EqualityComparer(Tolerance);

        private sealed class Rig
        {
            public readonly UITargetIndicatorController Controller = new UITargetIndicatorController();
            public CharacterGravityVectorProbe Character;
            public Transform Beam, Disc, Ring, ArrowsParent, Arrow0, Arrow1, Arrow0Scaler, Arrow1Scaler, ArrowsAimVisual;
            public Transform Aim, Location, LocationVisual, Ground, Target;
            public LineRenderer Line;
            public readonly List<Transform> Orientated = new List<Transform>();
            public Quaternion CameraFacing = Quaternion.Euler(10f, 20f, 30f);
        }

        // Every part starts on an inactive host, so an activation by the controller is visible.
        private static void WithRig(Action<Owned, Rig> body) => Owned.Run(owned =>
        {
            owned.LeaseRow<GameplayIslandManager>();
            var rig = new Rig { Character = NewCharacter(owned) };
            Transform Part(string name) => owned.Host("Lucid indicator " + name, false).transform;
            rig.Beam = Part("beam");
            rig.Disc = Part("disc");
            rig.Ring = Part("ring");
            rig.ArrowsParent = Part("arrows");
            rig.Arrow0 = Part("arrow0");
            rig.Arrow1 = Part("arrow1");
            rig.Arrow0Scaler = Part("arrow0 scaler");
            rig.Arrow1Scaler = Part("arrow1 scaler");
            rig.ArrowsAimVisual = Part("arrows aim visual");
            rig.Aim = Part("projection aim");
            rig.Location = Part("projection location");
            rig.LocationVisual = Part("projection location visual");
            rig.Ground = Part("projection ground");
            rig.Target = Part("projection target");
            rig.Line = owned.Host("Lucid indicator line", false).AddComponent<LineRenderer>();
            var renderer = new LineRendererContainer();
            Set(renderer, "m_lineRenderer", rig.Line);
            UITargetIndicatorController controller = rig.Controller;
            Set(controller, "m_beam", rig.Beam);
            Set(controller, "m_disc", rig.Disc);
            Set(controller, "m_ring", rig.Ring);
            Set(controller, "m_arrowsParent", rig.ArrowsParent);
            Set(controller, "m_arrow0", rig.Arrow0);
            Set(controller, "m_arrow1", rig.Arrow1);
            Set(controller, "m_arrow0Scaler", rig.Arrow0Scaler);
            Set(controller, "m_arrow1Scaler", rig.Arrow1Scaler);
            Set(controller, "m_arrowsAimVisual", rig.ArrowsAimVisual);
            Set(controller, "m_projectionAim", rig.Aim);
            Set(controller, "m_projectionLocation", rig.Location);
            Set(controller, "m_projectionLocationVisual", rig.LocationVisual);
            Set(controller, "m_projectionLocationRenderer", renderer);
            Set(controller, "m_projectionGround", rig.Ground);
            Set(controller, "m_projectionTarget", rig.Target);
            Set(controller, "m_projections", new List<ProjectionPlacement>());
            controller.Initialise(rig.Character, 1f, 2f, 3f, 4f, transform =>
            {
                rig.Orientated.Add(transform);
                transform.rotation = rig.CameraFacing;
            });
            body(owned, rig);
        });

        private static void AssertPlaced(Transform transform, Vector3 position, Quaternion rotation, float scale = 1f)
        {
            Assert.That(transform.gameObject.activeSelf, Is.True, transform.name + " active");
            Assert.That(transform.position, Is.EqualTo(position).Using(Vec), transform.name + " position");
            Assert.That(Quaternion.Angle(transform.rotation, rotation), Is.LessThan(0.05f), transform.name + " rotation");
            Assert.That(transform.localScale, Is.EqualTo(Vector3.one * scale).Using(Vec), transform.name + " scale");
        }

        private static readonly Vector3 AimPosition = new Vector3(1f, 2f, 3f);
        private static readonly Vector3 GroundPosition = new Vector3(4f, 5f, 6f);
        private static readonly Quaternion AimRotation = Quaternion.Euler(90f, 0f, 0f);
        private static readonly Quaternion GroundRotation = Quaternion.Euler(0f, 0f, 90f);

        private static void Place(Rig rig, float scale = 1f) =>
            rig.Controller.Set(AimPosition, AimRotation, GroundPosition, GroundRotation, scale);

        // ---- constructor (06003016, 0x60D2E0) and Set (0600300b, 0x60BC60) ----

        // 0x60d32a stores AngleAxis(-90, right) (constant at VA 0x29a6e20); 0x60d336 stores 2 and 10 from VA 0x29a7a90
        // and 0x60d346 stores the immediate 0x41f00000 (30).
        [Test]
        public void ConstructionStoresTheNativeDefaults()
        {
            var controller = new UITargetIndicatorController();
            Assert.That(Quaternion.Angle(Get<Quaternion>(controller, "LocalToWorldUp"), Quaternion.AngleAxis(-90f, Vector3.right)), Is.LessThan(0.01f));
            Assert.That((Get<float>(controller, "m_projectionTime"), Get<float>(controller, "m_projectionSpeedMin"), Get<float>(controller, "m_projectionSpeedMax")),
                Is.EqualTo((2f, 10f, 30f)));
        }

        [Test]
        public void SetStoresEachArgumentInItsOwnField()
        {
            var controller = new UITargetIndicatorController();
            controller.Set(AimPosition, AimRotation, GroundPosition, GroundRotation, 1.5f);
            Assert.That(Get<Vector3>(controller, "m_aimPosition"), Is.EqualTo(AimPosition));
            Assert.That(Get<Quaternion>(controller, "m_aimRotation"), Is.EqualTo(AimRotation));
            Assert.That(Get<Vector3>(controller, "m_groundPosition"), Is.EqualTo(GroundPosition));
            Assert.That(Get<Quaternion>(controller, "m_groundRotation"), Is.EqualTo(GroundRotation));
            Assert.That(Get<float>(controller, "m_arrowScaleMultiplier"), Is.EqualTo(1.5f));
        }

        // 0x60bb50..0x60bbd9: the character and the camera callback are stored and every projection is initialised with
        // the start speed, end speed, start gravity multiplier and end gravity multiplier in that order; each picks its
        // pair by its from-start flag.
        [Test]
        public void InitialiseStoresTheCharacterAndInitialisesEveryProjection()
        {
            WithRig((owned, rig) =>
            {
                var fromStart = new ProjectionPlacement();
                Set(fromStart, "m_fromStart", true);
                var fromEnd = new ProjectionPlacement();
                Set(rig.Controller, "m_projections", new List<ProjectionPlacement> { fromStart, fromEnd });
                Action<Transform> callback = _ => { };
                rig.Controller.Initialise(rig.Character, 1f, 2f, 3f, 4f, callback);
                Assert.That(Get<Character>(rig.Controller, "m_character"), Is.SameAs(rig.Character));
                Assert.That(Get<Action<Transform>>(rig.Controller, "m_orientateToCamera"), Is.SameAs(callback));
                Assert.That((Get<float>(fromStart, "m_speed"), Get<float>(fromStart, "m_gravityMultiplier")), Is.EqualTo((1f, 3f)));
                Assert.That((Get<float>(fromEnd, "m_speed"), Get<float>(fromEnd, "m_gravityMultiplier")), Is.EqualTo((2f, 4f)));
            });
        }

        // ---- IsTransformTypeGround (06003011, 0x60C430) ----

        [Test]
        public void OnlyTheThreeGroundTransformTypesAreGround()
        {
            WithRig((owned, rig) =>
            {
                // The native compare immediates at 0x60C436..0x60C446: 0xC275E621, 0x1828C5EF, 0x7A2212C8.
                int[] nativeGroundTypes = { -1032460767, 405325295, 2049053384 };
                foreach (AbilityAimTransformType type in Enum.GetValues(typeof(AbilityAimTransformType)))
                {
                    bool ground = Array.IndexOf(nativeGroundTypes, (int)type) >= 0;
                    Assert.That((bool)Call(rig.Controller, "IsTransformTypeGround", type), Is.EqualTo(ground), type.ToString());
                }
            });
        }

        // ---- SetTransform(transform, type) (06003012, 0x60BF90) ----

        // None hides the transform (0x60c0d9). The rotate types place it at the aim or ground position with that
        // rotation and unit scale (0x60c3e9, 0x60c147); a value outside the enum leaves it untouched (0x60c170).
        [Test]
        public void TransformTypesPlaceTheTransformAtTheAimOrGroundTarget()
        {
            WithRig((owned, rig) =>
            {
                Place(rig);
                Transform part = owned.Host("Lucid indicator subject").transform;
                Call(rig.Controller, "SetTransform", part, AbilityAimTransformType.None);
                Assert.That(part.gameObject.activeSelf, Is.False);
                Call(rig.Controller, "SetTransform", part, AbilityAimTransformType.AimTranslateRotate);
                AssertPlaced(part, AimPosition, AimRotation);
                Call(rig.Controller, "SetTransform", part, AbilityAimTransformType.GroundTranslateRotate);
                AssertPlaced(part, GroundPosition, GroundRotation);
                part.position = Vector3.zero;
                part.gameObject.SetActive(false);
                Call(rig.Controller, "SetTransform", part, (AbilityAimTransformType)12345);
                Assert.That(part.gameObject.activeSelf, Is.False);
                Assert.That(part.position, Is.EqualTo(Vector3.zero));
            });
        }

        // 0x60bfcd..0x60c0b3 and 0x60c1d5..0x60c2cc: the translate types rotate by AngleAxis(0, world up) times
        // LocalToWorldUp (-90 about right); AimTranslateWorldUp (0x60c2d1..0x60c3e7) uses the inverse of
        // LocalToWorldUp and a scale of four (constant at VA 0x29a72d8).
        [Test]
        public void TranslateTypesFaceTheLocalToWorldUpRotation()
        {
            WithRig((owned, rig) =>
            {
                Place(rig);
                Transform part = owned.Host("Lucid indicator subject").transform;
                Call(rig.Controller, "SetTransform", part, AbilityAimTransformType.AimTranslate);
                AssertPlaced(part, AimPosition, Quaternion.AngleAxis(-90f, Vector3.right));
                Call(rig.Controller, "SetTransform", part, AbilityAimTransformType.GroundTranslate);
                AssertPlaced(part, GroundPosition, Quaternion.AngleAxis(-90f, Vector3.right));
                Call(rig.Controller, "SetTransform", part, AbilityAimTransformType.AimTranslateWorldUp);
                AssertPlaced(part, AimPosition, Quaternion.AngleAxis(90f, Vector3.right), 4f);
            });
        }

        // 0x60c106..0x60c1d0 and 0x60c179..0x60c1d0: the camera facing types first let the callback orientate the
        // transform, then keep the rotation it gave while moving to the aim or ground position.
        [Test]
        public void CameraFacingTypesKeepTheRotationTheCallbackGave()
        {
            WithRig((owned, rig) =>
            {
                Place(rig);
                Transform part = owned.Host("Lucid indicator subject").transform;
                Call(rig.Controller, "SetTransform", part, AbilityAimTransformType.AimTranslateCameraFacing);
                AssertPlaced(part, AimPosition, rig.CameraFacing);
                Call(rig.Controller, "SetTransform", part, AbilityAimTransformType.GroundTranslateCameraFacing);
                AssertPlaced(part, GroundPosition, rig.CameraFacing);
                Assert.That(rig.Orientated, Is.EqualTo(new[] { part, part }));
            });
        }

        // ---- SetTransform(transform, position, rotation, scale) (06003014, 0x60D100) ----

        // 0x60d167..0x60d263: an override rotation is applied after the given one (given times override).
        // A 90 degree turn about x then 90 about y does not commute, so the order is observable.
        [Test]
        public void OverrideRotationIsAppliedAfterTheGivenRotation()
        {
            WithRig((owned, rig) =>
            {
                Place(rig);
                Quaternion overrideRotation = Quaternion.Euler(0f, 90f, 0f);
                Set(rig.Controller, "m_overrideRotation", (Quaternion?)overrideRotation);
                Transform part = owned.Host("Lucid indicator subject").transform;
                Call(rig.Controller, "SetTransform", part, AbilityAimTransformType.AimTranslateRotate);
                AssertPlaced(part, AimPosition, AimRotation * overrideRotation);
                Assert.That(Quaternion.Angle(part.rotation, overrideRotation * AimRotation), Is.GreaterThan(1f), "not the reverse order");
            });
        }

        // ---- SetTransformAnimation (06003015, 0x60C460) ----

        // 0x60c4a0..0x60c572: an active arrow turns by the rotation curve about forward and sits at the position
        // curve's distance along its own up; an inactive arrow is only hidden.
        [Test]
        public void ArrowAnimationRotatesAboutForwardAndOffsetsAlongTheTurnedUp()
        {
            WithRig((owned, rig) =>
            {
                Transform arrow = owned.Host("Lucid indicator subject").transform;
                AnimationCurve position = AnimationCurve.Constant(0f, 1f, 2f);
                AnimationCurve rotation = AnimationCurve.Constant(0f, 1f, 30f);
                Call(rig.Controller, "SetTransformAnimation", arrow, true, position, rotation, 0.5f);
                Assert.That(arrow.localPosition, Is.EqualTo(new Vector3(-1f, 1.7320508f, 0f)).Using(Vec));
                Assert.That(Quaternion.Angle(arrow.localRotation, Quaternion.AngleAxis(30f, Vector3.forward)), Is.LessThan(0.01f));
                arrow.localPosition = Vector3.zero;
                Call(rig.Controller, "SetTransformAnimation", arrow, false, position, rotation, 0.5f);
                Assert.That(arrow.gameObject.activeSelf, Is.False);
                Assert.That(arrow.localPosition, Is.EqualTo(Vector3.zero));
            });
        }

        // ---- UpdateTarget (0600300c, 0x60BCB0) ----

        private static CharacterAbilityDefinition_AirChaosAim.ChaosAimTarget Target()
        {
            var target = new CharacterAbilityDefinition_AirChaosAim.ChaosAimTarget
            {
                BeamTransformType = AbilityAimTransformType.AimTranslateRotate,
                DiscTransformType = AbilityAimTransformType.GroundTranslateRotate,
                RingTransformType = AbilityAimTransformType.GroundTranslateRotate,
                ArrowsTransformType = AbilityAimTransformType.AimTranslateRotate,
                TargetRotation = Quaternion.Euler(0f, 90f, 0f),
                ArrowsRotation = Quaternion.Euler(0f, 0f, 90f),
                Arrow0Enabled = true,
                Arrow1Enabled = false,
                Arrow0PositionAnimation = AnimationCurve.Constant(0f, 2f, 1f),
                Arrow0RotationAnimation = AnimationCurve.Constant(0f, 2f, 0f),
                Arrow1PositionAnimation = AnimationCurve.Constant(0f, 3f, 1f),
                Arrow1RotationAnimation = AnimationCurve.Constant(0f, 3f, 0f),
            };
            target.CalculateCachedValues();
            return target;
        }

        // 0x60bde5..0x60bf5a: the disc, ring and beam are placed under the target rotation as an override and the
        // arrows parent under the arrows rotation; the override is cleared afterwards. With a ground target all three
        // show, and a non-air aim hides the projection parts and the arrows aim visual.
        [Test]
        public void UpdateTargetPlacesTheIndicatorPartsUnderTheTargetRotations()
        {
            WithRig((owned, rig) =>
            {
                Place(rig);
                var target = Target();
                rig.Controller.UpdateTarget(0.1f, target, AbilityAimTargetType.Track, AbilityAimTargetType.Track, 5f, 0.1f, -1);
                Quaternion turned = Quaternion.Euler(0f, 90f, 0f);
                AssertPlaced(rig.Disc, GroundPosition, GroundRotation * turned);
                AssertPlaced(rig.Ring, GroundPosition, GroundRotation * turned);
                AssertPlaced(rig.Beam, AimPosition, AimRotation * turned);
                AssertPlaced(rig.ArrowsParent, AimPosition, AimRotation * Quaternion.Euler(0f, 0f, 90f));
                Assert.That(rig.Arrow0.gameObject.activeSelf, Is.True);
                Assert.That(rig.Arrow1.gameObject.activeSelf, Is.False);
                Assert.That(Get<Quaternion?>(rig.Controller, "m_overrideRotation").HasValue, Is.False);
                Assert.That(rig.ArrowsAimVisual.gameObject.activeSelf, Is.False);
                foreach (Transform hidden in new[] { rig.Aim, rig.Ground, rig.Target, rig.Location })
                    Assert.That(hidden.gameObject.activeSelf, Is.False, hidden.name);
                Assert.That(rig.Line.enabled, Is.False);
            });
        }

        // 0x60bdf1 and 0x60be36: with no ground target the disc is hidden (its type forced to None), and a ground
        // type ring is hidden too, while a non-ground type ring and the beam are placed as before.
        [Test]
        public void UpdateTargetHidesTheDiscAndGroundRingWithoutAGroundTarget()
        {
            WithRig((owned, rig) =>
            {
                Place(rig);
                var target = Target();
                rig.Disc.gameObject.SetActive(true);
                rig.Ring.gameObject.SetActive(true);
                rig.Controller.UpdateTarget(0.1f, target, AbilityAimTargetType.Wall, AbilityAimTargetType.None, 5f, 0.1f, -1);
                Assert.That(rig.Disc.gameObject.activeSelf, Is.False);
                Assert.That(rig.Ring.gameObject.activeSelf, Is.False);
                Assert.That(rig.Beam.gameObject.activeSelf, Is.True);
                target.RingTransformType = AbilityAimTransformType.AimTranslateRotate;
                rig.Controller.UpdateTarget(0.1f, target, AbilityAimTargetType.Wall, AbilityAimTargetType.None, 5f, 0.1f, -1);
                AssertPlaced(rig.Ring, AimPosition, AimRotation * Quaternion.Euler(0f, 90f, 0f));
            });
        }

        // 0x60bd55..0x60bd7d: a scale multiplier above one scales both arrow scalers by it and resets the arrow clocks;
        // otherwise the scalers keep unit scale and the clocks advance by the delta time (0x60bda4).
        [Test]
        public void UpdateTargetScalesTheArrowsOrAdvancesTheirClocks()
        {
            WithRig((owned, rig) =>
            {
                Place(rig, 2f);
                Set(rig.Controller, "m_arrow0AnimTime", 0.7f);
                Set(rig.Controller, "m_arrow1AnimTime", 0.7f);
                var target = Target();
                rig.Controller.UpdateTarget(0.1f, target, AbilityAimTargetType.Wall, AbilityAimTargetType.Track, 5f, 0.1f, -1);
                Assert.That(rig.Arrow0Scaler.localScale, Is.EqualTo(new Vector3(2f, 2f, 2f)).Using(Vec));
                Assert.That(rig.Arrow1Scaler.localScale, Is.EqualTo(new Vector3(2f, 2f, 2f)).Using(Vec));
                Assert.That((Get<float>(rig.Controller, "m_arrow0AnimTime"), Get<float>(rig.Controller, "m_arrow1AnimTime")), Is.EqualTo((0f, 0f)));
                Place(rig, 1f);
                rig.Controller.UpdateTarget(0.1f, target, AbilityAimTargetType.Wall, AbilityAimTargetType.Track, 5f, 0.1f, -1);
                Assert.That(rig.Arrow0Scaler.localScale, Is.EqualTo(Vector3.one).Using(Vec));
                Assert.That(Get<float>(rig.Controller, "m_arrow0AnimTime"), Is.EqualTo(0.1f).Within(1e-6f));
                Assert.That(Get<float>(rig.Controller, "m_arrow1AnimTime"), Is.EqualTo(0.1f).Within(1e-6f));
            });
        }

        // An air aim shows the projection parts: the location is active, a fresh cycle resets the projections (the
        // aim marker and the line renderer on, the location visual on) and, with nothing to trace, the location visual
        // and the ground marker are hidden again at the end (0x60cb7f, 0x60cb96) while the target marker stays off;
        // the arrows aim visual follows the aim being air.
        [Test]
        public void UpdateTargetShowsTheAirProjectionPartsAndRestartsTheCycle()
        {
            WithRig((owned, rig) =>
            {
                Place(rig);
                var target = Target();
                rig.Controller.UpdateTarget(0.1f, target, AbilityAimTargetType.Air, AbilityAimTargetType.None, 5f, 0.1f, -1);
                Assert.That(rig.Location.gameObject.activeSelf, Is.True);
                Assert.That(rig.Aim.gameObject.activeSelf, Is.True);
                Assert.That(rig.LocationVisual.gameObject.activeSelf, Is.False);
                Assert.That(rig.Ground.gameObject.activeSelf, Is.False);
                Assert.That(rig.Target.gameObject.activeSelf, Is.False);
                Assert.That(rig.Line.enabled, Is.True);
                Assert.That(rig.ArrowsAimVisual.gameObject.activeSelf, Is.True);
                Assert.That(Get<float>(rig.Controller, "m_projectionTotalTime"), Is.EqualTo(0.1f).Within(1e-6f));
                rig.Controller.UpdateTarget(0.1f, target, AbilityAimTargetType.Air, AbilityAimTargetType.None, 5f, 0.1f, -1);
                Assert.That(Get<float>(rig.Controller, "m_projectionTotalTime"), Is.EqualTo(0.2f).Within(1e-6f), "the cycle continues while under the projection time");
                // 0x60c65e..0x60c66f: `jb` skips the reset only while the total is below the projection time (2, VA
                // 0x29a7a90), so a total equal to it restarts the cycle and a smaller one does not.
                rig.Aim.gameObject.SetActive(false);
                rig.Line.enabled = false;
                Set(rig.Controller, "m_projectionTotalTime", 1.99f);
                rig.Controller.UpdateTarget(0.1f, target, AbilityAimTargetType.Air, AbilityAimTargetType.None, 5f, 0.1f, -1);
                Assert.That(Get<float>(rig.Controller, "m_projectionTotalTime"), Is.EqualTo(2.09f).Within(1e-5f));
                Assert.That(rig.Aim.gameObject.activeSelf, Is.False, "below the projection time nothing restarts");
                Assert.That(rig.Line.enabled, Is.False);
                Set(rig.Controller, "m_projectionTotalTime", 2f);
                rig.Controller.UpdateTarget(0.1f, target, AbilityAimTargetType.Air, AbilityAimTargetType.None, 5f, 0.1f, -1);
                Assert.That(Get<float>(rig.Controller, "m_projectionTotalTime"), Is.EqualTo(0.1f).Within(1e-6f), "a total equal to the projection time restarts the cycle");
                Assert.That(rig.Aim.gameObject.activeSelf, Is.True);
                Assert.That(rig.Line.enabled, Is.True);
                rig.Controller.UpdateTarget(0.1f, target, AbilityAimTargetType.Wall, AbilityAimTargetType.None, 5f, 0.1f, -1);
                Assert.That(rig.Location.gameObject.activeSelf, Is.False, "leaving the air aim hides the location");
                Assert.That(rig.Aim.gameObject.activeSelf, Is.False);
                Assert.That(rig.Line.enabled, Is.False);
                Assert.That(rig.ArrowsAimVisual.gameObject.activeSelf, Is.False);
            });
        }

        // ---- SetProjectionLocation (0600300f, 0x60CF30) and SetProjectionTarget (06003010, 0x60CE60) ----

        // 0x60cf8d..0x60d0ba: the location marker moves to the position; a ray along gravity for groundDistance
        // against the mask shows the ground marker at the hit lifted by groundOffset along the normal, looking along
        // the normal with the character forward as up. Without a hit the ground marker is hidden.
        [Test]
        public void ProjectionLocationMovesTheMarkerAndShowsTheGroundMarkerOnAHit()
        {
            WithRig((owned, rig) =>
            {
                rig.Character.SetWorldRotation(Quaternion.Euler(0f, 90f, 0f));
                GameObject floor = owned.Host("Lucid indicator floor");
                floor.transform.position = new Vector3(0f, -0.5f, 0f);
                floor.AddComponent<BoxCollider>().size = new Vector3(10f, 1f, 10f);
                Physics.SyncTransforms();
                Call(rig.Controller, "SetProjectionLocation", new Vector3(0f, 3f, 0f), 10f, 0.2f, (LayerMask)(-1));
                Assert.That(rig.Location.position, Is.EqualTo(new Vector3(0f, 3f, 0f)).Using(Vec));
                Assert.That(rig.Ground.gameObject.activeSelf, Is.True);
                Assert.That(rig.Ground.position, Is.EqualTo(new Vector3(0f, 0.2f, 0f)).Using(Vec));
                Assert.That(rig.Ground.rotation * Vector3.forward, Is.EqualTo(Vector3.up).Using(Vec));
                Assert.That(rig.Ground.rotation * Vector3.up, Is.EqualTo(new Vector3(1f, 0f, 0f)).Using(Vec));
                Call(rig.Controller, "SetProjectionLocation", new Vector3(0f, 3f, 0f), 1f, 0.2f, (LayerMask)(-1));
                Assert.That(rig.Ground.gameObject.activeSelf, Is.False);
            });
        }

        [Test]
        public void ProjectionTargetIsShownAtTheHitOnlyWhenRequested()
        {
            WithRig((owned, rig) =>
            {
                rig.Character.SetWorldRotation(Quaternion.Euler(0f, 90f, 0f));
                GameObject floor = owned.Host("Lucid indicator floor");
                floor.transform.position = new Vector3(0f, -0.5f, 0f);
                floor.AddComponent<BoxCollider>().size = new Vector3(10f, 1f, 10f);
                Physics.SyncTransforms();
                Assert.That(Physics.Raycast(new Vector3(2f, 3f, 1f), Vector3.down, out RaycastHit hit, 10f), Is.True);
                Call(rig.Controller, "SetProjectionTarget", false, hit, 0.2f);
                Assert.That(rig.Target.gameObject.activeSelf, Is.False);
                Assert.That(rig.Target.position, Is.EqualTo(Vector3.zero));
                Call(rig.Controller, "SetProjectionTarget", true, hit, 0.2f);
                Assert.That(rig.Target.gameObject.activeSelf, Is.True);
                Assert.That(rig.Target.position, Is.EqualTo(new Vector3(2f, 0.2f, 1f)).Using(Vec));
                Assert.That(rig.Target.rotation * Vector3.up, Is.EqualTo(new Vector3(1f, 0f, 0f)).Using(Vec));
            });
        }
    }
}
