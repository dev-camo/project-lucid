using System;
using System.Collections.Generic;
using System.Reflection;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;
using static ProjectLucid.Tests.BossAbilityTestSupport;
using static ProjectLucid.Tests.CharacterTriggerTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;
using State = HardlightProject.NightmareEggmanProjectile.ProjectileMovementState;

namespace ProjectLucid.Tests
{
    // NightmareEggmanProjectile (Game.Runtime 0200066b; x86-64 0x57D330..0x57EB40) and its visual behaviour (0200066d; 0x57D770,
    // 0x57E470..0x57F1E0). Both are real components on owned hosts; no Awake or OnEnable runs in Edit Mode, so each case calls the
    // member it covers. The spline is a real InertSpline: a straight Bezier along +z (controls at thirds of its 90-unit knot), on which
    // the world transform at spline distance d is at (0, 0, d). The player stands at (0, 0, 40), so its spline distance is 40 (the
    // nearest-point search is numerical, hence the tolerance on positions that depend on it). Serialized fields the scene would
    // supply (the visual behaviour list, the phase settings, the events, the projectile definition) and the references Setup and
    // Awake would cache are set by reflection, with the reason where it is not obvious.
    // Not covered: the Random.Range draws of Fire for a non-degenerate range (the range collapses to one value in the cases), and the
    // sampled curve of ReturnToPlayerSplinePosition other than constants.
    public sealed class NightmareEggmanProjectileTests
    {
        // Global UnityEngine.Random state is borrowed: whatever a test draws, the incoming state is put back.
        private UnityEngine.Random.State _incomingRandomState;

        [NUnit.Framework.SetUp]
        public void SaveIncomingRandomState()
        {
            _incomingRandomState = UnityEngine.Random.state;
        }

        [NUnit.Framework.TearDown]
        public void RestoreIncomingRandomState()
        {
            UnityEngine.Random.state = _incomingRandomState;
        }

        private const float PlayerDistance = 40f;
        private const float PositionTolerance = 0.1f;
        // A sweep from a collider reports that collider too (the cast starts inside it), so the visual behaviour sits on the
        // Ignore Raycast layer and the walls and the casts use the Default layer only.
        private const int IgnoreRaycastLayer = 2;
        private const int WallMask = 1;
        private static readonly Type SettingsType =
            typeof(NightmareEggmanProjectileVisualBehaviour).GetNestedType("MovementPhaseSettings", BindingFlags.NonPublic);
        private static readonly Type RotationTypeType =
            typeof(NightmareEggmanProjectileVisualBehaviour).GetNestedType("RotationType", BindingFlags.NonPublic);

        private sealed class Scene
        {
            public Owned Owned;
            public NightmareEggmanProjectile Projectile;
            public InertSpline Inert;
            public Spline Spline;
            public int[] ReachedTarget;
            public int[] FiringAtPlayer;
            public int[] Fired;
            public int[] CoolingDown;
        }

        private static int[] CountEvent(object target, string field)
        {
            var count = new int[1];
            if (Get<UnityEvent>(target, field) == null) Set(target, field, new UnityEvent());
            Get<UnityEvent>(target, field).AddListener(() => count[0]++);
            return count;
        }

        private static int BaseState(NightmareEggmanProjectile projectile) => Convert.ToInt32(Get<object>(projectile, "m_state"));

        private static State Phase(NightmareEggmanProjectile projectile) => Get<State>(projectile, "m_currentMovementState");

        // The straight inert spline of the guardian projectile cases: 90 units along +z.
        private static InertSpline NewStraightSpline(Owned owned, out Spline spline)
        {
            InertSpline inert = owned.Host("Lucid nightmare inert spline", false).AddComponent<InertSpline>();
            spline = owned.Host("Lucid nightmare spline", false).AddComponent<Spline>();
            Set(spline, "m_inertSpline", inert);
            Set(spline, "m_activeSplineType", SplineVariantType.Inert);
            Set(inert, "m_splineType", SplineType.Bezier);
            var start = new InertSplineKnot();
            Set(start, "m_transform", new LightweightTransform(Vector3.zero, Quaternion.identity));
            Set(start, "m_tangentNext", new Vector3(0f, 0f, 30f));
            var end = new InertSplineKnot();
            Set(end, "m_transform", new LightweightTransform(new Vector3(0f, 0f, 90f), Quaternion.identity));
            Set(end, "m_tangentPrev", new Vector3(0f, 0f, -30f));
            foreach (InertSplineKnot knot in new[] { start, end })
            {
                Set(knot, "m_length", 90f);
                Set(knot, "m_lengthInverse", 1f / 90f);
                Set(knot, "m_tLut", new[] { 0f, 0.5f });
            }
            Set(inert, "m_knots", new[] { start, end });
            return inert;
        }

        // A projectile on the spline with the registered character manager cached and the player current at (0, 0, 40).
        private static void WithScene(Action<Scene> body) => WithManager((owned, manager, character) =>
        {
            owned.LeaseRow<TimeManager>(); // the time-scaled projectile subscribes to the TimeManager row
            var scene = new Scene { Owned = owned };
            scene.Projectile = EnemyAbilityTestSupport.AddTimeScaled<NightmareEggmanProjectile>(owned.Host("Lucid nightmare projectile", false));
            scene.Inert = NewStraightSpline(owned, out scene.Spline);
            scene.Projectile.SetSpline(scene.Spline);
            var definition = owned.Track(ScriptableObject.CreateInstance<ProjectileDefinition>());
            Set(definition, "m_velocity", 10f);
            Set(definition, "m_maxLifespanSeconds", 4f);
            Set(definition, "m_collisionLayerMask", (LayerMask)WallMask);
            Set(scene.Projectile, "m_definition", definition); // Initialise needs the App's hazard table
            Set(scene.Projectile, "m_characterManager", manager); // cached by Setup, which these cases do not run
            Set(scene.Projectile, "m_offsetVisualBehaviours", new NightmareEggmanProjectileVisualBehaviour[0]); // serialized, supplied by the prefab
            MakeCurrent(manager, character, new Vector3(0f, 0f, PlayerDistance));
            scene.ReachedTarget = CountEvent(scene.Projectile, "m_onReachedTargetDistance");
            scene.FiringAtPlayer = CountEvent(scene.Projectile, "m_onFiringAtPlayer");
            scene.Fired = CountEvent(scene.Projectile, "m_onFired");
            scene.CoolingDown = CountEvent(scene.Projectile, "m_onCoolingDown");
            body(scene);
        });

