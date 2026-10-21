using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.TestTools;
using static ProjectLucid.Tests.WorldMemberAccess;
using Owned = ProjectLucid.Tests.WorldTriggerTestSupport.Owned;
using State = HardlightProject.GuardianProjectile.ProjectileMovementState;

namespace ProjectLucid.Tests
{
    // GuardianProjectile (02000665, nested 02000666 and 02000667; x86-64 0x57A930..0x57CB20). It is a real
    // component on an owned host; no Awake or OnEnable runs in Edit Mode, so each case calls the member it
    // covers. The projectile definition is a real ProjectileDefinition whose private velocity and lifespan
    // are set as its serialized fields would be; the cached CharacterManager is the registered one the
    // original Setup would cache. Each expectation names the native address of the body it comes from.
    // Not covered here: the damaging branches of Hit. HitIfNotHomingAttack past its guards calls
    // Character.TryTakeDamage, whose form checks read the form handler that Awake builds, so Edit Mode cannot
    // drive it; that leaves the projectile-type gate at 0x57B6E4..0x57B6F7 (which ground-ability guards apply to
    // which types) and the EnterCooldown after the damage call (0x57B799) unobserved for a hit that damages.
    // Also not covered: the base teardown (TimeScaledComponent.Shutdown) that OnDestroy runs only with a guardian
    // manager; its TimeManager unsubscription needs the logged-exception TimeManager build of the catch-connector
    // case, so only the unsubscription from the guardian's final-hit event is observed.
    public sealed class OriginalGuardianProjectileTests
    {
        private static readonly Regex ActorDefinitionSettingsFault =
            new Regex("^NullReferenceException: Object reference not set to an instance of an object$");

        private const string BoundsAndNull = "Il2CppSetOptionAttribute(2,False) Il2CppSetOptionAttribute(1,False)";
        private const BindingFlags Declared = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        private sealed class Rig
        {
            public Owned Owned;
            public GuardianProjectile Projectile;
            public ProjectileDefinition Definition;
            public int[] Cooled;
            public int[] Fired;
        }

        private sealed class CharacterRig
        {
            public Rig Rig;
            public CharacterManager Manager;
            public Character Character;
            public Collider PlayerCollider;
        }

        private sealed class BossRig
        {
            public Rig Rig;
            public BossManager Manager;
            public GuardianManager Guardian;
            public Boss Boss;
            public Collider BossCollider;
            public Transform[] RestPoints;
        }

        // The listener count of a serialized UnityEvent field, creating the event when the host has none.
        private static int[] CountEvent(object target, string field)
        {
            var count = new int[1];
            if (Get(target, field) == null) Set(target, field, new UnityEvent());
            ((UnityEvent)Get(target, field)).AddListener(() => count[0]++);
            return count;
        }

        private static string TypeAttributes(Type type) => string.Join(" ", type.GetCustomAttributesData()
            .Select(data => data.AttributeType.Name + "(" + string.Join(",", data.ConstructorArguments.Select(argument => argument.Value)) + ")"));

        private static float Life(GuardianProjectile projectile) => (float)Get(projectile, "m_lifeRemainingSeconds");
        private static void SetLife(GuardianProjectile projectile, float seconds) => Set(projectile, "m_lifeRemainingSeconds", seconds);
        private static void SetMovement(GuardianProjectile projectile, State state) => Set(projectile, "m_movementState", state);

        // Projectile.m_state: WarmingUp 0, Fired 1, CoolingDown 2, Expired 3 (a protected nested enum).
        private static int BaseState(GuardianProjectile projectile) => Convert.ToInt32(Get(projectile, "m_state"));

        private static void SetBaseState(GuardianProjectile projectile, int state) =>
            Set(projectile, "m_state", Enum.ToObject(typeof(Projectile).GetNestedType("ProjectileState", BindingFlags.NonPublic), state));

        private static Vector3 Vec(object value) => (Vector3)value;

        // TimeScaledComponent_SDT type initialisation takes the TimeManager row; the lease releases it.
        private static Rig NewRig(Owned owned, OwnedTimeScaledObjects time, float velocity = 10f, float lifespan = 4f)
        {
            var rig = new Rig { Owned = owned };
            rig.Projectile = time.Add<GuardianProjectile>("guardian projectile");
            rig.Definition = owned.Track(ScriptableObject.CreateInstance<ProjectileDefinition>());
            Set(rig.Definition, "m_velocity", velocity);
            Set(rig.Definition, "m_maxLifespanSeconds", lifespan);
            Set(rig.Projectile, "m_definition", rig.Definition);
            rig.Cooled = CountEvent(rig.Projectile, "m_onCoolingDown");
            rig.Fired = CountEvent(rig.Projectile, "m_onFired");
            return rig;
        }

        // A projectile with the registered CharacterManager cached, the character current and its collision collider set.
        private static void WithCharacter(string prefix, Action<Owned, CharacterRig> body) =>
            CharacterTriggerTestSupport.WithManager((owned, manager, character) =>
            {
                owned.LeaseRow<TimeManager>();
                using (var time = new OwnedTimeScaledObjects(prefix))
                {
                    var rig = new CharacterRig { Manager = manager, Character = character, Rig = NewRig(owned, time) };
                    rig.PlayerCollider = character.GetComponent<BoxCollider>();
                    Set(character, "m_colliderCollision", rig.PlayerCollider);
                    CharacterTriggerTestSupport.SetCurrent(manager, character, true);
                    Set(rig.Rig.Projectile, "m_characterManager", manager); // Reflection: assigned by Awake, which these cases do not run
                    body(owned, rig);
                }
            });

        // A projectile with no character; the BossManager row is the one its SaveManager-reading field initialiser needs.
        private static void WithBoss(string prefix, Action<Owned, BossRig> body) => Owned.Run(owned =>
        {
            owned.LeaseRow<TimeManager>();
            owned.LeaseRow<SaveManager>();
            using (var time = new OwnedTimeScaledObjects(prefix))
            {
                var rig = new BossRig { Rig = NewRig(owned, time) };
                rig.Manager = owned.Host("Lucid guardian boss manager", false).AddComponent<BossManager>();
                rig.Boss = owned.Host("Lucid guardian boss", false).AddComponent<Boss>();
                CharacterTriggerTestSupport.SetActorProperty(rig.Boss, "Storage", new FSMStorage(0));
                // Registered with the manager as Boss.Awake's callback does (RegisterBoss, 060009ec). The first listed boss
                // would switch HUD focus through the App, which is absent, so a second boss is listed meanwhile.
                for (int i = 0; i < 2; i++) LogAssert.Expect(LogType.Exception, ActorDefinitionSettingsFault);
                var definition = owned.Track(ScriptableObject.CreateInstance<BossDefinition>());
                definition.Traits = new BossTraits();
                Set(rig.Boss, "m_definition", definition); // Reflection: serialized field without a setter
                // A boss already listed keeps Register (BossManager.Register) off the first-boss HUD focus, which needs the App.
                Boss placeholder = owned.Host("Lucid guardian boss placeholder", false).AddComponent<Boss>();
                rig.Manager.Bosses.Add(placeholder);
                Call(rig.Boss, "RegisterBoss", rig.Manager);
                rig.Manager.Bosses.Remove(placeholder);
                Set(rig.Boss, "m_bossInteractionComponents", Array.CreateInstance(typeof(Boss).GetNestedType("BossBodyComponents", BindingFlags.NonPublic), 0));
                rig.BossCollider = rig.Boss.gameObject.AddComponent<BoxCollider>();
                Set(rig.Boss, "m_colliderCollision", rig.BossCollider);
                rig.Guardian = owned.Host("Lucid guardian manager", false).AddComponent<GuardianManager>();
                rig.RestPoints = new[] { owned.Host("Lucid rest point 0", false).transform, owned.Host("Lucid rest point 1", false).transform };
                Set(rig.Guardian, "m_eggpawnRestPoints", rig.RestPoints.Select(point => new GuardianManager.EggpawnRestPoints { Location = point }).ToArray());
                rig.Rig.Projectile.SetBoss(rig.Boss);
                // Reflection: the projectile's Awake looks both managers up in the registry; these cases do not run it
                Set(rig.Rig.Projectile, "m_bossManager", rig.Manager);
                Set(rig.Rig.Projectile, "m_guardianManager", rig.Guardian);
                body(owned, rig);
            }
        });

        private static void AddAttachPoint(Boss boss, ActorAttachPointType attach, Transform projectileSpawn, Collider collider)
        {
            Type componentType = typeof(Boss).GetNestedType("BossBodyComponents", BindingFlags.NonPublic);
            object component = Activator.CreateInstance(componentType);
            componentType.GetField("ProjectileSpawnPoint").SetValue(component, projectileSpawn);
            componentType.GetField("Collider").SetValue(component, collider);
            object dictionary = Get(boss, "m_attachPointDictionary");
            dictionary.GetType().GetMethod("Add").Invoke(dictionary, new[] { (object)attach, component });
        }

        // ---- declarations ----

