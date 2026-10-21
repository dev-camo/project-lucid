using System;
using System.Linq;
using System.Reflection;
using System.Text;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using ProjectLucid.Tests.CharCoreShared;
using UnityEngine;
using static ProjectLucid.Tests.CharacterAbilityTestSupport;
using static ProjectLucid.Tests.CharacterTriggerTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // The rails proximity abilities (TypeDefs 0x02000306..0x02000308): the shared generic update, proximity check and
    // debug text of CharacterAbility_RailsProximity<T> (x86-64 bodies 0xE67E00..0xE68B10) and the air and ground leaves
    // (0x750490..0x750740). Each case cites the native comparison it pins. The ability is built by its real constructor
    // and given a real Character on an owned inactive host and the definition, the way ActorAbility.Initialise would.
    // Not covered by any test, because they need a track manager with an octree or a body-bearing character: the
    // proximity check once a track manager exists (TryGetRailHit, FindNearestSurfaceLocationFromWorld, the
    // TargetedLocationIsValid raycast and SetTargetedObject, 0xE68428..0xE68A25) and the final target hand over
    // (TrySetTargetImmediately, which needs Character.IsActive and so a rigid body, 0xE682C0..0xE68394).
    public sealed class CharacterRailsProximityAbilityTests
    {
        private sealed class RailsBrain : CharacterBrain
        {
            public override void Close()
            {
            }
        }

        // CharacterAbility_TargetingHoming's field initialiser creates the camera manager row.
        private static void WithProximity<TAbility, TDefinition>(Action<Owned, CharacterGravityVectorProbe, TAbility, TDefinition> body)
            where TAbility : CharacterAbility, new()
            where TDefinition : CharacterAbilityDefinition => WithAbility<TAbility, TDefinition>(owned => owned.LeaseRow<CinemachineCameraManager>(), body);

        private static void WithGround(Action<Owned, CharacterGravityVectorProbe, CharacterAbility_RailsProximityGround, CharacterAbilityDefinition_RailsProximityGround> body) =>
            WithProximity(body);

        private static void WithAir(Action<Owned, CharacterGravityVectorProbe, CharacterAbility_RailsProximityAir, CharacterAbilityDefinition_RailsProximityAir> body) =>
            WithProximity(body);

        // RaycastColliderHit.Hit is the hit flag with the hit distance below the minimum distance (0x5D05D0); the
        // projected hit compares against the maximum distance instead.
        private static void SetGroundHit(Character character, bool hit, float distance = 0f, float minimum = 1f, float maximum = 0f)
        {
            RaycastColliderHit ground = character.Collider.GroundHit;
            Set(ground, "m_hit", hit);
            Set(ground, "m_hitDistance", distance);
            Set(ground, "m_minDistance", minimum);
            Set(ground, "m_maxDistance", maximum);
        }

        // Keeps the targeting interval from firing, so the refresh request of the base update does not hide the request
        // that the proximity body makes itself.
        private static void SuppressTargetingRefresh(object ability) => Set(ability, "m_nextTargetIntervalTime", 100f);

        private static void Update(object ability) => Call(ability, "DoOnUpdate", new RailsBrain(), 0.02f);

        private static bool RefreshRequested(object ability) => Get<bool>(ability, "m_targetRequiresRefresh");

        private static SurfaceLocation Surface(Owned owned, Quaternion rotation, Vector3 position) => new SurfaceLocation
        {
            m_surface = owned.Host("Lucid proximity rail", false).AddComponent<InertSpline>(),
            m_worldRotation = rotation,
            m_worldPosition = position,
        };

        // ---- ValidToTrigger ----

        // 0x750490: the air leaf may trigger while the ground hit (RaycastColliderHit.Hit) is false, the ground leaf (0x750720)
        // while it is true. The projected hit does not count.
        [Test]
        public void TheAirLeafTriggersOffTheGroundAndTheGroundLeafOnIt()
        {
            WithAir((owned, character, ability, definition) =>
            {
                SetGroundHit(character, false);
                Assert.That((bool)Call(ability, "ValidToTrigger"), Is.True, "no hit");
                SetGroundHit(character, true);
                Assert.That((bool)Call(ability, "ValidToTrigger"), Is.False, "a hit");
                SetGroundHit(character, true, distance: 2f, minimum: 1f, maximum: 5f);
                Assert.That((bool)Call(ability, "ValidToTrigger"), Is.True, "only a projected hit");
            });
            WithGround((owned, character, ability, definition) =>
            {
                SetGroundHit(character, false);
                Assert.That((bool)Call(ability, "ValidToTrigger"), Is.False, "no hit");
                SetGroundHit(character, true);
                Assert.That((bool)Call(ability, "ValidToTrigger"), Is.True, "a hit");
                SetGroundHit(character, true, distance: 2f, minimum: 1f, maximum: 5f);
                Assert.That((bool)Call(ability, "ValidToTrigger"), Is.False, "only a projected hit");
            });
        }

        // ---- CanJoinSurface ----

        // 0xE68A50: the base joins any location that has a surface (the first pointer of the by-value location is non zero);
        // the ground leaf keeps it.
        [Test]
        public void ByDefaultAnyLocationWithASurfaceCanBeJoined()
        {
            WithGround((owned, character, ability, definition) =>
            {
                SurfaceLocation location = Surface(owned, Quaternion.identity, Vector3.zero);
                Assert.That((bool)Call(ability, "CanJoinSurface", Vector3.zero, location, false), Is.True);
                Assert.That((bool)Call(ability, "CanJoinSurface", Vector3.zero, location, true), Is.True, "the tracker flag does not matter");
                Assert.That((bool)Call(ability, "CanJoinSurface", Vector3.zero, default(SurfaceLocation), false), Is.False);
            });
        }

        private static bool AirCanJoin(object ability, Vector3 position, SurfaceLocation location, bool railTrackerActive) =>
            (bool)Call(ability, "CanJoinSurface", position, location, railTrackerActive);

        // 0x7506AC..0x7506CF: without the rail tracker the surface joins when the cached cosine tolerance is at least the
        // dot of the normalised velocity with the surface normal (setae: equality joins, an unordered pair does not).
        [Test]
        public void WithoutTheRailTrackerTheToleranceCosineMustReachTheVelocityDot()
        {
            WithAir((owned, character, ability, definition) =>
            {
                definition.SurfaceNormalAngleTolerance = 60f;
                definition.CalculateCachedValues();
                SurfaceLocation location = Surface(owned, Quaternion.identity, Vector3.zero);
                foreach ((Vector3 velocity, bool expected) in new[]
                {
                    (new Vector3(0f, 0.4f, 0f), true), (new Vector3(0f, 0.6f, 0f), false), (new Vector3(0f, -1f, 0f), true), (new Vector3(1f, 0f, 0f), true),
                })
                {
                    SetActorProperty(character, "WorldVelocityNormalised", velocity);
                    Assert.That(AirCanJoin(ability, Vector3.zero, location, false), Is.EqualTo(expected), "velocity " + velocity);
                }
                // The position of the character is not consulted without the tracker.
                SetActorProperty(character, "WorldVelocityNormalised", new Vector3(0f, 0.6f, 0f));
                Assert.That(AirCanJoin(ability, new Vector3(0f, -50f, 0f), location, false), Is.False);

                definition.SurfaceNormalAngleTolerance = 0f;
                definition.CalculateCachedValues();
                SetActorProperty(character, "WorldVelocityNormalised", new Vector3(0f, 1f, 0f));
                Assert.That(AirCanJoin(ability, Vector3.zero, location, false), Is.True, "a cosine of one meets a dot of exactly one");
            });
        }

        // The normal is the up vector rotated by the location (Quaternion.op_Multiply at 0x7505D4), not the world up.
        [Test]
        public void TheSurfaceNormalIsTheUpVectorOfTheLocationRotation()
        {
            WithAir((owned, character, ability, definition) =>
            {
                definition.SurfaceNormalAngleTolerance = 60f;
                definition.CalculateCachedValues();
                SurfaceLocation tilted = Surface(owned, Quaternion.AngleAxis(90f, Vector3.forward), Vector3.zero);
                SetActorProperty(character, "WorldVelocityNormalised", new Vector3(-1f, 0f, 0f));
                Assert.That(AirCanJoin(ability, Vector3.zero, tilted, false), Is.False, "the rotated normal is -x, the dot is one");
                SetActorProperty(character, "WorldVelocityNormalised", new Vector3(0f, 1f, 0f));
                Assert.That(AirCanJoin(ability, Vector3.zero, tilted, false), Is.True, "the rotated normal is -x, the dot is zero");
                Assert.That(AirCanJoin(ability, Vector3.zero, Surface(owned, Quaternion.identity, Vector3.zero), false), Is.False,
                    "the same velocity along an unrotated normal has a dot of one against the 0.5 tolerance");
            });
        }

        // 0x750609..0x7506A8: with the rail tracker the height of the character along the normal above the location is
        // tested against the attach distance on the side the velocity points to. Moving along the normal the height must
        // be above minus the distance, moving against it below the distance (strictly, ja and seta), and a velocity
        // perpendicular to the normal never joins.
        [Test]
        public void WithTheRailTrackerTheHeightAlongTheNormalIsTestedAgainstTheAttachDistance()
        {
            WithAir((owned, character, ability, definition) =>
            {
                definition.RailTrackerAttachDistance = 2f;
                definition.SurfaceNormalAngleTolerance = 60f;
                definition.CalculateCachedValues();
                SurfaceLocation location = Surface(owned, Quaternion.identity, Vector3.zero);
                foreach ((Vector3 velocity, float height, bool expected) in new[]
                {
                    (Vector3.up, -1.5f, true), (Vector3.up, -2f, false), (Vector3.up, -3f, false), (Vector3.up, 5f, true),
                    (Vector3.down, 1.5f, true), (Vector3.down, 2f, false), (Vector3.down, 3f, false), (Vector3.down, -5f, true),
                    (Vector3.right, 0f, false), (Vector3.right, -1f, false), (Vector3.right, 1f, false),
                })
                {
                    SetActorProperty(character, "WorldVelocityNormalised", velocity);
                    Assert.That(AirCanJoin(ability, new Vector3(0f, height, 0f), location, true), Is.EqualTo(expected), "velocity " + velocity + ", height " + height);
                }
            });
        }

        // The height is measured from the position of the location, along the normal (0x750609..0x750621).
        [Test]
        public void TheTrackerHeightIsMeasuredFromTheLocationPosition()
        {
            WithAir((owned, character, ability, definition) =>
            {
                definition.RailTrackerAttachDistance = 2f;
                definition.CalculateCachedValues();
                SurfaceLocation location = Surface(owned, Quaternion.identity, new Vector3(7f, 10f, -3f));
                SetActorProperty(character, "WorldVelocityNormalised", Vector3.up);
                Assert.That(AirCanJoin(ability, new Vector3(100f, 9f, 100f), location, true), Is.True, "one below the location, inside the distance");
                Assert.That(AirCanJoin(ability, new Vector3(7f, 7f, -3f), location, true), Is.False, "three below the location");
            });
        }

        // The air leaf starts from the base result (0x750572): a location without a surface never joins.
        [Test]
        public void ALocationWithoutASurfaceNeverJoinsInTheAir()
        {
            WithAir((owned, character, ability, definition) =>
            {
                SetActorProperty(character, "WorldVelocityNormalised", Vector3.up);
                Assert.That(AirCanJoin(ability, Vector3.zero, default(SurfaceLocation), false), Is.False);
                Assert.That(AirCanJoin(ability, Vector3.zero, default(SurfaceLocation), true), Is.False);
            });
        }

        // ---- Update ----

        // 0xE67F64: an ability that is already triggered leaves at once, before it asks whether it may trigger.
        [Test]
        public void ATriggeredAbilityDoesNothingMore()
        {
            WithGround((owned, character, ability, definition) =>
            {
                SuppressTargetingRefresh(ability);
                SetGroundHit(character, false);
                ability.IsTriggered = true;
                Update(ability);
                Assert.That(ability.IsTriggered, Is.True);
                Assert.That(RefreshRequested(ability), Is.False, "ValidToTrigger was not asked");
            });
        }

        // 0xE67F8B..0xE68019: an ability that may not trigger asks for a refresh of the target and leaves.
        [Test]
        public void AnAbilityThatMayNotTriggerAsksForARefresh()
        {
            WithGround((owned, character, ability, definition) =>
            {
                SuppressTargetingRefresh(ability);
                SetGroundHit(character, false);
                Update(ability);
                Assert.That(RefreshRequested(ability), Is.True);
            });
            WithAir((owned, character, ability, definition) =>
            {
                SuppressTargetingRefresh(ability);
                SetGroundHit(character, true);
                Update(ability);
                Assert.That(RefreshRequested(ability), Is.True);
            });
        }

        private static void StoreAutoAttachSurface(Owned owned, Character character) =>
            character.Storage.SetValue<ISurface>(ActorFSMKeys.RailAutoAttach, owned.Host("Lucid auto attach rail", false).AddComponent<InertSpline>());

        // 0xE68075..0xE68084: after a jump on the rail tracker a character that is still rising (local vertical velocity above
        // zero, ja) leaves before looking at the stored auto attach rail; the rising test needs both the tracker flag and the
        // upward velocity.
        [Test]
        public void RisingAfterAJumpOnTheRailTrackerLeavesBeforeTheAutoAttachCheck()
        {
            foreach ((bool tracker, float vertical, bool reachesTheAutoAttachCheck) in new[]
            {
                (true, 1f, false), (true, 0f, true), (true, -1f, true), (false, 1f, true),
            })
            {
                WithGround((owned, character, ability, definition) =>
                {
                    SuppressTargetingRefresh(ability);
                    SetGroundHit(character, true);
                    definition.HomingDistance = 0f;
                    character.Storage.SetValue(ActorFSMKeys.JumpOnRailTracker, tracker);
                    SetActorProperty(character, "LocalVelocity", new Vector3(0f, vertical, 0f));
                    StoreAutoAttachSurface(owned, character);
                    Update(ability);
                    Assert.That(RefreshRequested(ability), Is.EqualTo(reachesTheAutoAttachCheck), "tracker " + tracker + ", vertical " + vertical);
                });
            }
        }

        // 0xE68086..0xE68160: without a refresh request the stored auto attach rail decides whether one is made; with one
        // the rail is not read. The update then ends on a zero homing distance (0xE681C4, jnp).
        [Test]
        public void AStoredAutoAttachRailOrAPendingRefreshLetsTheUpdateContinue()
        {
            WithGround((owned, character, ability, definition) =>
            {
                SuppressTargetingRefresh(ability);
                SetGroundHit(character, true);
                definition.HomingDistance = 0f;
                Update(ability);
                Assert.That(RefreshRequested(ability), Is.False, "no rail is stored");
                StoreAutoAttachSurface(owned, character);
                Update(ability);
                Assert.That(RefreshRequested(ability), Is.True, "a rail is stored");
            });
            WithGround((owned, character, ability, definition) =>
            {
                SuppressTargetingRefresh(ability);
                SetGroundHit(character, true);
                definition.HomingDistance = 0f;
                Set(ability, "m_targetRequiresRefresh", true);
                Update(ability);
                Assert.That(RefreshRequested(ability), Is.True, "the pending request stays");
                Assert.That(ability.IsTriggered, Is.False);
            });
        }

        // 0xE683A0: without a track manager (UnityEngine.Object.op_Equality at 0xE6844E) the proximity check fails, so the
        // ability stays untriggered and the targeted object is not written.
        [Test]
        public void WithoutATrackManagerTheProximityCheckFindsNothing()
        {
            WithGround((owned, character, ability, definition) =>
            {
                SuppressTargetingRefresh(ability);
                SetGroundHit(character, true);
                definition.HomingDistance = 3f;
                Set(ability, "m_targetRequiresRefresh", true);
                Set(character, "m_trackManagerRef", new SystemRef<TrackManager>("TrackManager", null));
                Assert.That(character.TrackManager == null, Is.True);
                Update(ability);
                Assert.That(ability.IsTriggered, Is.False);
                Assert.That(Get<CharacterTargetObjectData>(ability, "m_targetedObject").Target, Is.Null);
                Assert.That((bool)Call(ability, "ProximityCheckToTargetedObject", 3f, false), Is.False);
            });
        }

        // ---- Properties and debug text ----

        // 0xE67E00: the additional speed curve is the one of the definition (0x100 of the rails homing definition), and the
        // inherited speed lookup evaluates it.
        [Test]
        public void TheAdditionalSpeedCurveComesFromTheDefinition()
        {
            WithGround((owned, character, ability, definition) =>
            {
                Assert.That(Call(ability, "get_AdditionalSpeedCurve"), Is.SameAs(definition.HomingAdditionalSpeed));
                Assert.That(ability.GetAdditionalSpeed(0.5f), Is.EqualTo(0f), "a curve needs two keys");
                definition.HomingAdditionalSpeed = AnimationCurve.Linear(0f, 0f, 1f, 10f);
                Assert.That(Call(ability, "get_AdditionalSpeedCurve"), Is.SameAs(definition.HomingAdditionalSpeed));
                Assert.That(ability.GetAdditionalSpeed(0.5f), Is.EqualTo(5f).Within(1e-5f));
            });
        }

        // 0xE68A60: after the base text the indicator is On when the targeted location has a surface (cmp [+0xB8], 0).
        [Test]
        public void TheDebugTextShowsWhetherTheIndicatorHasASurface()
        {
            WithGround((owned, character, ability, definition) =>
            {
                var off = new StringBuilder();
                Call(ability, "GetDebugExtraInfo", off);
                Assert.That(off.ToString(), Does.EndWith("Indicator = Off "));
                Set(ability, "m_targetedLocation", Surface(owned, Quaternion.identity, Vector3.zero));
                var on = new StringBuilder();
                Call(ability, "GetDebugExtraInfo", on);
                Assert.That(on.ToString(), Does.EndWith("Indicator = On "));
                Assert.That(on.ToString(), Does.StartWith(off.ToString().Substring(0, off.Length - "Indicator = Off ".Length)));
            });
        }

        // ---- Declarations ----

        private static string[] AttributeNames(Type type) => type.GetCustomAttributesData()
            .Where(data => data.AttributeType != typeof(SerializableAttribute))
            .Select(data => data.AttributeType.Name == "Il2CppSetOptionAttribute"
                ? "Il2CppSetOption:" + Convert.ToInt32(data.ConstructorArguments[0].Value)
                : data.AttributeType.Name.Replace("Attribute", "")).ToArray();

        // The declaration rows of the original DummyDll tables; this is declaration shape, not behaviour.
        [Test]
        public void AbilitiesKeepTheOriginalDeclarationShape()
        {
            Type generic = typeof(CharacterAbility_RailsProximity<>);
            AssertMembers(generic, 0x00100081,
                new[] { ("RailHomingReactivateTime", 0x8051) },
                new[] { ("get_AdditionalSpeedCurve", 0x08c4), ("DoOnUpdate", 0x00c4), ("ValidToTrigger", 0x05c4), ("ProximityCheckToTargetedObject", 0x0081), ("CanJoinSurface", 0x01c4), ("GetDebugExtraInfo", 0x00c4), (".ctor", 0x1884) });
            Assert.That(generic.BaseType.GetGenericTypeDefinition(), Is.EqualTo(typeof(CharacterAbility_TargetingHoming<>)));
            Assert.That(generic.GetInterfaces().Select(i => i.Name), Does.Contain("IAbilityTrigger"));
            Assert.That(generic.GetGenericArguments().Single().GetGenericParameterConstraints(), Is.EqualTo(new[] { typeof(CharacterAbilityDefinition_RailsHoming) }));
            Assert.That(AttributeNames(generic), Is.EqualTo(new[] { "Il2CppSetOption:2", "Il2CppSetOption:1" }));
            FieldInfo reactivate = generic.GetField("RailHomingReactivateTime", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(reactivate.IsLiteral, Is.True);
            Assert.That(reactivate.GetRawConstantValue(), Is.EqualTo(0.1f));

            AssertMembers(typeof(CharacterAbility_RailsProximityAir), 0x00100001,
                new (string, int)[0],
                new[] { ("ValidToTrigger", 0x00c4), ("CanJoinSurface", 0x00c4), (".ctor", 0x1886) });
            Assert.That(typeof(CharacterAbility_RailsProximityAir).BaseType, Is.EqualTo(typeof(CharacterAbility_RailsProximity<CharacterAbilityDefinition_RailsProximityAir>)));
            Assert.That(AttributeNames(typeof(CharacterAbility_RailsProximityAir)), Is.EqualTo(new[] { "Il2CppSetOption:2", "Il2CppSetOption:1" }));

            AssertMembers(typeof(CharacterAbility_RailsProximityGround), 0x00100001,
                new (string, int)[0],
                new[] { ("ValidToTrigger", 0x00c4), (".ctor", 0x1886) });
            Assert.That(typeof(CharacterAbility_RailsProximityGround).BaseType, Is.EqualTo(typeof(CharacterAbility_RailsProximity<CharacterAbilityDefinition_RailsProximityGround>)));
            Assert.That(AttributeNames(typeof(CharacterAbility_RailsProximityGround)), Is.EqualTo(new[] { "Il2CppSetOption:2", "Il2CppSetOption:1" }));
        }
    }
}