        // A flying projectile: the world spline is what Fire would have fetched from the spline component, and the phase fields are the
        // ones Fire draws. Reflection: Fire also builds a tracker, which only the moving-ahead phase uses.
        private static void StartPhase(Scene scene, State phase, float desiredMetersAhead, float secondsToStay = 3f)
        {
            Set(scene.Projectile, "m_worldSpline", scene.Inert);
            Set(scene.Projectile, "m_desiredMetersAheadOfPlayer", desiredMetersAhead);
            Set(scene.Projectile, "m_timeToStayAheadOfPlayer", secondsToStay);
            Call(scene.Projectile, "ChangeState", phase);
        }

        private static Vector3 Move(Scene scene, float deltaTime) => (Vector3)Call(scene.Projectile, "DoMovement", deltaTime);

        // ---- the constructor ----

        // 0x57E9F0: the serialized tuning values of a fresh component, and the empty tracking collections; the projectile base leaves it
        // Expired (3) with a four-hit buffer.
        [Test]
        public void ConstructedProjectileHasTheNativeTuningDefaults()
        {
            Owned.Run(owned =>
            {
                owned.LeaseRow<TimeManager>();
                var projectile = EnemyAbilityTestSupport.AddTimeScaled<NightmareEggmanProjectile>(owned.Host("Lucid nightmare defaults", false));
                Assert.That(Get<float>(projectile, "m_metersAheadOfPlayerMin"), Is.EqualTo(20f));
                Assert.That(Get<float>(projectile, "m_metersAheadOfPlayerMax"), Is.EqualTo(25f));
                Assert.That(Get<float>(projectile, "m_timeToStayAheadOfPlayerMin"), Is.EqualTo(2f));
                Assert.That(Get<float>(projectile, "m_timeToStayAheadOfPlayerMax"), Is.EqualTo(4f));
                Assert.That(Get<float>(projectile, "m_returnSplineDistancePerSecond"), Is.EqualTo(5f));
                Assert.That(Get<float>(projectile, "m_splineMetersCloseToPlayer"), Is.EqualTo(4f));
                Assert.That(Get<float>(projectile, "m_secondsUntilExplosionOnceClose"), Is.EqualTo(2f));
                Assert.That(Get<Dictionary<ISurface, ISurface[]>>(projectile, "m_trackableSurfaces"), Is.Empty);
                Assert.That(Get<List<ISurface>>(projectile, "m_trackableSurfacesList"), Is.Empty);
                Assert.That(BaseState(projectile), Is.EqualTo(3));
            });
        }

        // ---- the trivial overrides ----

        // 0x57E9C0/0x57E9D0/0x57E9E0 return false, do nothing and return false; 0x57E9A0 stores the spline.
        [Test]
        public void TheTrivialOverridesAndSetSplineDoWhatTheNativeBodiesDo()
        {
            WithScene(scene =>
            {
                Collider collider = scene.Owned.Host("Lucid nightmare bystander", false).AddComponent<BoxCollider>();
                Assert.That((bool)Call(scene.Projectile, "ShouldIgnoreCollider", collider), Is.False);
                Assert.That((bool)Call(scene.Projectile, "ShouldTeleportToHitPoint"), Is.False);
                scene.Projectile.transform.position = new Vector3(1f, 2f, 3f);
                scene.Projectile.UpdateFocus(new Vector3(9f, 9f, 9f));
                Assert.That(scene.Projectile.transform.position, Is.EqualTo(new Vector3(1f, 2f, 3f)), "the focus is ignored");
                Assert.That(Get<Spline>(scene.Projectile, "m_spline"), Is.SameAs(scene.Spline));
                scene.Projectile.SetSpline(null);
                Assert.That(Get<Spline>(scene.Projectile, "m_spline"), Is.Null);
            });
        }

        // ---- Fire ----

        // 0x57D330: with collapsed ranges the draws are the range values; the projectile starts in the moving-ahead phase, takes the
        // spline's world form, tracks it with one tracker, starts at the spline distance the draw behind the player (40 - 22 = 18), and
        // fires like any projectile (state Fired, lifespan from the definition, the fired event once).
        [Test]
        public void FireStartsBehindThePlayerOnTheSplineAndFiresTheProjectile()
        {
            WithScene(scene =>
            {
                Set(scene.Projectile, "m_metersAheadOfPlayerMin", 22f);
                Set(scene.Projectile, "m_metersAheadOfPlayerMax", 22f);
                Set(scene.Projectile, "m_timeToStayAheadOfPlayerMin", 3f);
                Set(scene.Projectile, "m_timeToStayAheadOfPlayerMax", 3f);
                scene.Projectile.transform.position = new Vector3(5f, 5f, 5f);

                scene.Projectile.Fire();

                Assert.That(Get<float>(scene.Projectile, "m_desiredMetersAheadOfPlayer"), Is.EqualTo(22f).Within(1e-5f), "a draw from a collapsed range");
                Assert.That(Get<float>(scene.Projectile, "m_timeToStayAheadOfPlayer"), Is.EqualTo(3f).Within(1e-5f), "a draw from a collapsed range");
                Assert.That(Phase(scene.Projectile), Is.EqualTo(State.ProgressingAheadOfPlayer));
                Assert.That(Get<ISpline>(scene.Projectile, "m_worldSpline"), Is.SameAs(scene.Inert));
                var surfaces = Get<Dictionary<ISurface, ISurface[]>>(scene.Projectile, "m_trackableSurfaces");
                Assert.That(surfaces.Keys, Is.EqualTo(new ISurface[] { scene.Inert }));
                Assert.That(surfaces[scene.Inert], Is.SameAs(Array.Empty<ISurface>()));
                Assert.That(Get<List<ISurface>>(scene.Projectile, "m_trackableSurfacesList"), Is.EqualTo(new ISurface[] { scene.Inert }));
                Assert.That(Get<Tracker2D>(scene.Projectile, "m_tracker2D"), Is.Not.Null);
                AssertNear(scene.Projectile.transform.position, new Vector3(0f, 0f, 18f), "the start behind the player", PositionTolerance);
                AssertNear(Get<Vector3>(scene.Projectile, "m_lastFrameMovement"), scene.Projectile.transform.position, "the start is remembered");
                Assert.That(BaseState(scene.Projectile), Is.EqualTo(1), "fired");
                Assert.That(Get<float>(scene.Projectile, "m_lifeRemainingSeconds"), Is.EqualTo(4f));
                Assert.That(scene.Fired[0], Is.EqualTo(1));
            });
        }