        [Test]
        public void GuardianProjectileDeclarationsKeepTheOriginalFlagsFieldsAndMethods()
        {
            Type type = typeof(GuardianProjectile);
            Assert.That((int)type.Attributes, Is.EqualTo(0x00100001));
            Assert.That(type.BaseType, Is.EqualTo(typeof(Projectile)));
            Assert.That(TypeAttributes(type), Is.EqualTo(BoundsAndNull));
            Assert.That(type.GetFields(Declared).OrderBy(field => field.MetadataToken)
                .Select(field => field.Name + ":" + field.FieldType.Name + ":" + ((int)field.Attributes).ToString("x4") + ":"
                    + string.Join(" ", field.GetCustomAttributesData().Select(data => data.AttributeType.Name))).ToArray(),
                Is.EqualTo(new[]
                {
                    "m_onHitBossPhase4:UnityEvent:0001:SerializeField", "m_lerpMovementTime:Single:0001:SerializeField",
                    "m_lerpEasing:EaseType:0001:SerializeField", "m_onArriveAtRestingPlace:UnityEvent:0001:SerializeField",
                    "m_onBossReadyForFinalHit:UnityEvent:0001:SerializeField", "m_centerOfCircle:Vector3:0001:",
                    "m_radiusOfCircle:Single:0001:", "m_progressionArcRadians:Single:0001:",
                    "m_travelSpeedRadians:Single:0001:", "m_cullTimeOnceBehindPlayer:Single:0001:",
                    "m_returningRadians:Single:0001:", "m_finalRadius:Single:0001:",
                    "m_cachedY:Single:0001:", "m_targetDirection:Vector3:0001:",
                    "m_startingDistanceSq:Single:0001:", "m_currentDistanceSq:Single:0001:",
                    "m_movementState:ProjectileMovementState:0001:", "m_bossManager:BossManager:0001:",
                    "m_guardianManager:GuardianManager:0001:", "m_returnTransform:Transform:0001:",
                    "m_startLerpMovementPosition:Vector3:0001:", "m_finalRestingTransform:Transform:0001:",
                    "m_movementIsClockwise:Boolean:0001:", "m_boss:Boss:0001:",
                    "m_behindPlayerSoPreparingToDie:Boolean:0001:", "m_hasHitBoss:Boolean:0001:",
                    "m_straightVelocity:Vector3:0001:", "m_previousFramePosition:Vector3:0001:",
                    "m_spline:Spline:0001:", "m_worldSpline:ISpline:0001:",
                    "m_tracker2D:Tracker2D:0001:", "m_trackableSurfaces:Dictionary`2:0021:",
                    "m_trackableSurfacesList:List`1:0021:", "m_nearestSplineLocationWorld:Vector3:0001:",
                    "m_distanceAlongSpline:Single:0001:", "m_lerpTime:Single:0001:",
                }));
            Assert.That(type.GetMembers(Declared).OfType<MethodBase>().OrderBy(method => method.MetadataToken)
                .Select(method => method.Name + ":" + ((int)method.Attributes).ToString("x4")).ToArray(),
                Is.EqualTo(new[]
                {
                    "UpdateFocus:00c6", "DoMovement:00c4", "Hit:00c4",
                    "OnProcessDamageAction:0081", "ShouldIgnoreCollider:00c4", "ArcMovement:0081",
                    "Fire:00c6", "SetArcCenter:0086", "SetTargetDirection:0086",
                    "SetProgressionRates:0086", "SetCullingValues:0086", "ReturnProjectile:0086",
                    "StraightLineMovement:0081", "LerpMovement:0081", "SplineMovement:0081",
                    "ShouldTeleportToHitPoint:00c4", "SetBoss:0086", "SetSpline:0086",
                    "ForceEnterCoolDown:00c6", "InvokeFinalHitReadiness:0081", "OnDestroy:00c6",
                    ".ctor:1886", "<Hit>g__HitBossAndStagger|39_0:0081", "<Hit>g__CheckIfTrackAndExplode|39_1:0081",
                    "<Hit>g__HitBossDealDamageAndBounceOff|39_2:0081", "<Hit>g__HitIfNotHomingAttack|39_3:0081",
                    "<ReturnProjectile>g__ReturnArc|48_0:0081", "<ReturnProjectile>g__ReturnStraight|48_1:0081",
                }));
        }

        [Test]
        public void GuardianProjectileNestedTypesKeepTheOriginalShape()
        {
            Type state = typeof(State);
            Assert.That((int)state.Attributes, Is.EqualTo(0x00000102));
            Assert.That(Enum.GetNames(state), Is.EqualTo(new[]
            {
                "FiredAsArc", "ReturningAsArc", "FiredStraight", "ReturningStraight", "FiredOverhead",
                "ReturningFromOverhead", "MoveToResting", "Resting", "ExplodingInPlace",
            }));
            Assert.That(Enum.GetValues(state).Cast<object>().Select(value => Convert.ToInt32(value)), Is.EqualTo(Enumerable.Range(0, 9)));

            Type closure = typeof(GuardianProjectile).GetNestedType("<>c__DisplayClass39_0", BindingFlags.NonPublic);
            Assert.That(closure, Is.Not.Null);
            Assert.That((int)closure.Attributes, Is.EqualTo(0x00100103));
            Assert.That(closure.BaseType, Is.EqualTo(typeof(System.ValueType)));
            Assert.That(closure.GetFields(Declared).OrderBy(field => field.MetadataToken)
                .Select(field => field.Name + ":" + field.FieldType.Name + ":" + ((int)field.Attributes).ToString("x4")).ToArray(),
                Is.EqualTo(new[] { "<>4__this:GuardianProjectile:0006", "hitCollider:Collider:0006" }));
        }

        // The constructor (0x57C9C0) stores a zero arc centre, then two empty tracking collections of its own, and
        // runs the Projectile field initialisers (state Expired, four ray-hit slots).
        [Test]
        public void ConstructorBuildsItsOwnEmptyTrackingCollections()
        {
            WithBoss("lucid-guardian-ctor", (owned, rig) =>
            {
                GuardianProjectile projectile = rig.Rig.Projectile;
                var surfaces = (Dictionary<ISurface, ISurface[]>)Get(projectile, "m_trackableSurfaces");
                var list = (List<ISurface>)Get(projectile, "m_trackableSurfacesList");
                Assert.That(surfaces, Is.Empty);
                Assert.That(list, Is.Empty);
                GuardianProjectile other = rig.Rig.Owned.Host("Lucid second guardian projectile", false).AddComponent<GuardianProjectile>();
                Assert.That(Get(other, "m_trackableSurfaces"), Is.Not.SameAs(surfaces), "each projectile builds its own");
                Assert.That(Get(other, "m_trackableSurfacesList"), Is.Not.SameAs(list));
                Assert.That(Vec(Get(projectile, "m_centerOfCircle")), Is.EqualTo(Vector3.zero));
                Assert.That(BaseState(projectile), Is.EqualTo(3));
                Assert.That(((Array)Get(projectile, "m_raycastHits")).Length, Is.EqualTo(4));
            });
        }

        // ---- simple setters and aiming ----

        // 0x57C100, 0x57C180 (two different rates), 0x57C1A0, 0x57C530.
        [Test]
        public void SettersStoreTheirValuesInTheirOwnFields()
        {
            WithBoss("lucid-guardian-setters", (owned, rig) =>
            {
                GuardianProjectile projectile = rig.Rig.Projectile;
                projectile.SetArcCenter(new Vector3(1f, 2f, 3f));
                projectile.SetProgressionRates(0.5f, 0.75f);
                projectile.SetCullingValues(2.5f);
                projectile.SetBoss(null);
                Assert.That(Vec(Get(projectile, "m_centerOfCircle")), Is.EqualTo(new Vector3(1f, 2f, 3f)));
                Assert.That(Get(projectile, "m_travelSpeedRadians"), Is.EqualTo(0.5f));
                Assert.That(Get(projectile, "m_returningRadians"), Is.EqualTo(0.75f));
                Assert.That(Get(projectile, "m_cullTimeOnceBehindPlayer"), Is.EqualTo(2.5f));
                Assert.That(Get(projectile, "m_boss") == null, Is.True);
                projectile.SetBoss(rig.Boss);
                Assert.That(Get(projectile, "m_boss"), Is.SameAs(rig.Boss));
            });
        }

        // 0x57A930: the direction is normalised and scaled by the definition's velocity (+0x28); the rotation looks
        // along it; the target at the projectile's own position gives a zero velocity and no rotation.
        [Test]
        public void UpdateFocusAimsAtTheTargetAtTheDefinitionVelocity()
        {
            WithBoss("lucid-guardian-focus", (owned, rig) =>
            {
                GuardianProjectile projectile = rig.Rig.Projectile;
                projectile.transform.position = new Vector3(1f, 2f, 3f);
                projectile.UpdateFocus(new Vector3(4f, 6f, 3f));
                WorldMemberAccess.AreClose(new Vector3(6f, 8f, 0f), Vec(Get(projectile, "m_straightVelocity")), "velocity");
                WorldMemberAccess.AreClose(new Vector3(0.6f, 0.8f, 0f), projectile.transform.forward, "facing");

                projectile.UpdateFocus(new Vector3(1f, 2f, 3f));
                Assert.That(Vec(Get(projectile, "m_straightVelocity")), Is.EqualTo(Vector3.zero));
                Assert.That(Quaternion.Angle(projectile.transform.rotation, Quaternion.identity), Is.LessThan(1e-3f));
            });
        }