        // 0x57D3F0 stores the phase directly (it is not ChangeState, so the phase clock is kept), 0x57D410 tells every visual behaviour
        // the moving-ahead phase, and 0x57D500/0x57D50C clear both surface collections while 0x57D5F7 only builds the tracker when there is
        // none: firing a pooled projectile again reuses the tracker and leaves one surface registered.
        [Test]
        public void FireKeepsThePhaseClockTellsTheVisualsAndReusesTheTrackerOnRefire()
        {
            WithScene(scene =>
            {
                Set(scene.Projectile, "m_metersAheadOfPlayerMin", 22f);
                Set(scene.Projectile, "m_metersAheadOfPlayerMax", 22f);
                Set(scene.Projectile, "m_timeToStayAheadOfPlayerMin", 3f);
                Set(scene.Projectile, "m_timeToStayAheadOfPlayerMax", 3f);
                NightmareEggmanProjectileVisualBehaviour visual = NewVisual(scene.Owned);
                object settings = NewSettings(State.ProgressingAheadOfPlayer);
                SetSettings(visual, settings);
                Set(scene.Projectile, "m_offsetVisualBehaviours", new[] { visual });
                Set(scene.Projectile, "m_timeInMotionPhase", 5f);

                scene.Projectile.Fire();

                Assert.That(Get<float>(scene.Projectile, "m_timeInMotionPhase"), Is.EqualTo(5f), "Fire does not restart the phase clock");
                Assert.That(Get<State>(visual, "m_currentMovementState"), Is.EqualTo(State.ProgressingAheadOfPlayer));
                Assert.That(Get<object>(visual, "m_currentMovementSettings"), Is.SameAs(settings));
                var tracker = Get<Tracker2D>(scene.Projectile, "m_tracker2D");

                scene.Projectile.Fire();

                Assert.That(Get<Tracker2D>(scene.Projectile, "m_tracker2D"), Is.SameAs(tracker), "the tracker is built only once");
                Assert.That(Get<Dictionary<ISurface, ISurface[]>>(scene.Projectile, "m_trackableSurfaces").Count, Is.EqualTo(1));
                Assert.That(Get<List<ISurface>>(scene.Projectile, "m_trackableSurfacesList").Count, Is.EqualTo(1));
                Assert.That(scene.Fired[0], Is.EqualTo(2));
            });
        }

        // ---- DoMovement ----

        // 0x57D9E0: without a world spline the step is zero and nothing moves or counts time.
        [Test]
        public void MovementWithoutAWorldSplineDoesNothing()
        {
            WithScene(scene =>
            {
                scene.Projectile.transform.position = new Vector3(1f, 2f, 3f);
                Assert.That(Move(scene, 1f), Is.EqualTo(Vector3.zero));
                Assert.That(scene.Projectile.transform.position, Is.EqualTo(new Vector3(1f, 2f, 3f)));
                Assert.That(Get<float>(scene.Projectile, "m_timeInMotionPhase"), Is.Zero);
            });
        }

        // 0x57DA90 (moving ahead): the projectile advances along the tracked spline by the definition's velocity times the step (10 *
        // 0.5 = 5 units from 18 to 23); the destination (62) is not reached, so the phase is unchanged. The step returned is the
        // old position minus the new one. Fire builds the tracker.
        [Test]
        public void MovingAheadAdvancesByVelocityTimesTheStep()
        {
            WithScene(scene =>
            {
                Set(scene.Projectile, "m_metersAheadOfPlayerMin", 22f);
                Set(scene.Projectile, "m_metersAheadOfPlayerMax", 22f);
                scene.Projectile.Fire();

                Vector3 step = Move(scene, 0.5f);

                AssertNear(scene.Projectile.transform.position, new Vector3(0f, 0f, 23f), "five units further", 0.5f);
                Assert.That(step.z, Is.EqualTo(-5f).Within(0.5f), "old position minus new position");
                Assert.That(Phase(scene.Projectile), Is.EqualTo(State.ProgressingAheadOfPlayer));
                Assert.That(Get<float>(scene.Projectile, "m_timeInMotionPhase"), Is.EqualTo(0.5f));
                Assert.That(scene.ReachedTarget[0], Is.Zero);
            });
        }

        // 0x57DF10..0x57E07A: reaching a spline distance above the player's plus the draw (40 + 22 = 62) starts the hold and raises the
        // reached-target event; a position short of it does neither.
        [Test]
        public void ReachingThePointAheadOfThePlayerStartsTheHold()
        {
            WithScene(scene =>
            {
                StartPhase(scene, State.ProgressingAheadOfPlayer, 22f);
                scene.Projectile.transform.position = new Vector3(0f, 0f, 50f);
                Call(scene.Projectile, "CheckIfDestinationReached");
                Assert.That(Phase(scene.Projectile), Is.EqualTo(State.ProgressingAheadOfPlayer), "50 is short of 62");
                Assert.That(scene.ReachedTarget[0], Is.Zero);

                Set(scene.Projectile, "m_timeInMotionPhase", 1.5f);
                scene.Projectile.transform.position = new Vector3(0f, 0f, 70f);
                Call(scene.Projectile, "CheckIfDestinationReached");
                Assert.That(Phase(scene.Projectile), Is.EqualTo(State.HoldingPosition));
                Assert.That(Get<float>(scene.Projectile, "m_timeInMotionPhase"), Is.Zero, "the phase clock restarts");
                Assert.That(scene.ReachedTarget[0], Is.EqualTo(1));
            });
        }

        // 0x57DBD6 (holding): the projectile sits at the player's spline distance plus the draw (62), facing along the spline; the hold ends
        // when the phase clock is above the stay time (0x57E1C2: jbe, so a clock equal to it holds), starting the charge and raising the
        // firing-at-player event. The step is the old position minus the new.
        [Test]
        public void HoldingStaysAheadOfThePlayerUntilTheStayTimeIsPassed()
        {
            WithScene(scene =>
            {
                StartPhase(scene, State.HoldingPosition, 22f);
                scene.Projectile.transform.position = new Vector3(0f, 0f, 5f);

                Vector3 step = Move(scene, 1f);
                AssertNear(scene.Projectile.transform.position, new Vector3(0f, 0f, 62f), "ahead of the player", PositionTolerance);
                Assert.That(step.z, Is.EqualTo(-57f).Within(PositionTolerance), "old position minus new position");
                Assert.That(Quaternion.Angle(scene.Projectile.transform.rotation, Quaternion.identity), Is.LessThan(1f), "faces along the spline");
                Assert.That(Get<float>(scene.Projectile, "m_timeInMotionPhase"), Is.EqualTo(1f));
                Assert.That(Phase(scene.Projectile), Is.EqualTo(State.HoldingPosition));

                Move(scene, 2f);
                Assert.That(Get<float>(scene.Projectile, "m_timeInMotionPhase"), Is.EqualTo(3f));
                Assert.That(Phase(scene.Projectile), Is.EqualTo(State.HoldingPosition), "a clock equal to the stay time still holds");
                Assert.That(scene.FiringAtPlayer[0], Is.Zero);

                Move(scene, 0.5f);
                Assert.That(Phase(scene.Projectile), Is.EqualTo(State.ChargingPlayer));
                Assert.That(Get<float>(scene.Projectile, "m_timeInMotionPhase"), Is.Zero, "the phase clock restarts");
                Assert.That(scene.FiringAtPlayer[0], Is.EqualTo(1));
            });
        }

        // 0x57E1BA..0x57E1C9 (and the inlined copy at 0x57DC00): jbe is also taken on an unordered compare, so a NaN stay time never
        // starts the charge and the projectile keeps holding. `clock <= stay` as the guard would charge.
        [Test]
        public void HoldingWithANaNStayTimeNeverStartsTheCharge()
        {
            WithScene(scene =>
            {
                StartPhase(scene, State.HoldingPosition, 22f);
                Set(scene.Projectile, "m_timeToStayAheadOfPlayer", float.NaN); // Fire draws it from the authored range
                scene.Projectile.transform.position = new Vector3(0f, 0f, 5f);

                Move(scene, 1f);

                Assert.That(Phase(scene.Projectile), Is.EqualTo(State.HoldingPosition));
                Assert.That(Get<float>(scene.Projectile, "m_timeInMotionPhase"), Is.EqualTo(1f));
                Assert.That(scene.FiringAtPlayer[0], Is.Zero);
            });
        }

        // 0x57DC68 (charging): the remaining distance ahead of the player shrinks by the curve at the phase clock times the return speed
        // (5) times the step, and the projectile sits at the player's spline distance plus what remains. Constant curve 1, step 2:
        // 14 - 10 = 4, the projectile is at 44. The charge ends when the remaining distance is not above the close-to-player range
        // (0x57DCA4: ja, so exactly 4 ends it): 14 -> 4 does, 14.5 -> 4.5 does not. A curve of one half halves the closing.
        [Test]
        public void ChargingClosesTheDistanceByTheCurveTimesTheReturnSpeed()
        {
            foreach ((float desired, float curve, float expectedDesired, State expectedPhase) in new[]
            {
                (14f, 1f, 4f, State.ReachedPlayer),
                (14.5f, 1f, 4.5f, State.ChargingPlayer),
                (14f, 0.5f, 9f, State.ChargingPlayer),
            })
            {
                WithScene(scene =>
                {
                    Set(scene.Projectile, "m_returnSplineDistancePerSecondCurve", AnimationCurve.Constant(0f, 10f, curve));
                    StartPhase(scene, State.ChargingPlayer, desired);

                    Move(scene, 2f);

                    string label = $"desired {desired} curve {curve}";
                    Assert.That(Get<float>(scene.Projectile, "m_desiredMetersAheadOfPlayer"), Is.EqualTo(expectedDesired), label);
                    AssertNear(scene.Projectile.transform.position, new Vector3(0f, 0f, PlayerDistance + expectedDesired), label, PositionTolerance);
                    Assert.That(Phase(scene.Projectile), Is.EqualTo(expectedPhase), label);
                    Assert.That(Get<float>(scene.Projectile, "m_timeInMotionPhase"), Is.EqualTo(expectedPhase == State.ReachedPlayer ? 0f : 2f), label);
                    Assert.That(scene.FiringAtPlayer[0], Is.Zero, "no event is raised on reaching the player");
                });
            }
        }

        // 0x57DA63 adds the step to the phase clock before 0x57E295 evaluates the curve at it. A linear curve from (0, 0) to (4, 1), a
        // step of 2: the clock is 2, the scale 0.5 and 0.5 * 5 * 2 = 5 closes 14 to 9. Sampling before the increment (clock 0, scale 0)
        // would leave 14.
        [Test]
        public void ChargingSamplesTheCurveAtTheClockAfterTheStep()
        {
            WithScene(scene =>
            {
                Set(scene.Projectile, "m_returnSplineDistancePerSecondCurve", AnimationCurve.Linear(0f, 0f, 4f, 1f));
                StartPhase(scene, State.ChargingPlayer, 14f);

                Move(scene, 2f);

                Assert.That(Get<float>(scene.Projectile, "m_timeInMotionPhase"), Is.EqualTo(2f));
                Assert.That(Get<float>(scene.Projectile, "m_desiredMetersAheadOfPlayer"), Is.EqualTo(9f).Within(1e-4f));
                Assert.That(Phase(scene.Projectile), Is.EqualTo(State.ChargingPlayer));
            });
        }

        // 0x57DCFE (reached): the closing continues at the plain return speed (the curve is not used: a zero curve changes nothing),
        // 4 - 5 * 1 = -1 puts the projectile at 39, and it cools down (state CoolingDown, cool-down event once) once the phase clock is
        // not below the seconds until explosion (0x57E44C: the ja skip, so exactly 2 explodes).
        [Test]
        public void ReachedPlayerClosesAtThePlainSpeedAndCoolsDownAfterTheExplosionTime()
        {
            WithScene(scene =>
            {
                Set(scene.Projectile, "m_returnSplineDistancePerSecondCurve", AnimationCurve.Constant(0f, 10f, 0f));
                StartPhase(scene, State.ReachedPlayer, 4f);

                Move(scene, 1f);
                Assert.That(Get<float>(scene.Projectile, "m_desiredMetersAheadOfPlayer"), Is.EqualTo(-1f));
                AssertNear(scene.Projectile.transform.position, new Vector3(0f, 0f, 39f), "the plain speed", PositionTolerance);
                Assert.That(BaseState(scene.Projectile), Is.EqualTo(3), "still the freshly built Expired state: not cooling down");
                Assert.That(scene.CoolingDown[0], Is.Zero);

                Move(scene, 1f);
                AssertNear(scene.Projectile.transform.position, new Vector3(0f, 0f, 34f), "a further five units", PositionTolerance);
                Assert.That(BaseState(scene.Projectile), Is.EqualTo(2), "cooling down");
                Assert.That(scene.CoolingDown[0], Is.EqualTo(1));
            });
        }