        // 0x57C120: unlike UpdateFocus the direction is used as given, without normalising.
        [Test]
        public void SetTargetDirectionScalesTheDirectionWithoutNormalising()
        {
            WithBoss("lucid-guardian-direction", (owned, rig) =>
            {
                GuardianProjectile projectile = rig.Rig.Projectile;
                projectile.SetTargetDirection(new Vector3(0f, 3f, 4f));
                Assert.That(Vec(Get(projectile, "m_straightVelocity")), Is.EqualTo(new Vector3(0f, 30f, 40f)));
                WorldMemberAccess.AreClose(new Vector3(0f, 0.6f, 0.8f), projectile.transform.forward, "facing");
            });
        }

        [Test]
        public void ShouldTeleportToHitPointIsAlwaysFalse()
        {
            WithBoss("lucid-guardian-teleport", (owned, rig) =>
            {
                foreach (State state in Enum.GetValues(typeof(State)))
                {
                    SetMovement(rig.Rig.Projectile, state);
                    Assert.That(Call(rig.Rig.Projectile, "ShouldTeleportToHitPoint"), Is.False, state.ToString());
                }
            });
        }

        // ---- movement ----

        // 0x57AA70: Resting, ExplodingInPlace and anything past them (0x57ac68) return the zero vector and move nothing.
        [Test]
        public void RestingAndExplodingProjectilesDoNotMove()
        {
            WithBoss("lucid-guardian-still", (owned, rig) =>
            {
                GuardianProjectile projectile = rig.Rig.Projectile;
                projectile.transform.position = new Vector3(1f, 2f, 3f);
                projectile.SetTargetDirection(Vector3.forward); // velocity (0, 0, 10): the definition velocity is 10 (0x57C120)
                foreach (State state in new[] { State.Resting, State.ExplodingInPlace, (State)9 })
                {
                    SetMovement(projectile, state);
                    Assert.That(Vec(Call(projectile, "DoMovement", 0.5f)), Is.EqualTo(Vector3.zero), state.ToString());
                    Assert.That(projectile.transform.position, Is.EqualTo(new Vector3(1f, 2f, 3f)), state.ToString());
                }
            });
        }

        // 0x57B080 / 0x57AAFB: velocity times the step is applied in world space (0 is Space.World), whatever the facing.
        [Test]
        public void StraightMovementTranslatesInWorldSpaceByTheVelocityTimesTheStep()
        {
            WithCharacter("lucid-guardian-straight", (owned, rig) =>
            {
                GuardianProjectile projectile = rig.Rig.Projectile;
                Character character = rig.Character;
                character.SetWorldPosition(new Vector3(0f, 0f, 1000f));
                projectile.SetTargetDirection(Vector3.forward); // velocity (0, 0, 10): the definition velocity is 10 (0x57C120)
                projectile.transform.rotation = Quaternion.Euler(0f, 90f, 0f); // the facing is independent of the velocity
                projectile.transform.position = new Vector3(1f, 2f, 3f);
                foreach (State state in new[] { State.FiredStraight, State.ReturningStraight })
                {
                    projectile.transform.position = new Vector3(1f, 2f, 3f);
                    SetMovement(projectile, state);
                    Assert.That(Vec(Call(projectile, "DoMovement", 0.5f)), Is.EqualTo(new Vector3(0f, 0f, 5f)), state.ToString());
                    Assert.That(projectile.transform.position, Is.EqualTo(new Vector3(1f, 2f, 8f)), state.ToString());
                }
            });
        }

        // 0x57AB4D..0x57ABB0 (and the same test at 0x57B0D8..0x57B148): after a fired straight or arc move, a dot
        // product of (position - character) with the movement that is not below zero (ordered ucomiss/ja at 0x57AB9C) starts
        // the cull: the flag is set and the remaining life becomes the cull time. Zero counts, and so does NaN.
        [Test]
        public void StraightMovementStartsTheCullWhenTheDotProductIsNotBelowZero()
        {
            WithCharacter("lucid-guardian-cull", (owned, rig) =>
            {
                GuardianProjectile projectile = rig.Rig.Projectile;
                Character character = rig.Character;
                projectile.SetCullingValues(0.75f);
                SetMovement(projectile, State.FiredStraight);

                (Vector3 characterAt, Vector3 direction, float step, bool culled, string label)[] cases =
                {
                    (Vector3.zero, Vector3.forward, 1f, true, "moving away from the character"),
                    (Vector3.zero, Vector3.back, 0.5f, false, "moving towards the character"),
                    (Vector3.zero, Vector3.forward, 0f, true, "a zero dot product"),
                    (new Vector3(float.PositiveInfinity, 0f, 0f), Vector3.forward, 0f, true, "an unordered (NaN) dot product"),
                };
                foreach (var testCase in cases)
                {
                    character.SetWorldPosition(testCase.characterAt);
                    projectile.transform.position = new Vector3(0f, 0f, 10f);
                    projectile.SetTargetDirection(testCase.direction);
                    Set(projectile, "m_behindPlayerSoPreparingToDie", false);
                    SetLife(projectile, 4f);
                    Call(projectile, "DoMovement", testCase.step);
                    Assert.That(Get(projectile, "m_behindPlayerSoPreparingToDie"), Is.EqualTo(testCase.culled), testCase.label);
                    Assert.That(Life(projectile), Is.EqualTo(testCase.culled ? 0.75f : 4f), testCase.label);
                }
            });
        }

        // The cull is skipped once it has started (the life is not restarted), without a valid current character,
        // and for every returning movement.
        [Test]
        public void StraightMovementSkipsTheCullWhenAlreadyStartedWithoutACharacterOrWhileReturning()
        {
            WithCharacter("lucid-guardian-no-cull", (owned, rig) =>
            {
                GuardianProjectile projectile = rig.Rig.Projectile;
                rig.Character.SetWorldPosition(Vector3.zero);
                projectile.SetCullingValues(0.75f);
                projectile.SetTargetDirection(Vector3.forward); // velocity (0, 0, 10): the definition velocity is 10 (0x57C120)
                projectile.transform.position = new Vector3(0f, 0f, 10f);

                SetMovement(projectile, State.FiredStraight);
                Set(projectile, "m_behindPlayerSoPreparingToDie", true);
                SetLife(projectile, 1.25f);
                Call(projectile, "DoMovement", 1f);
                Assert.That(Life(projectile), Is.EqualTo(1.25f), "already started");

                Set(projectile, "m_behindPlayerSoPreparingToDie", false);
                CharacterTriggerTestSupport.SetCurrent(rig.Manager, rig.Character, false);
                Call(projectile, "DoMovement", 1f);
                Assert.That(Get(projectile, "m_behindPlayerSoPreparingToDie"), Is.False, "no valid character");
                CharacterTriggerTestSupport.SetCurrent(rig.Manager, rig.Character, true);

                SetMovement(projectile, State.ReturningStraight);
                Call(projectile, "DoMovement", 1f);
                Assert.That(Get(projectile, "m_behindPlayerSoPreparingToDie"), Is.False, "returning movement never culls");
                Assert.That(Life(projectile), Is.EqualTo(1.25f));
            });
        }

        // 0x57ACA0 outbound (0x57AEE8): the arc angle advances by the travel rate times the step, anticlockwise
        // (positive) unless clockwise; the point is centre + (sin, cos) * radius at the projectile's own height.
        [Test]
        public void OutboundArcMovementAdvancesTheAngleAgainstTheClockwiseFlag()
        {
            WithCharacter("lucid-guardian-arc", (owned, rig) =>
            {
                GuardianProjectile projectile = rig.Rig.Projectile;
                projectile.SetArcCenter(Vector3.zero);
                projectile.SetProgressionRates(Mathf.PI / 2f, 0.25f);
                Set(projectile, "m_radiusOfCircle", 5f);
                SetMovement(projectile, State.FiredAsArc);

                foreach (bool clockwise in new[] { false, true })
                {
                    Set(projectile, "m_movementIsClockwise", clockwise);
                    Set(projectile, "m_progressionArcRadians", 0f);
                    projectile.transform.position = new Vector3(0f, 3f, 5f);
                    var movement = Vec(Call(projectile, "DoMovement", 1f));
                    float side = clockwise ? -5f : 5f;
                    WorldMemberAccess.AreClose(new Vector3(side, 3f, 0f), projectile.transform.position, "position, clockwise " + clockwise);
                    WorldMemberAccess.AreClose(new Vector3(side, 0f, -5f), movement, "movement, clockwise " + clockwise);
                    WorldMemberAccess.AreClose(new Vector3(0f, 3f, 5f), Vec(Get(projectile, "m_previousFramePosition")), "previous position");
                    Assert.That((float)Get(projectile, "m_progressionArcRadians"), Is.EqualTo((clockwise ? -1f : 1f) * Mathf.PI / 2f).Within(1e-6f));
                }
            });
        }