        // 0x57E940 and the loops in every phase check: a phase change tells each visual behaviour the new phase, and every step moves the
        // visual behaviours after the phase logic (so a behaviour sees the new phase and a restarted clock).
        [Test]
        public void PhaseChangesAndStepsReachTheVisualBehaviours()
        {
            WithScene(scene =>
            {
                NightmareEggmanProjectileVisualBehaviour visual =
                    scene.Owned.Host("Lucid nightmare visual", false).AddComponent<NightmareEggmanProjectileVisualBehaviour>();
                Set(visual, "m_offsetVisualBehaviours", new NightmareEggmanProjectileVisualBehaviour[0]);
                SetSettings(visual, NewSettings(State.HoldingPosition), NewSettings(State.ChargingPlayer));
                Set(scene.Projectile, "m_offsetVisualBehaviours", new[] { visual });

                StartPhase(scene, State.HoldingPosition, 22f);
                Assert.That(Get<State>(visual, "m_currentMovementState"), Is.EqualTo(State.HoldingPosition), "ChangeState registers the phase");
                Assert.That(Get<float>(scene.Projectile, "m_timeInMotionPhase"), Is.Zero);

                Move(scene, 1f);
                Assert.That(Get<float>(visual, "m_timeInMotionPhase"), Is.EqualTo(1f), "the step moves the visual behaviour");

                Move(scene, 4f);
                Assert.That(Phase(scene.Projectile), Is.EqualTo(State.ChargingPlayer));
                Assert.That(Get<State>(visual, "m_currentMovementState"), Is.EqualTo(State.ChargingPlayer));
                Assert.That(Get<float>(visual, "m_timeInMotionPhase"), Is.EqualTo(4f),
                    "the visual behaviour was restarted by the phase change, then moved by the step");
            });
        }

        // ---- DoCollisions ----

        // A sphere-collider visual behaviour at (0, 0, 2) and a wall in front of it: 0x57E820 sweeps the collider by the distance it has
        // moved since the last frame (from the origin, so 2 along +z); the sphere of radius 0.5 meets the wall's face (z = 3.75) when
        // its centre is at 3.25, 1.25 along. Without a further move the cast has no length and misses; the hit info is then default.
        private static NightmareEggmanProjectileVisualBehaviour NewCollidingVisual(Owned owned, out Collider wall)
        {
            GameObject host = owned.Host("Lucid nightmare colliding visual");
            host.layer = IgnoreRaycastLayer;
            host.transform.position = new Vector3(0f, 0f, 2f);
            host.AddComponent<SphereCollider>();
            NightmareEggmanProjectileVisualBehaviour visual = host.AddComponent<NightmareEggmanProjectileVisualBehaviour>();
            Set(visual, "m_collider", host.GetComponent<SphereCollider>()); // serialized
            Set(visual, "m_offsetVisualBehaviours", new NightmareEggmanProjectileVisualBehaviour[0]);
            GameObject wallHost = owned.Host("Lucid nightmare wall");
            wallHost.transform.position = new Vector3(0f, 0f, 4f);
            BoxCollider box = wallHost.AddComponent<BoxCollider>();
            box.size = new Vector3(4f, 4f, 0.5f);
            wall = box;
            Physics.SyncTransforms();
            return visual;
        }

        [Test]
        public void ACollidingVisualBehaviourSweepsFromWhereItWasLastFrame()
        {
            Owned.Run(owned =>
            {
                NightmareEggmanProjectileVisualBehaviour visual = NewCollidingVisual(owned, out Collider wall);

                Assert.That(visual.DoCollisions(WallMask, out RaycastHit hit), Is.True);
                Assert.That(hit.collider, Is.SameAs(wall));
                Assert.That(hit.distance, Is.EqualTo(1.25f).Within(1e-3f));
                Assert.That(Get<Vector3>(visual, "m_lastFrameWorldPosition"), Is.EqualTo(new Vector3(0f, 0f, 2f)), "the position is remembered");

                Assert.That(visual.DoCollisions(WallMask, out RaycastHit again), Is.False, "no movement since the last call: a zero-length cast");
                Assert.That(again.collider, Is.Null);
                Assert.That(again.distance, Is.Zero, "the hit info is default");
            });
        }

        // 0x57E8DF takes the entry at count - 1: with two walls along the sweep the reported hit is the second one the cast returns.
        [Test]
        public void ASweepThroughTwoWallsReportsTheLastHitTheCastReturns()
        {
            Owned.Run(owned =>
            {
                NightmareEggmanProjectileVisualBehaviour visual = NewCollidingVisual(owned, out Collider farWall);
                GameObject nearHost = owned.Host("Lucid nightmare near wall");
                nearHost.transform.position = new Vector3(0f, 0f, 3f);
                nearHost.AddComponent<BoxCollider>().size = new Vector3(4f, 4f, 0.5f);
                Physics.SyncTransforms();

                var expected = new RaycastHit[4];
                var sphere = visual.GetComponent<SphereCollider>();
                Assert.That(sphere.PhysicsCastFromCollider(new Vector3(0f, 0f, 2f), WallMask, expected, QueryTriggerInteraction.Ignore), Is.EqualTo(2),
                    "both walls lie on the sweep from the origin to the visual");

                Assert.That(visual.DoCollisions(WallMask, out RaycastHit hit), Is.True);
                Assert.That(hit.collider, Is.SameAs(expected[1].collider), "the last entry the cast returned");
                Assert.That(hit.collider, Is.Not.SameAs(expected[0].collider));
            });
        }

        // 0x57E7A0: the projectile asks its visual behaviours with the definition's layer mask; the first that hits supplies the hit,
        // and without a hit the hit info is cleared.
        [Test]
        public void TheProjectileCollidesThroughItsVisualBehaviours()
        {
            WithScene(scene =>
            {
                NightmareEggmanProjectileVisualBehaviour visual = NewCollidingVisual(scene.Owned, out Collider wall);
                Set(scene.Projectile, "m_offsetVisualBehaviours", new[] { visual });

                var args = new object[] { Vector3.zero, default(RaycastHit) };
                Assert.That((bool)Call(scene.Projectile, "DoCollisions", args), Is.True);
                Assert.That(((RaycastHit)args[1]).collider, Is.SameAs(wall));

                args = new object[] { Vector3.zero, default(RaycastHit) };
                Assert.That((bool)Call(scene.Projectile, "DoCollisions", args), Is.False, "the second sweep has no length");
                Assert.That(((RaycastHit)args[1]).collider, Is.Null);

                Set(scene.Projectile, "m_offsetVisualBehaviours", new NightmareEggmanProjectileVisualBehaviour[0]);
                args = new object[] { Vector3.zero, default(RaycastHit) };
                Assert.That((bool)Call(scene.Projectile, "DoCollisions", args), Is.False, "no behaviours, no collision");
            });
        }