        // The outbound arc runs the same behind-player test as the straight movement (0x57AFD0..0x57B062).
        [Test]
        public void OutboundArcMovementStartsTheCullWhenItMovesAwayFromTheCharacter()
        {
            WithCharacter("lucid-guardian-arc-cull", (owned, rig) =>
            {
                GuardianProjectile projectile = rig.Rig.Projectile;
                projectile.SetArcCenter(Vector3.zero);
                projectile.SetProgressionRates(Mathf.PI / 2f, 0.25f);
                projectile.SetCullingValues(0.75f);
                Set(projectile, "m_radiusOfCircle", 5f);
                SetMovement(projectile, State.FiredAsArc);
                foreach ((Vector3 characterAt, bool culled) in new[] { (Vector3.zero, true), (new Vector3(10f, 0f, 0f), false) })
                {
                    rig.Character.SetWorldPosition(characterAt);
                    Set(projectile, "m_progressionArcRadians", 0f);
                    Set(projectile, "m_behindPlayerSoPreparingToDie", false);
                    SetLife(projectile, 4f);
                    projectile.transform.position = new Vector3(0f, 0f, 5f);
                    Call(projectile, "DoMovement", 1f);
                    Assert.That(Get(projectile, "m_behindPlayerSoPreparingToDie"), Is.EqualTo(culled), characterAt.ToString());
                    Assert.That(Life(projectile), Is.EqualTo(culled ? 0.75f : 4f), characterAt.ToString());
                }
            });
        }

        // 0x57ACC8 returning: the angle runs the other way (clockwise adds), and the radius and height blend from where
        // the projectile turned back to the return point by the fraction of the starting squared distance already
        // covered, clamped to 0..1. The return point (0, 9, 12) is 15 from the origin; the projectile is 85 (squared) from it.
        [Test]
        public void ReturningArcMovementSpiralsInTowardsTheReturnPoint()
        {
            WithBoss("lucid-guardian-arc-return", (owned, rig) =>
            {
                GuardianProjectile projectile = rig.Rig.Projectile;
                Transform returnPoint = owned.Host("Lucid return point", true).transform;
                returnPoint.position = new Vector3(0f, 9f, 12f);
                projectile.SetArcCenter(Vector3.zero);
                projectile.SetProgressionRates(1f, 0.5f);
                Set(projectile, "m_radiusOfCircle", 5f);
                Set(projectile, "m_cachedY", 3f);
                Set(projectile, "m_returnTransform", returnPoint);
                SetMovement(projectile, State.ReturningAsArc);

                (bool clockwise, float startingDistanceSq, float radius, float height, string label)[] cases =
                {
                    (true, 170f, 10f, 6f, "half way"),
                    (false, 170f, 10f, 6f, "half way anticlockwise"),
                    (true, 40f, 5f, 3f, "progress below zero is clamped to the start"),
                    (true, -10f, 15f, 9f, "progress above one is clamped to the return point"),
                };
                foreach (var testCase in cases)
                {
                    Set(projectile, "m_movementIsClockwise", testCase.clockwise);
                    Set(projectile, "m_progressionArcRadians", 0f);
                    Set(projectile, "m_startingDistanceSq", testCase.startingDistanceSq);
                    projectile.transform.position = new Vector3(0f, 3f, 5f);
                    var movement = Vec(Call(projectile, "DoMovement", 1f));
                    float angle = testCase.clockwise ? 0.5f : -0.5f;
                    var expected = new Vector3((float)(Math.Sin(angle) * testCase.radius), testCase.height, (float)(Math.Cos(angle) * testCase.radius));
                    WorldMemberAccess.AreClose(expected, projectile.transform.position, testCase.label);
                    WorldMemberAccess.AreClose(expected - new Vector3(0f, 3f, 5f), movement, testCase.label + " movement");
                    Assert.That((float)Get(projectile, "m_currentDistanceSq"), Is.EqualTo(85f).Within(1e-3f), testCase.label);
                    Assert.That((float)Get(projectile, "m_finalRadius"), Is.EqualTo(15f).Within(1e-4f), testCase.label);
                }
            });
        }

        // 0x57ACF5 takes the squared distance in the return transform's local space (InverseTransformPoint), and
        // 0x57AD5B..0x57ADE2 takes the final radius as the plain magnitude of the return point's world position, with no
        // arc-centre subtraction. The basic case above has an identity return transform and a zero arc centre, where the
        // world-space distance and the centre-relative radius would agree.
        [Test]
        public void ReturningArcMovementMeasuresInTheReturnTransformsSpaceAndAgainstTheOrigin()
        {
            WithBoss("lucid-guardian-arc-return-space", (owned, rig) =>
            {
                GuardianProjectile projectile = rig.Rig.Projectile;
                (Vector3 centre, float scale, float distanceSq, float radius, float height, string label)[] cases =
                {
                    // Local offset (0, -3, -3.5): 21.25; progress (170 - 21.25) / 170 = 0.875.
                    (Vector3.zero, 2f, 21.25f, 13.75f, 8.25f, "scaled return transform"),
                    // World offset (0, -6, -7): 85; progress 0.5; the radius runs 5 -> 15 even though the centre is not the origin.
                    (new Vector3(3f, 0f, 4f), 1f, 85f, 10f, 6f, "offset arc centre"),
                };
                foreach (var testCase in cases)
                {
                    Transform returnPoint = owned.Host("Lucid return point " + testCase.label, true).transform;
                    returnPoint.position = new Vector3(0f, 9f, 12f);
                    returnPoint.localScale = Vector3.one * testCase.scale;
                    projectile.SetArcCenter(testCase.centre);
                    projectile.SetProgressionRates(1f, 0.5f);
                    Set(projectile, "m_radiusOfCircle", 5f);
                    Set(projectile, "m_cachedY", 3f);
                    Set(projectile, "m_returnTransform", returnPoint);
                    Set(projectile, "m_movementIsClockwise", true);
                    Set(projectile, "m_progressionArcRadians", 0f);
                    Set(projectile, "m_startingDistanceSq", 170f);
                    SetMovement(projectile, State.ReturningAsArc);
                    projectile.transform.position = new Vector3(0f, 3f, 5f);

                    Call(projectile, "DoMovement", 1f);

                    var expected = new Vector3(testCase.centre.x + (float)Math.Sin(0.5) * testCase.radius, testCase.height,
                        testCase.centre.z + (float)Math.Cos(0.5) * testCase.radius);
                    Assert.That((float)Get(projectile, "m_currentDistanceSq"), Is.EqualTo(testCase.distanceSq).Within(1e-3f), testCase.label);
                    Assert.That((float)Get(projectile, "m_finalRadius"), Is.EqualTo(15f).Within(1e-4f), testCase.label);
                    WorldMemberAccess.AreClose(expected, projectile.transform.position, testCase.label);
                }
            });
        }

        // 0x57ABED: heading for the return point from above, the projectile re-aims at it every step (UpdateFocus) and
        // then moves by velocity times the step.
        [Test]
        public void ReturningFromOverheadReaimsAtTheReturnPointBeforeMoving()
        {
            WithBoss("lucid-guardian-overhead-return", (owned, rig) =>
            {
                GuardianProjectile projectile = rig.Rig.Projectile;
                Transform returnPoint = owned.Host("Lucid overhead return point", true).transform;
                returnPoint.position = new Vector3(0f, 0f, 10f);
                projectile.transform.position = Vector3.zero;
                Set(projectile, "m_returnTransform", returnPoint);
                projectile.SetTargetDirection(Vector3.right); // velocity (10, 0, 0)
                SetMovement(projectile, State.ReturningFromOverhead);
                var movement = Vec(Call(projectile, "DoMovement", 0.5f));
                WorldMemberAccess.AreClose(new Vector3(0f, 0f, 5f), movement, "movement");
                WorldMemberAccess.AreClose(new Vector3(0f, 0f, 5f), projectile.transform.position, "position");
                WorldMemberAccess.AreClose(new Vector3(0f, 0f, 10f), Vec(Get(projectile, "m_straightVelocity")), "velocity");
            });
        }

        // 0x57B160: the elapsed time accumulates across calls; a progress of exactly one still moves, and only more than
        // one (ordered compare, 0x57B1DF) arrives; NaN progress does not arrive. Movement is new position minus the
        // position on entry, measured from the start position rather than the current one.
        [Test]
        public void LerpMovementEasesFromTheStartPositionAndArrivesPastOne()
        {
            WithBoss("lucid-guardian-lerp", (owned, rig) =>
            {
                GuardianProjectile projectile = rig.Rig.Projectile;
                Transform resting = owned.Host("Lucid resting point", true).transform;
                resting.position = new Vector3(10f, 0f, 0f);
                int[] arrived = CountEvent(projectile, "m_onArriveAtRestingPlace");
                Set(projectile, "m_lerpMovementTime", 2f);
                Set(projectile, "m_lerpEasing", EaseUtilities.EaseType.NONE);
                Set(projectile, "m_startLerpMovementPosition", Vector3.zero);
                Set(projectile, "m_finalRestingTransform", resting);
                SetMovement(projectile, State.MoveToResting);
                projectile.transform.position = new Vector3(1f, 0f, 0f);

                Assert.That(Vec(Call(projectile, "DoMovement", 1f)), Is.EqualTo(new Vector3(4f, 0f, 0f)));
                Assert.That(projectile.transform.position, Is.EqualTo(new Vector3(5f, 0f, 0f)));
                Assert.That(Get(projectile, "m_lerpTime"), Is.EqualTo(1f));

                Call(projectile, "DoMovement", 1f);
                Assert.That(projectile.transform.position, Is.EqualTo(new Vector3(10f, 0f, 0f)), "progress of exactly one reaches the resting point");
                Assert.That(Get(projectile, "m_movementState"), Is.EqualTo(State.MoveToResting));
                Assert.That(arrived[0], Is.EqualTo(0));

                Assert.That(Vec(Call(projectile, "DoMovement", 0.01f)), Is.EqualTo(Vector3.zero));
                Assert.That(Get(projectile, "m_movementState"), Is.EqualTo(State.Resting));
                Assert.That(arrived[0], Is.EqualTo(1));
                Assert.That(projectile.transform.position, Is.EqualTo(new Vector3(10f, 0f, 0f)));
            });
        }