        // 0x57E820: with nested behaviours the behaviour does not sweep its own collider; it answers with the nested ones.
        [Test]
        public void ABehaviourWithNestedBehavioursCollidesOnlyThroughThem()
        {
            Owned.Run(owned =>
            {
                NightmareEggmanProjectileVisualBehaviour colliding = NewCollidingVisual(owned, out Collider wall);
                NightmareEggmanProjectileVisualBehaviour parent =
                    owned.Host("Lucid nightmare parent visual").AddComponent<NightmareEggmanProjectileVisualBehaviour>();
                Set(parent, "m_offsetVisualBehaviours", new[] { colliding });

                Assert.That(parent.DoCollisions(WallMask, out RaycastHit hit), Is.True);
                Assert.That(hit.collider, Is.SameAs(wall));
                Assert.That(Get<Vector3>(parent, "m_lastFrameWorldPosition"), Is.EqualTo(Vector3.zero), "the parent's own position is not tracked");

                Assert.That(parent.DoCollisions(WallMask, out RaycastHit second), Is.False);
                Assert.That(second.collider, Is.Null);
            });
        }

        // ---- the visual behaviour: settings ----

        private static object NewSettings(State state, Action<object> configure = null)
        {
            object settings = Activator.CreateInstance(SettingsType);
            SettingsType.GetField("MovementState").SetValue(settings, state);
            configure?.Invoke(settings);
            return settings;
        }

        // Awake rolls every phase's Offset between OffsetMin and OffsetMax (0x57ECB0), so a case that runs Awake fixes the
        // offset through both bounds.
        private static void SetRolledOffset(object settings, Vector3 offset)
        {
            SetField(settings, "OffsetMin", offset);
            SetField(settings, "OffsetMax", offset);
        }

        private static void SetField(object settings, string name, object value) => SettingsType.GetField(name).SetValue(settings, value);

        private static T GetField<T>(object settings, string name) => (T)SettingsType.GetField(name).GetValue(settings);

        private static void SetSettings(NightmareEggmanProjectileVisualBehaviour visual, params object[] settings)
        {
            Array array = Array.CreateInstance(SettingsType, settings.Length);
            for (int i = 0; i < settings.Length; i++)
                array.SetValue(settings[i], i);
            Set(visual, "m_stateSettings", array); // serialized per-phase settings
        }

        private static NightmareEggmanProjectileVisualBehaviour NewVisual(Owned owned, Transform parent = null)
        {
            var visual = owned.Host("Lucid nightmare visual behaviour", false, parent).AddComponent<NightmareEggmanProjectileVisualBehaviour>();
            Set(visual, "m_offsetVisualBehaviours", new NightmareEggmanProjectileVisualBehaviour[0]);
            return visual;
        }

        // 0x57F1E0: the curve times are one second and the home-in time ten.
        [Test]
        public void PhaseSettingsStartWithTheNativeTimes()
        {
            object settings = Activator.CreateInstance(SettingsType);
            Assert.That(GetField<float>(settings, "CurveXTime"), Is.EqualTo(1f));
            Assert.That(GetField<float>(settings, "CurveYTime"), Is.EqualTo(1f));
            Assert.That(GetField<float>(settings, "CurveZTime"), Is.EqualTo(1f));
            Assert.That(GetField<float>(settings, "LerpXYTime"), Is.EqualTo(10f));
        }

        // 0x57ECB0 draws each offset component between the minimum and the maximum; with equal bounds the draw is the bound. 0x57EB40:
        // Awake does that for every phase, caches the character manager through the registered reference and initialises the rotator.
        [Test]
        public void AwakeRollsEveryPhaseOffsetAndCachesTheCharacterManager()
        {
            WithManager((owned, manager, character) =>
            {
                NightmareEggmanProjectileVisualBehaviour visual = NewVisual(owned);
                object first = NewSettings(State.HoldingPosition, s =>
                {
                    SetField(s, "OffsetMin", new Vector3(1f, 2f, 3f));
                    SetField(s, "OffsetMax", new Vector3(1f, 2f, 3f));
                });
                object second = NewSettings(State.ChargingPlayer, s =>
                {
                    SetField(s, "OffsetMin", new Vector3(-4f, -5f, -6f));
                    SetField(s, "OffsetMax", new Vector3(-4f, -5f, -6f));
                });
                SetSettings(visual, first, second);

                Call(visual, "Awake");

                Assert.That(GetField<Vector3>(first, "Offset"), Is.EqualTo(new Vector3(1f, 2f, 3f)).Using(new UnityEngine.TestTools.Utils.Vector3EqualityComparer(1e-5f)), "draws from collapsed ranges");
                Assert.That(GetField<Vector3>(second, "Offset"), Is.EqualTo(new Vector3(-4f, -5f, -6f)).Using(new UnityEngine.TestTools.Utils.Vector3EqualityComparer(1e-5f)), "draws from collapsed ranges");
                Assert.That(Get<CharacterManager>(visual, "m_characterManager"), Is.SameAs(manager));
                Assert.That(Get<bool>(visual, "m_hasCachedTransform"), Is.True, "the rotator base ran");
            });
        }

        // 0x57D770: the last settings with the new phase become current (the loop does not stop at the first), the phase clock restarts
        // and the nested behaviours are told. A phase that inherits the previous base offset copies the previous phase's offset; one that
        // inherits the curved offset also copies the uncurved offset (the curve is evaluated but not applied).
        [Test]
        public void RegisteringAPhaseSelectsTheLastMatchingSettingsAndCarriesOffsets()
        {
            Owned.Run(owned =>
            {
                NightmareEggmanProjectileVisualBehaviour visual = NewVisual(owned);
                NightmareEggmanProjectileVisualBehaviour nested = NewVisual(owned);
                Set(visual, "m_offsetVisualBehaviours", new[] { nested });
                SetSettings(nested); // no phases of its own
                object previous = NewSettings(State.ProgressingAheadOfPlayer, s =>
                {
                    SetField(s, "Offset", new Vector3(1f, 2f, 3f));
                    SetField(s, "CurveX", true);
                    SetField(s, "AnimationCurveX", AnimationCurve.Constant(0f, 10f, 0.5f));
                });
                object firstHold = NewSettings(State.HoldingPosition, s => SetField(s, "Offset", new Vector3(7f, 7f, 7f)));
                object lastHold = NewSettings(State.HoldingPosition, s => SetField(s, "Offset", new Vector3(8f, 8f, 8f)));
                object carryBase = NewSettings(State.ChargingPlayer, s =>
                {
                    SetField(s, "MaintainPreviousBaseOffset", true);
                    SetField(s, "Offset", new Vector3(9f, 9f, 9f));
                });
                object carryCurved = NewSettings(State.ReachedPlayer, s =>
                {
                    SetField(s, "MaintainPreviousCurvedOffsetAsNewBaseOffset", true);
                    SetField(s, "Offset", new Vector3(9f, 9f, 9f));
                });
                SetSettings(visual, previous, firstHold, lastHold, carryBase, carryCurved);
                Set(visual, "m_timeInMotionPhase", 5f);

                visual.RegisterStateChange(State.ProgressingAheadOfPlayer);
                Assert.That(Get<object>(visual, "m_currentMovementSettings"), Is.SameAs(previous));
                Assert.That(Get<float>(visual, "m_timeInMotionPhase"), Is.Zero);
                Assert.That(Get<State>(nested, "m_currentMovementState"), Is.EqualTo(State.ProgressingAheadOfPlayer), "nested behaviours are told");

                visual.RegisterStateChange(State.HoldingPosition);
                Assert.That(Get<object>(visual, "m_currentMovementSettings"), Is.SameAs(lastHold), "the last matching settings win");
                Assert.That(GetField<Vector3>(lastHold, "Offset"), Is.EqualTo(new Vector3(8f, 8f, 8f)), "nothing carried");

                visual.RegisterStateChange(State.ChargingPlayer);
                Assert.That(GetField<Vector3>(carryBase, "Offset"), Is.EqualTo(new Vector3(8f, 8f, 8f)), "the hold's offset is carried");

                Set(visual, "m_currentMovementSettings", previous);
                Set(visual, "m_timeInMotionPhase", 1f);
                visual.RegisterStateChange(State.ReachedPlayer);
                Assert.That(GetField<Vector3>(carryCurved, "Offset"), Is.EqualTo(new Vector3(1f, 2f, 3f)), "the uncurved offset is carried");
                Assert.That(Get<object>(visual, "m_currentMovementSettings"), Is.SameAs(carryCurved));
            });
        }

        // 0x57D7D9/0x57D7B0: the settings are assigned only on a match, so a phase without settings keeps the previous ones while the
        // new phase is still recorded and the clock restarts.
        [Test]
        public void RegisteringAPhaseWithoutSettingsKeepsThePreviousSettings()
        {
            Owned.Run(owned =>
            {
                NightmareEggmanProjectileVisualBehaviour visual = NewVisual(owned);
                object hold = NewSettings(State.HoldingPosition);
                SetSettings(visual, hold);
                visual.RegisterStateChange(State.HoldingPosition);
                Set(visual, "m_timeInMotionPhase", 5f);

                visual.RegisterStateChange(State.ChargingPlayer);

                Assert.That(Get<object>(visual, "m_currentMovementSettings"), Is.SameAs(hold));
                Assert.That(Get<State>(visual, "m_currentMovementState"), Is.EqualTo(State.ChargingPlayer));
                Assert.That(Get<float>(visual, "m_timeInMotionPhase"), Is.Zero);
            });
        }

        // ---- the visual behaviour: movement ----

        // 0x57E519: in the first two phases the local position becomes the phase offset with each curved axis scaled by its curve at the
        // phase clock over the axis time. Offset (1,2,3), x by a constant 0.5, y by a linear 0-to-1 curve over 2 seconds, clock 0.5:
        // x = 1 * 0.5, y = 2 * 0.25, z = 3. The direction of movement is the new local position minus the old (here the origin).
        [Test]
        public void OffsetMovementScalesCurvedAxesByTheirCurves()
        {
            Owned.Run(owned =>
            {
                NightmareEggmanProjectileVisualBehaviour visual = NewVisual(owned);
                SetSettings(visual, NewSettings(State.ProgressingAheadOfPlayer, s =>
                {
                    SetField(s, "Offset", new Vector3(1f, 2f, 3f));
                    SetField(s, "CurveX", true);
                    SetField(s, "AnimationCurveX", AnimationCurve.Constant(0f, 10f, 0.5f));
                    SetField(s, "CurveXTime", 2f);
                    SetField(s, "CurveY", true);
                    SetField(s, "AnimationCurveY", AnimationCurve.Linear(0f, 0f, 1f, 1f));
                    SetField(s, "CurveYTime", 2f);
                }));
                visual.RegisterStateChange(State.ProgressingAheadOfPlayer);

                visual.DoMovement(0.5f);

                AssertNear(visual.transform.localPosition, new Vector3(0.5f, 0.5f, 3f), "curved local position", 1e-4f);
                AssertNear(Get<Vector3>(visual, "m_movementDirection"), new Vector3(0.5f, 0.5f, 3f), "movement from the origin", 1e-4f);
                Assert.That(Get<float>(visual, "m_timeInMotionPhase"), Is.EqualTo(0.5f));
            });
        }

        // 0x57E5D4..0x57E688: when charging with home-in set, the offset blends in x and y towards the player's position in the parent's
        // space and in z towards zero, by the phase clock over the lerp time clamped to [0, 1]. Parent at (10,0,0), player at (14,0,5)
        // (parent space (4,0,5)), offset (2,4,6), lerp time 10: clock 5 gives (3,2,3); clock 20 clamps to (4,0,0). Without home-in the
        // plain offset applies.
        [Test]
        public void ChargingWithHomeInBlendsTowardsThePlayerAndWithoutItUsesTheOffset()
        {
            WithManager((owned, manager, character) =>
            {
                MakeCurrent(manager, character, new Vector3(14f, 0f, 5f));
                Transform parent = owned.Host("Lucid nightmare visual parent", false).transform;
                parent.position = new Vector3(10f, 0f, 0f);
                NightmareEggmanProjectileVisualBehaviour visual = NewVisual(owned, parent);
                SetSettings(visual, NewSettings(State.ChargingPlayer, s =>
                {
                    SetRolledOffset(s, new Vector3(2f, 4f, 6f));
                    SetField(s, "HomeInOnPlayerPosition", true);
                    SetField(s, "LerpXYTime", 10f);
                }));
                Call(visual, "Awake");
                visual.RegisterStateChange(State.ChargingPlayer);

                visual.DoMovement(5f);
                AssertNear(visual.transform.localPosition, new Vector3(3f, 2f, 3f), "halfway", 1e-4f);

                visual.DoMovement(15f);
                AssertNear(visual.transform.localPosition, new Vector3(4f, 0f, 0f), "clamped at the player", 1e-4f);

                SetField(Get<object>(visual, "m_currentMovementSettings"), "HomeInOnPlayerPosition", false);
                visual.DoMovement(1f);
                AssertNear(visual.transform.localPosition, new Vector3(2f, 4f, 6f), "the plain offset", 1e-4f);
            });
        }