        [Test]
        public void LerpMovementAppliesTheEasingToTheProgress()
        {
            WithBoss("lucid-guardian-lerp-ease", (owned, rig) =>
            {
                GuardianProjectile projectile = rig.Rig.Projectile;
                Transform resting = owned.Host("Lucid eased resting point", true).transform;
                resting.position = new Vector3(8f, 0f, 0f);
                Set(projectile, "m_lerpMovementTime", 2f);
                Set(projectile, "m_lerpEasing", EaseUtilities.EaseType.InQuad);
                Set(projectile, "m_finalRestingTransform", resting);
                SetMovement(projectile, State.MoveToResting);
                projectile.transform.position = Vector3.zero;
                Call(projectile, "DoMovement", 1f);
                Assert.That(projectile.transform.position, Is.EqualTo(new Vector3(2f, 0f, 0f)), "0.5 eased to 0.25 of eight");
            });
        }

        // With a zero lerp time and a zero step the progress is NaN: it is not greater than one, so the projectile does
        // not arrive and the lerp runs on the NaN (the engine refuses the NaN position and logs it).
        [Test]
        public void LerpMovementWithNaNProgressDoesNotArrive()
        {
            WithBoss("lucid-guardian-lerp-nan", (owned, rig) =>
            {
                GuardianProjectile projectile = rig.Rig.Projectile;
                Transform resting = owned.Host("Lucid nan resting point", true).transform;
                int[] arrived = CountEvent(projectile, "m_onArriveAtRestingPlace");
                Set(projectile, "m_lerpMovementTime", 0f);
                Set(projectile, "m_lerpEasing", EaseUtilities.EaseType.NONE);
                Set(projectile, "m_finalRestingTransform", resting);
                SetMovement(projectile, State.MoveToResting);
                LogAssert.Expect(LogType.Error, new Regex("assign attempt"));
                Call(projectile, "DoMovement", 0f);
                Assert.That(Get(projectile, "m_movementState"), Is.EqualTo(State.MoveToResting));
                Assert.That(arrived[0], Is.EqualTo(0));
            });
        }

        // Arrival with no authored event only changes the state (0x57B1FC tests the event for null).
        [Test]
        public void LerpMovementArrivesWithoutAnAuthoredEvent()
        {
            WithBoss("lucid-guardian-lerp-no-event", (owned, rig) =>
            {
                GuardianProjectile projectile = rig.Rig.Projectile;
                Transform resting = owned.Host("Lucid no event resting point", true).transform;
                Set(projectile, "m_onArriveAtRestingPlace", null);
                Set(projectile, "m_lerpMovementTime", 1f);
                Set(projectile, "m_lerpEasing", EaseUtilities.EaseType.NONE);
                Set(projectile, "m_finalRestingTransform", resting);
                SetMovement(projectile, State.MoveToResting);
                Assert.That(Vec(Call(projectile, "DoMovement", 2f)), Is.EqualTo(Vector3.zero));
                Assert.That(Get(projectile, "m_movementState"), Is.EqualTo(State.Resting));
            });
        }

        // ---- cooling down and colliders ----

        // 0x57C810: with exceptions allowed, a projectile that is returning as an arc (1), returning from overhead (5),
        // moving to rest (6) or resting (7) keeps going (mask 0xE2 at 0x57C823); otherwise the base rule applies
        // (0x57C82D: nothing happens once CoolingDown or Expired, else it cools down and tells its event).
        [Test]
        public void ForceEnterCoolDownLeavesReturningAndRestingProjectilesWhenExceptionsAreAllowed()
        {
            WithBoss("lucid-guardian-cooldown", (owned, rig) =>
            {
                GuardianProjectile projectile = rig.Rig.Projectile;
                var exempt = new HashSet<State> { State.ReturningAsArc, State.ReturningFromOverhead, State.MoveToResting, State.Resting };
                foreach (State movement in Enum.GetValues(typeof(State)))
                {
                    foreach (bool allow in new[] { false, true })
                    {
                        foreach (int baseState in new[] { 0, 1, 2, 3 })
                        {
                            SetMovement(projectile, movement);
                            SetBaseState(projectile, baseState);
                            int before = rig.Rig.Cooled[0];
                            projectile.ForceEnterCoolDown(allow);
                            bool unchanged = (allow && exempt.Contains(movement)) || baseState >= 2;
                            string label = movement + ", exceptions " + allow + ", state " + baseState;
                            Assert.That(BaseState(projectile), Is.EqualTo(unchanged ? baseState : 2), label);
                            Assert.That(rig.Rig.Cooled[0] - before, Is.EqualTo(unchanged ? 0 : 1), label);
                        }
                    }
                }
            });
        }

        // 0x57BB10: only the returning arc and straight states (1 and 3) ignore the player's collision collider, and
        // nothing else (not the trigger collider, not another collider); without a valid character nothing is ignored.
        [Test]
        public void OnlyReturningProjectilesIgnoreThePlayersCollisionCollider()
        {
            WithCharacter("lucid-guardian-ignore", (owned, rig) =>
            {
                GuardianProjectile projectile = rig.Rig.Projectile;
                var trigger = rig.Character.gameObject.AddComponent<SphereCollider>();
                Set(rig.Character, "m_colliderTrigger", trigger);
                Collider other = owned.Host("Lucid other collider", false).AddComponent<BoxCollider>();
                var ignoring = new HashSet<State> { State.ReturningAsArc, State.ReturningStraight };
                foreach (State state in Enum.GetValues(typeof(State)))
                {
                    SetMovement(projectile, state);
                    Assert.That(Call(projectile, "ShouldIgnoreCollider", rig.PlayerCollider), Is.EqualTo(ignoring.Contains(state)), state + " player");
                    Assert.That(Call(projectile, "ShouldIgnoreCollider", trigger), Is.False, state + " trigger");
                    Assert.That(Call(projectile, "ShouldIgnoreCollider", other), Is.False, state + " other");
                }
                SetMovement(projectile, State.ReturningStraight);
                CharacterTriggerTestSupport.SetCurrent(rig.Manager, rig.Character, false);
                Assert.That(Call(projectile, "ShouldIgnoreCollider", rig.PlayerCollider), Is.False, "no valid character");
                SetMovement(projectile, (State)9);
                CharacterTriggerTestSupport.SetCurrent(rig.Manager, rig.Character, true);
                Assert.That(Call(projectile, "ShouldIgnoreCollider", rig.PlayerCollider), Is.False, "past the last state");
            });
        }

        // ---- hits ----

        private static void SetInUse(Character character, params ActorAbilityType[] types)
        {
            var abilities = new Dictionary<ActorAbilityType, ActorAbilityInUseData>(HardlightProject.HardlightEnumComparers.ActorAbilityTypeComparer);
            foreach (ActorAbilityType type in types) abilities[type] = new ActorAbilityInUseData { InUse = true };
            ((FSMStorage)character.Storage).SetValue(ActorFSMKeys.AbilitiesInUse, abilities);
        }

        // 0x57B4A0 and 0x57B610: while fired, a hit on something that is not the player does nothing; on the player it
        // stops at once while a homing attack (0x95E16C42, the air target one) is in use, and for the two returnable
        // projectile types (0xFC29F697 and 0xB1F6AFB2) also while a ground running or rolling homing attack or the ground
        // boost (0x1BD0951C, 0x69542F0A, 0x5AB40B5B) is in use. The states from the resting move on do not react at all.
        [Test]
        public void FiredProjectilesDoNotHurtThePlayerWhileAHomingAttackIsInUse()
        {
            WithCharacter("lucid-guardian-hit-guards", (owned, rig) =>
            {
                GuardianProjectile projectile = rig.Rig.Projectile;
                Collider other = owned.Host("Lucid hit bystander", false).AddComponent<BoxCollider>();
                void Hit(Collider collider, string label, ProjectileType type, State state, params ActorAbilityType[] inUse)
                {
                    Set(rig.Rig.Definition, "m_projectileType", type);
                    SetMovement(projectile, state);
                    SetBaseState(projectile, 1);
                    SetInUse(rig.Character, inUse);
                    Call(projectile, "Hit", collider);
                    Assert.That(BaseState(projectile), Is.EqualTo(1), label);
                    Assert.That(rig.Rig.Cooled[0], Is.EqualTo(0), label);
                }

                foreach (State state in new[] { State.FiredAsArc, State.FiredStraight, State.FiredOverhead })
                {
                    Hit(other, state + " bystander", ProjectileType.GuardianKillerProjectile, state);
                    Hit(rig.PlayerCollider, state + " air homing", ProjectileType.GuardianKillerProjectile, state, ActorAbilityType.Character_HomingAttackAirTarget);
                    foreach (ProjectileType returnable in new[] { ProjectileType.GuardianReturnableProjectile, ProjectileType.GuardianReturnableStraightProjectile })
                    {
                        Hit(rig.PlayerCollider, state + " " + returnable + " air homing", returnable, state, ActorAbilityType.Character_HomingAttackAirTarget);
                        Hit(rig.PlayerCollider, state + " " + returnable + " ground running", returnable, state, ActorAbilityType.Character_HomingAttackGroundRunning);
                        Hit(rig.PlayerCollider, state + " " + returnable + " ground rolling", returnable, state, ActorAbilityType.Character_HomingAttackGroundRolling);
                        Hit(rig.PlayerCollider, state + " " + returnable + " ground boost", returnable, state, ActorAbilityType.Character_MovementGroundBoost);
                    }
                }
                foreach (State state in new[] { State.MoveToResting, State.Resting, State.ExplodingInPlace })
                    Hit(rig.PlayerCollider, state + " ignores the player", ProjectileType.GuardianKillerProjectile, state);
                LogAssert.NoUnexpectedReceived();
            });
        }

        // 0x57B7D0: returning as an arc or straight, a hit on a boss staggers it and cools the projectile down; a hit on
        // anything else does nothing.
        [Test]
        public void ReturningProjectilesStaggerTheBossTheyHit()
        {
            WithBoss("lucid-guardian-hit-boss", (owned, rig) =>
            {
                GuardianProjectile projectile = rig.Rig.Projectile;
                Collider other = owned.Host("Lucid hit bystander", false).AddComponent<BoxCollider>();
                var storage = (FSMStorage)rig.Boss.Storage;
                foreach (State state in new[] { State.ReturningAsArc, State.ReturningStraight })
                {
                    storage.SetValue(BossFSMKeys.WasHit, false);
                    SetMovement(projectile, state);
                    SetBaseState(projectile, 1);
                    Call(projectile, "Hit", other);
                    Assert.That(storage.GetValue(BossFSMKeys.WasHit, false, false), Is.False, state + " bystander");
                    Assert.That(BaseState(projectile), Is.EqualTo(1));
                    Call(projectile, "Hit", rig.BossCollider);
                    Assert.That(storage.GetValue(BossFSMKeys.WasHit, false, false), Is.True, state + " boss");
                    Assert.That(BaseState(projectile), Is.EqualTo(2));
                }
                Assert.That(rig.Rig.Cooled[0], Is.EqualTo(2));
            });
        }

        // 0x57B820: returning from overhead, the first hit on a boss takes a rest point, subscribes to the guardian
        // manager's final-hit event, remembers where it started, moves to rest for ever (infinite life), hits the boss
        // and tells its event. A second hit does nothing. Without a free rest point the projectile is killed instead.
        [Test]
        public void ReturningFromOverheadBouncesOffTheBossIntoARestPoint()
        {
            WithBoss("lucid-guardian-bounce", (owned, rig) =>
            {
                GuardianProjectile projectile = rig.Rig.Projectile;
                Transform returnPoint = owned.Host("Lucid bounce return point", true).transform;
                returnPoint.position = new Vector3(3f, 4f, 5f);
                Set(projectile, "m_returnTransform", returnPoint);
                rig.Manager.RestoreHealthToPool(5);
                int[] phase4 = CountEvent(projectile, "m_onHitBossPhase4");
                int[] ready = CountEvent(projectile, "m_onBossReadyForFinalHit");
                SetMovement(projectile, State.ReturningFromOverhead);
                SetLife(projectile, 1f);

                Call(projectile, "Hit", owned.Host("Lucid bounce bystander", false).AddComponent<BoxCollider>());
                Assert.That(Get(projectile, "m_hasHitBoss"), Is.False, "not a boss");

                Call(projectile, "Hit", rig.BossCollider);
                Assert.That(Get(projectile, "m_hasHitBoss"), Is.True);
                Assert.That(Get(projectile, "m_movementState"), Is.EqualTo(State.MoveToResting));
                Assert.That(Get(projectile, "m_finalRestingTransform"), Is.SameAs(rig.RestPoints[0]));
                Assert.That(Vec(Get(projectile, "m_startLerpMovementPosition")), Is.EqualTo(new Vector3(3f, 4f, 5f)));
                Assert.That(float.IsPositiveInfinity(Life(projectile)), Is.True);
                Assert.That(rig.Manager.BossHealthCurrent, Is.EqualTo(4), "the boss took the hit");
                Assert.That(phase4[0], Is.EqualTo(1));
                rig.Guardian.BossReadyForFinalHit();
                Assert.That(ready[0], Is.EqualTo(1), "subscribed to the final hit");

                Call(projectile, "Hit", rig.BossCollider);
                Assert.That(phase4[0], Is.EqualTo(1), "a second hit does nothing");
                Assert.That(rig.Manager.BossHealthCurrent, Is.EqualTo(4));
            });
        }

        [Test]
        public void ReturningFromOverheadWithNoFreeRestPointKillsTheProjectile()
        {
            WithBoss("lucid-guardian-bounce-full", (owned, rig) =>
            {
                GuardianProjectile projectile = rig.Rig.Projectile;
                Set(rig.Guardian, "m_eggpawnRestPoints", new GuardianManager.EggpawnRestPoints[0]);
                rig.Manager.RestoreHealthToPool(5);
                int[] ready = CountEvent(projectile, "m_onBossReadyForFinalHit");
                int[] expired = new int[1];
                projectile.OnExpired = hit => expired[0]++;
                SetMovement(projectile, State.ReturningFromOverhead);
                SetBaseState(projectile, 1);
                Call(projectile, "Hit", rig.BossCollider);
                Assert.That(Get(projectile, "m_hasHitBoss"), Is.True);
                Assert.That(BaseState(projectile), Is.EqualTo(3), "killed");
                Assert.That(expired[0], Is.EqualTo(1));
                Assert.That(Get(projectile, "m_movementState"), Is.EqualTo(State.ReturningFromOverhead));
                Assert.That(rig.Manager.BossHealthCurrent, Is.EqualTo(5), "the boss was not hit");
                rig.Guardian.BossReadyForFinalHit();
                Assert.That(ready[0], Is.EqualTo(0), "never subscribed");
            });
        }

        // 0x57B565: fired overhead, a hit on the Track layer explodes in place; only the killer straight projectile also cools
        // down (0xC34F9189), and a hit on another layer does nothing.
        [Test]
        public void OverheadProjectilesExplodeInPlaceOnTheTrack()
        {
            WithCharacter("lucid-guardian-track", (owned, rig) =>
            {
                GuardianProjectile projectile = rig.Rig.Projectile;
                var track = owned.Host("Lucid track collider", false);
                track.layer = LayerMask.NameToLayer("Track");
                Collider onTrack = track.AddComponent<BoxCollider>();
                Collider elsewhere = owned.Host("Lucid default layer collider", false).AddComponent<BoxCollider>();
                foreach (ProjectileType type in new[] { ProjectileType.GuardianKillerStraightProjectile, ProjectileType.GuardianKillerProjectile })
                {
                    Set(rig.Rig.Definition, "m_projectileType", type);
                    SetMovement(projectile, State.FiredOverhead);
                    SetBaseState(projectile, 1);
                    rig.Rig.Cooled[0] = 0;
                    Call(projectile, "Hit", elsewhere);
                    Assert.That(Get(projectile, "m_movementState"), Is.EqualTo(State.FiredOverhead), type + " other layer");
                    Call(projectile, "Hit", onTrack);
                    Assert.That(Get(projectile, "m_movementState"), Is.EqualTo(State.ExplodingInPlace), type + " track");
                    bool cools = type == ProjectileType.GuardianKillerStraightProjectile;
                    Assert.That(rig.Rig.Cooled[0], Is.EqualTo(cools ? 1 : 0), type + " cooling");
                    Assert.That(BaseState(projectile), Is.EqualTo(cools ? 2 : 1), type + " state");
                }
            });
        }

        // ---- damage callback ----

        private static void RunDamageAction(Action<Owned, BossRig, Action<DamageAction>> body) => WithBoss("lucid-guardian-damage", (owned, rig) =>
        {
            var hazard = owned.Track(ScriptableObject.CreateInstance<HazardDefinition>());
            hazard.Type = HazardType.BossDamageDealer;
            Set(rig.Rig.Projectile, "m_hazardDefinition", hazard);
            body(owned, rig, action => Call(rig.Rig.Projectile, "OnProcessDamageAction", action, rig.BossCollider));
        });