        // 0x57E6B9: once the player is reached the behaviour keeps moving by the last step: local position plus the movement direction,
        // which is again the same step. A step of (1,2,3) from the origin, then two further steps: (2,4,6), (3,6,9).
        [Test]
        public void ReachedPlayerKeepsMovingByTheLastStep()
        {
            Owned.Run(owned =>
            {
                NightmareEggmanProjectileVisualBehaviour visual = NewVisual(owned);
                SetSettings(visual,
                    NewSettings(State.ProgressingAheadOfPlayer, s => SetField(s, "Offset", new Vector3(1f, 2f, 3f))),
                    NewSettings(State.ReachedPlayer));
                visual.RegisterStateChange(State.ProgressingAheadOfPlayer);
                visual.DoMovement(0.1f);
                AssertNear(visual.transform.localPosition, new Vector3(1f, 2f, 3f), "first step", 1e-5f);

                visual.RegisterStateChange(State.ReachedPlayer);
                visual.DoMovement(0.1f);
                AssertNear(visual.transform.localPosition, new Vector3(2f, 4f, 6f), "second step", 1e-5f);
                AssertNear(Get<Vector3>(visual, "m_movementDirection"), new Vector3(1f, 2f, 3f), "the step stays the same", 1e-5f);
                visual.DoMovement(0.1f);
                AssertNear(visual.transform.localPosition, new Vector3(3f, 6f, 9f), "third step", 1e-5f);
            });
        }

        // 0x57E470: the nested behaviours move with the step, after the behaviour itself.
        [Test]
        public void MovementIsPassedOnToTheNestedBehaviours()
        {
            Owned.Run(owned =>
            {
                NightmareEggmanProjectileVisualBehaviour visual = NewVisual(owned);
                NightmareEggmanProjectileVisualBehaviour nested = NewVisual(owned);
                Set(visual, "m_offsetVisualBehaviours", new[] { nested });
                SetSettings(visual, NewSettings(State.HoldingPosition, s => SetField(s, "Offset", new Vector3(1f, 0f, 0f))));
                SetSettings(nested, NewSettings(State.HoldingPosition, s => SetField(s, "Offset", new Vector3(0f, 5f, 0f))));
                visual.RegisterStateChange(State.HoldingPosition);

                visual.DoMovement(0.25f);

                AssertNear(visual.transform.localPosition, new Vector3(1f, 0f, 0f), "the behaviour", 1e-5f);
                AssertNear(nested.transform.localPosition, new Vector3(0f, 5f, 0f), "the nested behaviour", 1e-5f);
                Assert.That(Get<float>(nested, "m_timeInMotionPhase"), Is.EqualTo(0.25f));
            });
        }

        // 0x57EE20: with rotation enabled for the phase the behaviour turns (smoothing is off here, so it snaps) to look along the
        // chosen direction with world up. The offset step is (5,0,0) from the origin and the player is at (10,0,0). Towards the player
        // is +x on both frames. Along the movement direction of the previous frame (0x57EEE3) is zero on the first frame and +x on the
        // second. Along this frame's local movement (0x57EEAE) is +x on the first frame and zero on the second. Looking along +x is a
        // yaw of 90 degrees; no direction (zero, or an unknown rotation type) looks along nothing, which is the identity. A phase
        // without rotation leaves the rotation alone.
        [Test]
        public void RotationFollowsThePlayerOrTheMovementDirectionOfThePhase()
        {
            Quaternion yaw = Quaternion.Euler(0f, 90f, 0f);
            foreach ((int rotationType, Quaternion first, Quaternion second) in new[]
            {
                (0, yaw, yaw), (1, Quaternion.identity, yaw), (2, yaw, Quaternion.identity), (7, Quaternion.identity, Quaternion.identity),
            })
            {
                WithManager((owned, manager, character) =>
                {
                    MakeCurrent(manager, character, new Vector3(10f, 0f, 0f));
                    NightmareEggmanProjectileVisualBehaviour visual = NewVisual(owned);
                    SetSettings(visual, NewSettings(State.HoldingPosition, s =>
                    {
                        SetRolledOffset(s, new Vector3(5f, 0f, 0f));
                        SetField(s, "RotateDuringPhase", true);
                        SetField(s, "RotationType", Enum.ToObject(RotationTypeType, rotationType));
                    }));
                    Call(visual, "Awake");
                    visual.ToggleSmoothing(false);
                    visual.RegisterStateChange(State.HoldingPosition);
                    visual.DoMovement(0.1f);
                    Assert.That(Quaternion.Angle(visual.transform.rotation, first), Is.LessThan(1e-2f), "first frame, rotation type " + rotationType);
                    visual.transform.rotation = Quaternion.identity;
                    visual.DoMovement(0.1f);
                    Assert.That(Quaternion.Angle(visual.transform.rotation, second), Is.LessThan(1e-2f), "second frame, rotation type " + rotationType);
                });
            }

            WithManager((owned, manager, character) =>
            {
                NightmareEggmanProjectileVisualBehaviour visual = NewVisual(owned);
                SetSettings(visual, NewSettings(State.HoldingPosition, s => SetRolledOffset(s, new Vector3(5f, 0f, 0f))));
                Call(visual, "Awake");
                visual.ToggleSmoothing(false);
                visual.RegisterStateChange(State.HoldingPosition);
                visual.transform.rotation = Quaternion.Euler(0f, 30f, 0f);
                visual.DoMovement(0.1f);
                Assert.That(Quaternion.Angle(visual.transform.rotation, Quaternion.Euler(0f, 30f, 0f)), Is.LessThan(1e-3f), "no rotation without the flag");
            });
        }
    }
}