        // 0x57BA40: None is ignored; the hard failure, stumble and knockback actions report the hit to the boss with the
        // projectile's hazard type (a damage dealer sets the boss-hit-player flag); the hard failure goes to the manager.
        [Test]
        public void DamageActionsReportTheHitToTheBoss()
        {
            RunDamageAction((owned, rig, run) =>
            {
                var storage = (FSMStorage)rig.Boss.Storage;
                int[] failures = new int[1];
                rig.Manager.OnBossHardFailure += () => failures[0]++;
                run(DamageAction.None);
                Assert.That(storage.GetValue(BossFSMKeys.BossHitPlayer, false, false), Is.False, "None");
                foreach (DamageAction action in new[] { DamageAction.Stumble, DamageAction.Knockback })
                {
                    storage.SetValue(BossFSMKeys.BossHitPlayer, false);
                    run(action);
                    Assert.That(storage.GetValue(BossFSMKeys.BossHitPlayer, false, false), Is.True, action.ToString());
                }
                Assert.That(failures[0], Is.EqualTo(0));
                storage.SetValue(BossFSMKeys.BossHitPlayer, false);
                run(DamageAction.HardFailure);
                Assert.That(failures[0], Is.EqualTo(1), "the hard failure reaches the manager");
                Assert.That(storage.GetValue(BossFSMKeys.BossHitPlayer, false, false), Is.False, "and does not set the hit flag");
            });
        }

        // Any other action, ReturnDamage included, throws an out-of-range exception naming the parameter (0x57BA96).
        [Test]
        public void UnexpectedDamageActionsThrowAnOutOfRangeException()
        {
            RunDamageAction((owned, rig, run) =>
            {
                foreach (DamageAction action in new[] { DamageAction.ReturnDamage, (DamageAction)12345 })
                {
                    var thrown = Assert.Throws<ArgumentOutOfRangeException>(() => run(action), action.ToString());
                    Assert.That(thrown.ParamName, Is.EqualTo("damageAction"));
                    Assert.That(thrown.ActualValue, Is.EqualTo(action));
                }
                Assert.That(((FSMStorage)rig.Boss.Storage).GetValue(BossFSMKeys.BossHitPlayer, false, false), Is.False);
            });
        }

        // ---- returning ----

        // 0x57C1B0: a fired arc takes the return point's projectile spawn point (attach hash 0xA6845439), remembers the
        // squared distance to it and its own height, and returns as an arc; a fired straight projectile takes the
        // collider transform of the phase 4 spawn point (0x29077C10), aims at it and returns straight; an overhead or
        // exploding one does the same and returns from overhead. Every state, even one that changes nothing, gains a
        // lifespan (0x57C388).
        [Test]
        public void ReturnProjectileTurnsAFiredProjectileTowardsTheBoss()
        {
            WithBoss("lucid-guardian-return", (owned, rig) =>
            {
                GuardianProjectile projectile = rig.Rig.Projectile;
                Transform spawn = owned.Host("Lucid return spawn", true).transform;
                spawn.position = new Vector3(0f, 9f, 12f);
                Transform phase4 = owned.Host("Lucid phase four spawn", true).transform;
                phase4.position = new Vector3(0f, 0f, 10f);
                Collider phase4Collider = phase4.gameObject.AddComponent<BoxCollider>();
                AddAttachPoint(rig.Boss, ActorAttachPointType.BossGuardian_ProjectileReturnPoint, spawn, null);
                AddAttachPoint(rig.Boss, ActorAttachPointType.BossGuardian_ProjectileSpawnPoint_Phase4, null, phase4Collider);
                projectile.transform.position = new Vector3(0f, 3f, 5f);

                SetMovement(projectile, State.FiredAsArc);
                SetLife(projectile, 1.5f);
                projectile.ReturnProjectile();
                Assert.That(Get(projectile, "m_movementState"), Is.EqualTo(State.ReturningAsArc));
                Assert.That(Get(projectile, "m_returnTransform"), Is.SameAs(spawn));
                Assert.That((float)Get(projectile, "m_startingDistanceSq"), Is.EqualTo(85f).Within(1e-3f));
                Assert.That((float)Get(projectile, "m_currentDistanceSq"), Is.EqualTo(85f).Within(1e-3f));
                Assert.That(Get(projectile, "m_cachedY"), Is.EqualTo(3f));
                Assert.That(Life(projectile), Is.EqualTo(5.5f), "four seconds more");

                foreach ((State from, State to) in new[] { (State.FiredStraight, State.ReturningStraight),
                    (State.FiredOverhead, State.ReturningFromOverhead), (State.ExplodingInPlace, State.ReturningFromOverhead) })
                {
                    projectile.transform.position = new Vector3(0f, 0f, 0f);
                    Set(projectile, "m_returnTransform", null);
                    projectile.SetTargetDirection(Vector3.zero);
                    SetMovement(projectile, from);
                    SetLife(projectile, 1f);
                    projectile.ReturnProjectile();
                    Assert.That(Get(projectile, "m_movementState"), Is.EqualTo(to), from.ToString());
                    Assert.That(Get(projectile, "m_returnTransform"), Is.SameAs(phase4), from.ToString());
                    WorldMemberAccess.AreClose(new Vector3(0f, 0f, 10f), Vec(Get(projectile, "m_straightVelocity")), from + " aims at the return point");
                    Assert.That(Life(projectile), Is.EqualTo(5f), from.ToString());
                }

                foreach (State state in new[] { State.ReturningAsArc, State.ReturningStraight, State.ReturningFromOverhead,
                    State.MoveToResting, State.Resting })
                {
                    Set(projectile, "m_returnTransform", null);
                    SetMovement(projectile, state);
                    SetLife(projectile, 2f);
                    projectile.ReturnProjectile();
                    Assert.That(Get(projectile, "m_movementState"), Is.EqualTo(state), state + " is left as it is");
                    Assert.That(Get(projectile, "m_returnTransform") == null, Is.True, state.ToString());
                    Assert.That(Life(projectile), Is.EqualTo(6f), state + " still gains the lifespan");
                }
            });
        }

        // ---- firing ----

        // 0x57BD00: the Projectile.Fire work (life from the definition, state Fired, the fired event), then the managers
        // from the registered rows and the arc set-up from the boss FSM: the angle is atan2(x, z) of the position, the arc
        // centre sits on the vertical axis at the projectile's height, the radius is the distance to it, and the direction
        // and the starting movement come from the boss storage. The behind-player flag is cleared.
        [Test]
        public void FireReadsTheMovementAndDirectionFromTheBossStorageAndSetsUpTheArc()
        {
            Owned.Run(owned =>
            {
                owned.LeaseRow<TimeManager>();
                owned.LeaseRow<SaveManager>();
                using (var time = new OwnedTimeScaledObjects("lucid-guardian-fire"))
                {
                    Rig rig = NewRig(owned, time);
                    BossManager bossManager = owned.Register(owned.Host("Lucid fire boss manager", false).AddComponent<BossManager>());
                    GuardianManager guardian = owned.Register(owned.Host("Lucid fire guardian manager", false).AddComponent<GuardianManager>());
                    Boss boss = owned.Host("Lucid fire boss", false).AddComponent<Boss>();
                    var storage = new FSMStorage(0);
                    CharacterTriggerTestSupport.SetActorProperty(boss, "Storage", storage);
                    GuardianProjectile projectile = rig.Projectile;
                    projectile.SetBoss(boss);
                    storage.SetValue(BossFSMKeys.GuardianProjectileMovement, State.FiredStraight);
                    storage.SetValue(BossFSMKeys.GuardianIsClockwise, true);
                    projectile.transform.position = new Vector3(3f, 2f, 4f);
                    SetLife(projectile, 0.5f);
                    SetBaseState(projectile, 0);
                    Set(projectile, "m_behindPlayerSoPreparingToDie", true);

                    projectile.Fire();

                    Assert.That(Life(projectile), Is.EqualTo(4f), "the definition's lifespan");
                    Assert.That(BaseState(projectile), Is.EqualTo(1), "fired");
                    Assert.That(rig.Fired[0], Is.EqualTo(1));
                    Assert.That(Get(projectile, "m_bossManager"), Is.SameAs(bossManager));
                    Assert.That(Get(projectile, "m_guardianManager"), Is.SameAs(guardian));
                    Assert.That(Get(projectile, "m_movementState"), Is.EqualTo(State.FiredStraight));
                    Assert.That((float)Get(projectile, "m_progressionArcRadians"), Is.EqualTo((float)Math.Atan2(3.0, 4.0)).Within(1e-6f));
                    Assert.That(Vec(Get(projectile, "m_centerOfCircle")), Is.EqualTo(new Vector3(0f, 2f, 0f)));
                    Assert.That((float)Get(projectile, "m_radiusOfCircle"), Is.EqualTo(5f).Within(1e-5f));
                    Assert.That(Get(projectile, "m_movementIsClockwise"), Is.True);
                    Assert.That(Get(projectile, "m_behindPlayerSoPreparingToDie"), Is.False);
                }
            });
        }

        // An empty boss storage yields the defaults, an arc start and anticlockwise, and keeps them (the reads store the default).
        [Test]
        public void FireWithAnEmptyBossStorageStartsAnAnticlockwiseArc()
        {
            Owned.Run(owned =>
            {
                owned.LeaseRow<TimeManager>();
                owned.LeaseRow<SaveManager>();
                using (var time = new OwnedTimeScaledObjects("lucid-guardian-fire-default"))
                {
                    Rig rig = NewRig(owned, time);
                    owned.Register(owned.Host("Lucid default boss manager", false).AddComponent<BossManager>());
                    owned.Register(owned.Host("Lucid default guardian manager", false).AddComponent<GuardianManager>());
                    Boss boss = owned.Host("Lucid default fire boss", false).AddComponent<Boss>();
                    var storage = new FSMStorage(0);
                    CharacterTriggerTestSupport.SetActorProperty(boss, "Storage", storage);
                    rig.Projectile.SetBoss(boss);
                    rig.Projectile.transform.position = new Vector3(0f, 1f, -5f);
                    SetMovement(rig.Projectile, State.Resting);
                    Set(rig.Projectile, "m_movementIsClockwise", true);

                    rig.Projectile.Fire();

                    Assert.That(Get(rig.Projectile, "m_movementState"), Is.EqualTo(State.FiredAsArc));
                    Assert.That(Get(rig.Projectile, "m_movementIsClockwise"), Is.False);
                    Assert.That((float)Get(rig.Projectile, "m_progressionArcRadians"), Is.EqualTo((float)Math.PI).Within(1e-6f), "atan2(0, -5)");
                    Assert.That(storage.GetValue(BossFSMKeys.GuardianIsClockwise, true, false), Is.False, "the default was stored");
                    Assert.That(storage.GetValue(BossFSMKeys.GuardianProjectileMovement, State.Resting, false), Is.EqualTo(State.FiredAsArc),
                        "0x57BEB5 reads the movement state with store-default set, so the default was written back");
                }
            });
        }

        // ---- spline and teardown ----

        // 0x57C550: the projectile takes the spline's world form, rebuilds its one-entry tracking collections (the value is
        // the shared empty array), builds the tracker once and gives it the lists, and starts at distance zero with no boss hit.
        [Test]
        public void SetSplineTracksTheWorldSplineWithOneTracker()
        {
            WithBoss("lucid-guardian-spline", (owned, rig) =>
            {
                GuardianProjectile projectile = rig.Rig.Projectile;
                Spline NewSpline(out InertSpline inert)
                {
                    inert = owned.Host("Lucid inert spline", false).AddComponent<InertSpline>();
                    Spline spline = owned.Host("Lucid spline", false).AddComponent<Spline>();
                    Set(spline, "m_inertSpline", inert);
                    Set(spline, "m_activeSplineType", SplineVariantType.Inert);
                    return spline;
                }

                Spline first = NewSpline(out InertSpline firstInert);
                Set(projectile, "m_distanceAlongSpline", 7f);
                Set(projectile, "m_hasHitBoss", true);
                projectile.SetSpline(first);
                Assert.That(Get(projectile, "m_spline"), Is.SameAs(first));
                Assert.That(Get(projectile, "m_worldSpline"), Is.SameAs(firstInert));
                var surfaces = (Dictionary<ISurface, ISurface[]>)Get(projectile, "m_trackableSurfaces");
                var list = (List<ISurface>)Get(projectile, "m_trackableSurfacesList");
                Assert.That(surfaces.Keys, Is.EqualTo(new ISurface[] { firstInert }));
                Assert.That(surfaces[firstInert], Is.SameAs(Array.Empty<ISurface>()));
                Assert.That(list, Is.EqualTo(new ISurface[] { firstInert }));
                object tracker = Get(projectile, "m_tracker2D");
                Assert.That(tracker, Is.Not.Null);
                Assert.That(Get(tracker, "m_world"), Is.SameAs(surfaces));
                Assert.That(Get(tracker, "m_surfaces"), Is.SameAs(list));
                Assert.That(Get(projectile, "m_distanceAlongSpline"), Is.EqualTo(0f));
                Assert.That(Get(projectile, "m_hasHitBoss"), Is.False);

                Spline second = NewSpline(out InertSpline secondInert);
                projectile.SetSpline(second);
                Assert.That(surfaces.Keys, Is.EqualTo(new ISurface[] { secondInert }), "the old entry is cleared");
                Assert.That(list, Is.EqualTo(new ISurface[] { secondInert }));
                Assert.That(Get(projectile, "m_tracker2D"), Is.SameAs(tracker), "one tracker for the projectile's life");
            });
        }

        // 0x57B2B0 samples the world spline at the old distance, advances the distance by the step times the definition's
        // velocity (0x57B388..0x57B3A1), samples again, applies the new orientation and then the new location, and returns
        // the difference of the two sampled locations. The spline is a straight Bezier along +z (controls at thirds of its
        // 90-unit knot length), so the location at distance d is (0, 0, d) and its tangent is +z.
        [Test]
        public void SplineMovementAdvancesAlongTheWorldSplineByVelocityTimesTheStep()
        {
            WithBoss("lucid-guardian-spline-move", (owned, rig) =>
            {
                GuardianProjectile projectile = rig.Rig.Projectile;
                InertSpline inert = owned.Host("Lucid straight inert spline", false).AddComponent<InertSpline>();
                Spline spline = owned.Host("Lucid straight spline", false).AddComponent<Spline>();
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
                projectile.SetSpline(spline);
                Set(projectile, "m_distanceAlongSpline", 5f);
                SetMovement(projectile, State.FiredOverhead);
                projectile.transform.position = new Vector3(7f, 7f, 7f);

                var movement = Vec(Call(projectile, "DoMovement", 0.5f));

                Assert.That((float)Get(projectile, "m_distanceAlongSpline"), Is.EqualTo(10f).Within(1e-4f), "5 + 0.5 * velocity 10");
                WorldMemberAccess.AreClose(new Vector3(0f, 0f, 10f), projectile.transform.position, "the new sample is applied");
                WorldMemberAccess.AreClose(new Vector3(0f, 0f, 5f), movement, "new sample minus old sample, not minus the projectile's own position");
                Assert.That(Quaternion.Angle(Quaternion.identity, projectile.transform.rotation), Is.LessThan(1e-2f), "looking down the tangent");
            });
        }

        // 0x57C880 tests the event for null.
        [Test]
        public void FinalHitReadinessInvokesTheAuthoredEventWhenThereIsOne()
        {
            WithBoss("lucid-guardian-ready", (owned, rig) =>
            {
                GuardianProjectile projectile = rig.Rig.Projectile;
                int[] ready = CountEvent(projectile, "m_onBossReadyForFinalHit");
                Call(projectile, "InvokeFinalHitReadiness");
                Assert.That(ready[0], Is.EqualTo(1));
                Set(projectile, "m_onBossReadyForFinalHit", null);
                Assert.DoesNotThrow(() => Call(projectile, "InvokeFinalHitReadiness"));
            });
        }

        // 0x57C8A0: with a guardian manager the projectile unsubscribes from its final-hit event; without one nothing
        // happens (and the base teardown is skipped).
        [Test]
        public void OnDestroyUnsubscribesFromTheGuardianManagersFinalHit()
        {
            WithBoss("lucid-guardian-destroy", (owned, rig) =>
            {
                GuardianProjectile projectile = rig.Rig.Projectile;
                int[] ready = CountEvent(projectile, "m_onBossReadyForFinalHit");
                Delegate handler = Delegate.CreateDelegate(typeof(Action), projectile,
                    typeof(GuardianProjectile).GetMethod("InvokeFinalHitReadiness", BindingFlags.Instance | BindingFlags.NonPublic));
                rig.Guardian.FinalHitReady += (Action)handler;
                rig.Guardian.BossReadyForFinalHit();
                Assert.That(ready[0], Is.EqualTo(1), "subscribed");

                projectile.OnDestroy();
                rig.Guardian.BossReadyForFinalHit();
                Assert.That(ready[0], Is.EqualTo(1), "unsubscribed");

                Set(projectile, "m_guardianManager", null); // Reflection: a projectile whose Awake found no guardian manager
                Assert.DoesNotThrow(() => projectile.OnDestroy());
            });
        }

        // 0x57C8A0 tests the manager with UnityEngine.Object equality, so a destroyed guardian that is still referenced counts
        // as absent and the projectile leaves the subscription alone.
        [Test]
        public void OnDestroyTreatsADestroyedGuardianManagerAsAbsent()
        {
            WithBoss("lucid-guardian-destroy-gone", (owned, rig) =>
            {
                GuardianProjectile projectile = rig.Rig.Projectile;
                int[] ready = CountEvent(projectile, "m_onBossReadyForFinalHit");
                Delegate handler = Delegate.CreateDelegate(typeof(Action), projectile,
                    typeof(GuardianProjectile).GetMethod("InvokeFinalHitReadiness", BindingFlags.Instance | BindingFlags.NonPublic));
                rig.Guardian.FinalHitReady += (Action)handler;
                UnityEngine.Object.DestroyImmediate(rig.Guardian.gameObject);
                Assert.That(rig.Guardian == null, Is.True, "destroyed but still referenced");

                projectile.OnDestroy();

                rig.Guardian.BossReadyForFinalHit();
                Assert.That(ready[0], Is.EqualTo(1), "the subscription was not removed");
            });
        }
    }
}
