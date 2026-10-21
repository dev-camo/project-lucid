using System;
using System.Collections.Generic;
using System.Reflection;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using ProjectLucid.Tests.CharCoreShared;
using UnityEngine;
using UnityEngine.TestTools.Utils;
using static ProjectLucid.Tests.CharacterAbilityTestSupport;
using static ProjectLucid.Tests.CharacterTriggerTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // CharacterAbility_AirChaosAim (TypeDef 0x020002b5, 0x73CCC0..0x741460). The character is a real component on an
    // owned inactive host with a supplied downward gravity, and the colliders the ray casts meet are real box colliders
    // on owned hosts. Cases cite the native comparison or store they pin. Not run here: the members that need a
    // started character, the camera manager or the track manager (Initialise, Close, DoOnEnter, DoOnUpdate,
    // DoOnLateUpdate, DoOnActivation and DoOnDeactivation, ControllerMovementActive, UpdateTarget, the rail and
    // transporter hits, ApplyTargetingImpulse, the target-type lookup and the targeting-modifier rotation of UpdateUI)
    // and the collision-data branches of RaycastHitIsValid (they need character collision data with its modifiers).
    public sealed class CharacterAirChaosAimAbilityTests
    {
        private const float Tolerance = 1e-3f;
        private static readonly Vector3EqualityComparer Vec = new Vector3EqualityComparer(Tolerance);

        // The rows the constructors read are leased for the case: the time manager row of the air chaos base and the
        // camera manager row of the aim ability. With homing targets, a HomingTarget also reads the character manager
        // row and the time category lookup.
        private static void WithAim(Action<Owned, CharacterGravityVectorProbe, CharacterAbility_AirChaosAim, CharacterAbilityDefinition_AirChaosAim> body, bool homingTargets = false) =>
            WithAbility<CharacterAbility_AirChaosAim, CharacterAbilityDefinition_AirChaosAim>(owned =>
            {
                owned.LeaseRow<TimeManager>();
                owned.LeaseRow<CinemachineCameraManager>();
                if (!homingTargets) return;
                owned.LeaseRow<CharacterManager>();
                FieldInfo config = typeof(TimeScaledComponent_SDT).GetField("s_config_Internal", BindingFlags.Static | BindingFlags.NonPublic);
                object prior = config.GetValue(null);
                owned.Lease(new ReleaseAction(() => config.SetValue(null, prior)));
                config.SetValue(null, owned.Track(ScriptableObject.CreateInstance<TimeCategoryLookup>()));
            }, (owned, character, ability, definition) =>
            {
                SetProperty(ability, "DefinitionAirChaos", definition);
                body(owned, character, ability, definition);
            });

        private static BoxCollider Box(Owned owned, string name, Vector3 position, Transform parent = null)
        {
            GameObject host = owned.Host(name, true, parent);
            host.transform.position = position;
            var box = host.AddComponent<BoxCollider>();
            Physics.SyncTransforms();
            return box;
        }

        private static RuntimeType RuntimeTypeOf(Type type)
        {
            var runtimeType = new RuntimeType();
            Set(runtimeType, "m_targetAssemblyQualifiedName", type.AssemblyQualifiedName);
            runtimeType.OnAfterDeserialize();
            return runtimeType;
        }

        private static CharacterAbilityDefinition_AirChaosAim.HomingTargetAimDefinition HomingDefinition(
            Type targetType, List<HomingTargetType> homingTypes = null) =>
            new CharacterAbilityDefinition_AirChaosAim.HomingTargetAimDefinition { TargetType = RuntimeTypeOf(targetType), HomingTypes = homingTypes };

        private static Dictionary<Collider, CharacterAbilityDefinition_AirChaosAim.HomingTargetAimDefinition> Registered(CharacterAbility_AirChaosAim ability) =>
            Get<Dictionary<Collider, CharacterAbilityDefinition_AirChaosAim.HomingTargetAimDefinition>>(ability, "m_homingTargetAimDefinitions");

        // ---- constructor (0600105d, 0x741460) and the IHomingAttack contract (06001054..06001059) ----

        // 0x741514 stores AngleAxis(90, right) (the constant at VA 0x29a7354), and the two collections and the camera
        // manager reference are created empty.
        [Test]
        public void ConstructionBuildsTheForwardToNormalRotationAndEmptyCollections()
        {
            WithAim((owned, character, ability, definition) =>
            {
                Assert.That(Quaternion.Angle(Get<Quaternion>(ability, "ForwardToNormalRotation"), Quaternion.AngleAxis(90f, Vector3.right)), Is.LessThan(0.01f));
                Assert.That(Registered(ability), Is.Empty);
                Assert.That(Get<List<Collider>>(ability, "m_homingTargets"), Is.Empty);
                Assert.That(Get<SystemRef<CinemachineCameraManager>>(ability, "m_cameraManagerRef"), Is.Not.Null);
            });
        }

        // 0x740ff0 and 0x741040 read the cached zone size (+0x268) and the authored zone offset (+0x1d8) of the
        // definition; the target callbacks are empty and IsHomingTargetValid returns false (0x7410b4).
        [Test]
        public void HomingContractReadsTheDefinitionZoneAndAcceptsNoTargets()
        {
            WithAim((owned, character, ability, definition) =>
            {
                definition.HomingTargetProjectDistance = 10f;
                definition.HomingTargetProjectConeAngle = new Vector2(20f, 40f);
                definition.HomingTargetZoneOffset = new Vector3(1f, 2f, 3f);
                CharacterAirChaosDefinitionTests.CacheKeepingTheConeTable(definition);
                IHomingAttack contract = ability;
                Assert.That(contract.TargetZoneSize, Is.EqualTo(definition.HomingTargetZoneSize));
                Assert.That(contract.TargetZoneSize, Is.Not.EqualTo(Vector3.zero));
                Assert.That(contract.TargetZoneOffset, Is.EqualTo(new Vector3(1f, 2f, 3f)));
                Assert.That(contract.IsHomingTargetValid(null), Is.False);
                contract.AddTarget(null);
                contract.RemoveTarget(null);
                contract.OnHomingTargetHit(null);
            });
        }

        // ---- OnTriggerEnter / OnTriggerExit (0600105a @0x7410D0, 0600105b @0x741410) ----

        // 0x74120a..0x741376: each definition's target type is looked up on the collider's object and its parents
        // (GetComponentInParent), then on the collider itself; the first definition that finds the type registers the
        // collider with TryAdd and ends the loop, and the dirty flag takes the TryAdd result with an or.
        [Test]
        public void TriggerEnterRegistersTheColliderWithTheFirstDefinitionWhoseTypeIsFound()
        {
            WithAim((owned, character, ability, definition) =>
            {
                BoxCollider collider = Box(owned, "Lucid chaos aim target", Vector3.zero);
                var skipped = HomingDefinition(typeof(Rigidbody));
                var chosen = HomingDefinition(typeof(BoxCollider));
                var later = HomingDefinition(typeof(BoxCollider));
                definition.HomingTargetDefinitions = new List<CharacterAbilityDefinition_AirChaosAim.HomingTargetAimDefinition> { skipped, chosen, later };
                ability.OnTriggerEnter(collider);
                Assert.That(Registered(ability).Count, Is.EqualTo(1));
                Assert.That(Registered(ability)[collider], Is.SameAs(chosen));
                Assert.That(Get<bool>(ability, "m_homingTargetsDirty"), Is.True);
                Set(ability, "m_homingTargetsDirty", false);
                ability.OnTriggerEnter(collider);
                Assert.That(Registered(ability)[collider], Is.SameAs(chosen));
                Assert.That(Get<bool>(ability, "m_homingTargetsDirty"), Is.False, "TryAdd refused the duplicate");
            });
        }

        [Test]
        public void TriggerEnterFindsTheTargetTypeOnAParentAndIgnoresColliderWithoutIt()
        {
            WithAim((owned, character, ability, definition) =>
            {
                GameObject parent = owned.Host("Lucid chaos aim parent");
                parent.AddComponent<Rigidbody>();
                BoxCollider child = Box(owned, "Lucid chaos aim child", Vector3.zero, parent.transform);
                BoxCollider stranger = Box(owned, "Lucid chaos aim stranger", Vector3.zero);
                definition.HomingTargetDefinitions = new List<CharacterAbilityDefinition_AirChaosAim.HomingTargetAimDefinition> { HomingDefinition(typeof(Rigidbody)) };
                ability.OnTriggerEnter(stranger);
                Assert.That(Registered(ability), Is.Empty);
                ability.OnTriggerEnter(child);
                Assert.That(Registered(ability).ContainsKey(child), Is.True);
            });
        }

        // 0x7412ad..0x741304: a HomingTarget component passes a definition whose homing type list is empty, or contains
        // the target's type; otherwise the definition is skipped (0x741306 je back to the loop).
        [Test]
        public void TriggerEnterFiltersHomingTargetsByTheDefinitionHomingTypes()
        {
            WithAim((owned, character, ability, definition) =>
            {
                GameObject host = owned.Host("Lucid chaos aim homing target");
                var collider = host.AddComponent<BoxCollider>();
                var target = host.AddComponent<HomingTarget>();
                Set(target, "m_collider", collider);
                Set(target, "m_targetType", HomingTargetType.Enemy);
                Physics.SyncTransforms();
                var rejecting = HomingDefinition(typeof(HomingTarget), new List<HomingTargetType> { HomingTargetType.Ring });
                definition.HomingTargetDefinitions = new List<CharacterAbilityDefinition_AirChaosAim.HomingTargetAimDefinition> { rejecting };
                ability.OnTriggerEnter(collider);
                Assert.That(Registered(ability), Is.Empty);
                var matching = HomingDefinition(typeof(HomingTarget), new List<HomingTargetType> { HomingTargetType.Ring, HomingTargetType.Enemy });
                definition.HomingTargetDefinitions = new List<CharacterAbilityDefinition_AirChaosAim.HomingTargetAimDefinition> { rejecting, matching };
                ability.OnTriggerEnter(collider);
                Assert.That(Registered(ability)[collider], Is.SameAs(matching));
                Registered(ability).Clear();
                var unrestricted = HomingDefinition(typeof(HomingTarget), new List<HomingTargetType>());
                definition.HomingTargetDefinitions = new List<CharacterAbilityDefinition_AirChaosAim.HomingTargetAimDefinition> { unrestricted };
                ability.OnTriggerEnter(collider);
                Assert.That(Registered(ability)[collider], Is.SameAs(unrestricted));
            }, true);
        }

        // 0x74128c..0x741349: an Enemy that is disabled skips the definition (0x74131a back to the loop); a non-empty
        // enemy type list skips it unless the list holds the enemy's type (0x741343); otherwise the definition is
        // registered. The enemy sits on an inactive host so its Awake never runs.
        [Test]
        public void TriggerEnterFiltersEnemiesByEnabledStateAndEnemyTypes()
        {
            WithAim((owned, character, ability, definition) =>
            {
                owned.LeaseRow<EnemyManager>();
                owned.LeaseRow<CharacterManager>();
                GameObject host = owned.Host("Lucid chaos aim enemy", false);
                var collider = host.AddComponent<BoxCollider>();
                var enemy = host.AddComponent<Enemy>();
                Set(enemy, "m_type", EnemyType.Motobug);
                var byEnemy = HomingDefinition(typeof(Enemy));
                byEnemy.EnemyTypes = new List<EnemyType>();
                var fallback = HomingDefinition(typeof(BoxCollider));
                definition.HomingTargetDefinitions = new List<CharacterAbilityDefinition_AirChaosAim.HomingTargetAimDefinition> { byEnemy, fallback };

                enemy.enabled = false;
                ability.OnTriggerEnter(collider);
                Assert.That(Registered(ability)[collider], Is.SameAs(fallback), "a disabled enemy skips its definition");

                enemy.enabled = true;
                Registered(ability).Clear();
                byEnemy.EnemyTypes = new List<EnemyType> { EnemyType.Crabmeat };
                ability.OnTriggerEnter(collider);
                Assert.That(Registered(ability)[collider], Is.SameAs(fallback), "the enemy type is not listed");

                Registered(ability).Clear();
                byEnemy.EnemyTypes = new List<EnemyType> { EnemyType.Crabmeat, EnemyType.Motobug };
                ability.OnTriggerEnter(collider);
                Assert.That(Registered(ability)[collider], Is.SameAs(byEnemy), "the enemy type is listed");

                Registered(ability).Clear();
                byEnemy.EnemyTypes = new List<EnemyType>();
                ability.OnTriggerEnter(collider);
                Assert.That(Registered(ability)[collider], Is.SameAs(byEnemy), "an empty list accepts every enemy type");
            });
        }

        [Test]
        public void TriggerExitForgetsTheColliderAndToleratesAnUnknownOne()
        {
            WithAim((owned, character, ability, definition) =>
            {
                BoxCollider known = Box(owned, "Lucid chaos aim known", Vector3.zero);
                BoxCollider unknown = Box(owned, "Lucid chaos aim unknown", Vector3.zero);
                Registered(ability)[known] = HomingDefinition(typeof(BoxCollider));
                ability.OnTriggerExit(unknown);
                Assert.That(Registered(ability).Count, Is.EqualTo(1));
                ability.OnTriggerExit(known);
                Assert.That(Registered(ability), Is.Empty);
            });
        }

        // ---- RemoveHomingTargetChildren (0600105c @0x740270) ----

        // 0x74030a..0x74045c: the list is refilled with the registered colliders, then every collider that has another
        // listed collider (itself included) among its transform ancestors is dropped from the dictionary. The list
        // keeps the pre-removal snapshot.
        [Test]
        public void ChildHomingTargetsAreDroppedWhenAnAncestorIsAlsoRegistered()
        {
            WithAim((owned, character, ability, definition) =>
            {
                BoxCollider top = Box(owned, "Lucid chaos aim top", Vector3.zero);
                BoxCollider child = Box(owned, "Lucid chaos aim child", Vector3.zero, top.transform);
                BoxCollider grandchild = Box(owned, "Lucid chaos aim grandchild", Vector3.zero, child.transform);
                BoxCollider separate = Box(owned, "Lucid chaos aim separate", Vector3.zero);
                foreach (BoxCollider collider in new[] { top, child, grandchild, separate })
                    Registered(ability)[collider] = HomingDefinition(typeof(BoxCollider));
                Call(ability, "RemoveHomingTargetChildren");
                Assert.That(Registered(ability).Keys, Is.EquivalentTo(new Collider[] { top, separate }));
                Assert.That(Get<List<Collider>>(ability, "m_homingTargets"), Is.EquivalentTo(new Collider[] { top, child, grandchild, separate }));
            });
        }

        // ---- TryGetHitCollider (06001042 @0x73F5F0) ----

        // 0x73f662..0x73f6b5: the sorted ray cast uses the collision mask (+0x1b4), not the track mask, and ignores
        // triggers (edx = 1); the nearest valid hit is returned. A miss returns false with a cleared hit.
        [Test]
        public void HitColliderIsTheNearestSolidColliderInTheCollisionMask()
        {
            WithAim((owned, character, ability, definition) =>
            {
                BoxCollider trigger = Box(owned, "Lucid chaos aim trigger", new Vector3(0f, 0f, 2f));
                trigger.isTrigger = true;
                BoxCollider near = Box(owned, "Lucid chaos aim near", new Vector3(0f, 0f, 5f));
                BoxCollider far = Box(owned, "Lucid chaos aim far", new Vector3(0f, 0f, 10f));
                far.gameObject.layer = 9;
                Physics.SyncTransforms();
                var arguments = new object[] { Vector3.zero, Vector3.forward, 20f, null };
                Assert.That((bool)Call(ability, "TryGetHitCollider", arguments), Is.True);
                var hit = (RaycastHit)arguments[3];
                Assert.That(hit.collider, Is.SameAs(near));
                Assert.That(hit.distance, Is.EqualTo(4.5f).Within(Tolerance));
                definition.CollisionMask = 1 << 9;
                definition.TrackMask = 1 << 0;
                arguments = new object[] { Vector3.zero, Vector3.forward, 20f, null };
                Assert.That((bool)Call(ability, "TryGetHitCollider", arguments), Is.True);
                Assert.That(((RaycastHit)arguments[3]).collider, Is.SameAs(far));
                arguments = new object[] { Vector3.zero, Vector3.forward, 1f, null };
                Assert.That((bool)Call(ability, "TryGetHitCollider", arguments), Is.False);
                Assert.That(((RaycastHit)arguments[3]).collider == null, Is.True);
                Assert.That(Get<CharacterCollisionData>(ability, "m_hitCollisionData") == null, Is.True);
            });
        }

        // 0x73f5a0..0x73f5e9: a hit collider without character collision data is valid (the data is null, so the
        // first test returns true), and the data found is stored in the out argument.
        [Test]
        public void ARaycastHitWithoutCollisionDataIsValid()
        {
            WithAim((owned, character, ability, definition) =>
            {
                BoxCollider box = Box(owned, "Lucid chaos aim solid", new Vector3(0f, 0f, 5f));
                Assert.That(Physics.Raycast(Vector3.zero, Vector3.forward, out RaycastHit hit, 20f), Is.True);
                var arguments = new object[] { hit, null };
                Assert.That((bool)Call(ability, "RaycastHitIsValid", arguments), Is.True);
                Assert.That((CharacterCollisionData)arguments[1] == null, Is.True);
            });
        }

        // ---- TryGetLedgeHit (06001048 @0x73F900) ----

        // 0x73f940..0x73fc12: the ray starts above the aim position by LedgeDistance against gravity and moves
        // LedgeOffset along the character forward projected on the plane of gravity and normalised, and it points along
        // gravity for LedgeDistance against the track mask. The ledge box is only 0.2 wide, so a forward that was not
        // projected (0.707 instead of 1) or an offset of the wrong sign misses it.
        [Test]
        public void LedgeHitCastsDownFromAboveAndAheadOfTheAimPosition()
        {
            WithAim((owned, character, ability, definition) =>
            {
                character.SetWorldRotation(Quaternion.LookRotation(new Vector3(1f, -1f, 0f).normalized, Vector3.up));
                definition.LedgeDistance = 2f;
                definition.LedgeOffset = 1f;
                Set(ability, "m_aimPosition", Vector3.zero);
                BoxCollider ledge = Box(owned, "Lucid chaos aim ledge", new Vector3(1f, 0f, 0f));
                ledge.size = new Vector3(0.2f, 1f, 2f);
                Physics.SyncTransforms();
                var arguments = new object[] { null };
                Assert.That((bool)Call(ability, "TryGetLedgeHit", arguments), Is.True);
                var hit = (RaycastHit)arguments[0];
                Assert.That(hit.collider, Is.SameAs(ledge));
                Assert.That(hit.point, Is.EqualTo(new Vector3(1f, 0.5f, 0f)).Using(Vec));
                definition.TrackMask = 1 << 9;
                arguments = new object[] { null };
                Assert.That((bool)Call(ability, "TryGetLedgeHit", arguments), Is.False, "the ledge is not on the track mask");
                definition.TrackMask = -1;
                definition.LedgeDistance = 0.1f;
                arguments = new object[] { null };
                Assert.That((bool)Call(ability, "TryGetLedgeHit", arguments), Is.False, "too short to reach the ledge");
            });
        }

        // ---- ProjectToGround (0600104d @0x73FD00) ----

        // 0x73fd5e..0x73ff12: a ray from OffsetDistance above the aim position casts along gravity for GroundDistance
        // against the track mask. A hit sets the ground type to Track, the ground position to the hit point lifted
        // OffsetDistance along the normal, and the ground rotation to look along the normal with the character forward as
        // up; a miss sets the ground type to None and leaves the position and rotation alone.
        [Test]
        public void GroundProjectionPlacesTheGroundTargetOnTheTrackBelowTheAimPosition()
        {
            WithAim((owned, character, ability, definition) =>
            {
                Face(character, 90f);
                BoxCollider floor = Box(owned, "Lucid chaos aim floor", new Vector3(0f, -0.5f, 0f));
                floor.size = new Vector3(10f, 1f, 10f);
                Physics.SyncTransforms();
                Set(ability, "m_aimPosition", new Vector3(0f, 5f, 0f));
                Set(ability, "m_groundPosition", new Vector3(7f, 7f, 7f));
                Set(ability, "m_groundType", AbilityAimTargetType.Wall);
                Call(ability, "ProjectToGround");
                Assert.That(Get<AbilityAimTargetType>(ability, "m_groundType"), Is.EqualTo(AbilityAimTargetType.Track));
                Assert.That(Get<Vector3>(ability, "m_groundPosition"), Is.EqualTo(new Vector3(0f, 0.1f, 0f)).Using(Vec));
                var rotation = Get<Quaternion>(ability, "m_groundRotation");
                Assert.That(rotation * Vector3.forward, Is.EqualTo(Vector3.up).Using(Vec));
                Assert.That(rotation * Vector3.up, Is.EqualTo(new Vector3(1f, 0f, 0f)).Using(Vec));
                definition.GroundDistance = 1f;
                Set(ability, "m_groundPosition", new Vector3(7f, 7f, 7f));
                Call(ability, "ProjectToGround");
                Assert.That(Get<AbilityAimTargetType>(ability, "m_groundType"), Is.EqualTo(AbilityAimTargetType.None));
                Assert.That(Get<Vector3>(ability, "m_groundPosition"), Is.EqualTo(new Vector3(7f, 7f, 7f)));
            });
        }

        private static void Face(Character character, float yaw) => character.SetWorldRotation(Quaternion.Euler(0f, yaw, 0f));

        // ---- SetTransformFromHit (0600104e @0x73FC20) ----

        // 0x73fc61..0x73fcdd: normal and point come from the hit, the offset is OffsetDistance along the normal and
        // the rotation looks along the normal with the character forward as up.
        [Test]
        public void TransformFromHitTakesTheNormalPointOffsetAndRotation()
        {
            WithAim((owned, character, ability, definition) =>
            {
                Face(character, 90f);
                BoxCollider floor = Box(owned, "Lucid chaos aim floor", new Vector3(0f, -0.5f, 0f));
                floor.size = new Vector3(10f, 1f, 10f);
                Physics.SyncTransforms();
                Assert.That(Physics.Raycast(new Vector3(2f, 5f, 3f), Vector3.down, out RaycastHit hit, 20f), Is.True);
                var arguments = new object[] { hit, null, null, null, null };
                Call(ability, "SetTransformFromHit", arguments);
                Assert.That((Vector3)arguments[1], Is.EqualTo(new Vector3(2f, 0f, 3f)).Using(Vec));
                Assert.That((Vector3)arguments[2], Is.EqualTo(new Vector3(0f, 0.1f, 0f)).Using(Vec));
                Assert.That((Vector3)arguments[3], Is.EqualTo(Vector3.up).Using(Vec));
                var rotation = (Quaternion)arguments[4];
                Assert.That(rotation * Vector3.forward, Is.EqualTo(Vector3.up).Using(Vec));
                Assert.That(rotation * Vector3.up, Is.EqualTo(new Vector3(1f, 0f, 0f)).Using(Vec));
            });
        }

        // ---- GetCameraOverride (0600104f @0x73E930) ----

        // 0x73e981: no target gives null. Without collision data on the last hit (0x73e9b8 jne) the target's own slope
        // override is chosen from the slope of the aim rotation: a rotation of 30 degrees about right under downward
        // gravity has a slope angle of 330 (hand-derived from ActorMovementUtilities), so the 329..331 override wins
        // over the 0..30 one.
        [Test]
        public void CameraOverrideWithoutCollisionDataIsTheTargetSlopeOverride()
        {
            WithAim((owned, character, ability, definition) =>
            {
                Assert.That(Call(ability, "GetCameraOverride", new object[] { null }) == null, Is.True);
                var low = owned.Track(ScriptableObject.CreateInstance<CameraAbilitySlopeDefinition>());
                low.AngleRanges = new List<Vector2> { new Vector2(0f, 30f) };
                var high = owned.Track(ScriptableObject.CreateInstance<CameraAbilitySlopeDefinition>());
                high.AngleRanges = new List<Vector2> { new Vector2(329f, 331f) };
                var target = new CharacterAbilityDefinition_AirChaosAim.ChaosAimTarget { CameraSlopeOverrides = new List<CameraAbilitySlopeDefinition> { low, high } };
                Set(ability, "m_aimRotation", Quaternion.AngleAxis(30f, Vector3.right));
                Assert.That(Call(ability, "GetCameraOverride", target), Is.SameAs(high));
                Set(ability, "m_aimRotation", Quaternion.identity);
                Assert.That(Call(ability, "GetCameraOverride", target), Is.SameAs(low), "a level aim rotation has slope angle 0");
            });
        }

        // 0x73ea45..0x73eb30: with camera override metadata on the hit, the override is the one stored under the
        // target's key and the aim rotation takes the collision data's rotation. The rotation is multiplied by
        // RotationReverseLocal only when ReverseByDirection is set and the override axis in world space has a strictly
        // negative dot with the current aim forward (jbe at 0x73eae4 keeps a dot of zero unreversed).
        [Test]
        public void CameraOverrideWithCollisionDataTakesTheColliderRotationAndReversesOnlyOnAStrictlyOpposedAxis()
        {
            WithAim((owned, character, ability, definition) =>
            {
                var key = owned.Track(ScriptableObject.CreateInstance<MetadataKeyType>());
                var overrideDefinition = owned.Track(ScriptableObject.CreateInstance<CameraAbilityOverrideDefinition>());
                Set(overrideDefinition, "m_axisAlignment", Vector3.forward);
                var group = owned.Track(ScriptableObject.CreateInstance<MetadataFixedGroup>());
                var row = new MetadataFixed(key, string.Empty);
                Set(row, "m_valueObject", overrideDefinition);
                Get<List<MetadataFixed>>(Get<object>(group, "m_data"), "m_metadata").Add(row);
                // The collision data is found by the ray cast of TryGetHitCollider (0x73F5F0), which stores it as the last hit.
                var data = Box(owned, "Lucid chaos aim camera override", new Vector3(0f, 0f, 5f)).gameObject.AddComponent<CharacterCollisionData>();
                // Reflection: OnCharacterManagerValid derives these from the data manager's terrain definitions, which need a started App
                SetProperty(data, "CameraOverrideMetadata", group);
                SetProperty(data, "HasCameraOverride", true);
                var hitArguments = new object[] { Vector3.zero, Vector3.forward, 20f, null };
                Assert.That((bool)Call(ability, "TryGetHitCollider", hitArguments), Is.True, "precondition: the ray hits the collider");
                Assert.That(Get<CharacterCollisionData>(ability, "m_hitCollisionData"), Is.SameAs(data), "precondition: the hit data is stored");
                var target = new CharacterAbilityDefinition_AirChaosAim.ChaosAimTarget { CameraOverrideMetadataKey = key };

                void Check(bool reverseByDirection, Quaternion aimRotation, Quaternion expected, string message)
                {
                    Set(overrideDefinition, "m_reverseByDirection", reverseByDirection);
                    Set(ability, "m_aimRotation", aimRotation);
                    Assert.That(Call(ability, "GetCameraOverride", target), Is.SameAs(overrideDefinition), message);
                    Assert.That(Quaternion.Angle(Get<Quaternion>(ability, "m_aimRotation"), expected), Is.LessThan(0.01f), message);
                }

                Check(true, Quaternion.AngleAxis(180f, Vector3.up), Actor.RotationReverseLocal, "forward opposes the axis, so the rotation is reversed");
                Check(true, Quaternion.AngleAxis(90f, Vector3.up), Quaternion.identity, "a dot of zero is not opposed");
                Check(true, Quaternion.identity, Quaternion.identity, "forward along the axis");
                Check(false, Quaternion.AngleAxis(180f, Vector3.up), Quaternion.identity, "without ReverseByDirection nothing is reversed");
            });
        }

        // ---- OnTargetType* without a registered target (06001045 @0x740550, 06001046, 06001047) ----

        // Entering stores the aim type before the target lookup (0x740591); with no entry for the type nothing else
        // happens, and neither leaving nor updating touches the lerp or timer state.
        [Test]
        public void TargetTypeEventsWithoutAnEntryOnlyRecordTheEnteredType()
        {
            WithAim((owned, character, ability, definition) =>
            {
                Set(ability, "m_aimLerpActive", true);
                Call(ability, "OnTargetTypeEnter", AbilityAimTargetType.Ledge);
                Assert.That(Get<AbilityAimTargetType>(ability, "m_aimType"), Is.EqualTo(AbilityAimTargetType.Ledge));
                Call(ability, "OnTargetTypeLeave", AbilityAimTargetType.Ledge);
                Call(ability, "OnTargetTypeUpdate", 0.1f);
                Assert.That(Get<bool>(ability, "m_cameraTargetTimerActive"), Is.False);
                Assert.That(Get<float>(ability, "m_cameraTargetTimer"), Is.EqualTo(0f));
                Assert.That(Get<bool>(ability, "m_aimLerpActive"), Is.True);
            });
        }

        // 0x74096d..0x740afa: the squared distance to the aim target is measured before SmoothDamp (0x7409cc). Only
        // when it exceeds 0.1 (VA 0x29a6bfc) does the scale latch take (0.1 >= the distance after smoothing) and the
        // scale time reset; otherwise the scale time advances while the latch is set. The scale is the curve at the
        // scale time while latched, else 1, and the first call only seeds the lerp. Only branch outcomes are asserted.
        [Test]
        public void UpdateUiLatchesTheScaleFromTheDistanceBeforeSmoothingAndAdvancesItOnlyWhileLatched()
        {
            WithAim((owned, character, ability, definition) =>
            {
                SetProperty(ability, "AirActive", true); // Reflection: SetAirActive(true) reads the save data settings (IsControlContinuous 0xE5D960), which need a started save manager
                definition.AimLerpSmoothTime = 1f;
                definition.AimLerpMaxSpeed = 10f;
                definition.AimLerpScaleAnim = AnimationCurve.Linear(0f, 2f, 1f, 4f);
                Set(ability, "m_cameraTargetTimerActive", true);
                Set(ability, "m_aimOffset", new Vector3(0f, 0f, 0.3f));
                var indicator = new UITargetIndicatorController();
                float Scale() => Get<float>(indicator, "m_arrowScaleMultiplier");

                Assert.That(ability.UpdateUI(0.1f, indicator, null), Is.False, "no target type entry");
                Assert.That(Get<bool>(ability, "m_aimLerpActive"), Is.True);
                Assert.That(Get<Vector3>(ability, "m_aimLerpPosition"), Is.EqualTo(new Vector3(0f, 0f, 0.3f)).Using(Vec));
                Assert.That(Scale(), Is.EqualTo(1f));

                // The distance to the target before smoothing (0.4, squared 0.16) is above 0.1 but the smoothed one is below it, so the latch takes
                // and the scale time restarts at zero. Measuring after SmoothDamp would leave the scale at 1.
                Set(ability, "m_aimPosition", new Vector3(0f, 0f, 0.4f));
                ability.UpdateUI(1f, indicator, null);
                Assert.That(Get<bool>(ability, "m_aimLerpScaleActive"), Is.True);
                Assert.That(Get<float>(ability, "m_aimLerpScaleTime"), Is.EqualTo(0f));
                Assert.That(Scale(), Is.EqualTo(2f).Within(Tolerance));

                // Now within 0.1 of the target before smoothing: the latched scale time advances.
                ability.UpdateUI(0.5f, indicator, null);
                Assert.That(Get<float>(ability, "m_aimLerpScaleTime"), Is.EqualTo(0.5f).Within(Tolerance));
                Assert.That(Scale(), Is.EqualTo(3f).Within(Tolerance));

                // Without the latch the time stays and the scale is 1.
                Set(ability, "m_aimLerpScaleActive", false);
                ability.UpdateUI(0.5f, indicator, null);
                Assert.That(Get<float>(ability, "m_aimLerpScaleTime"), Is.EqualTo(0.5f).Within(Tolerance));
                Assert.That(Scale(), Is.EqualTo(1f));
            });
        }

        // 0x7408e0..0x740920: the indicator update does nothing and reports false while the aim is not active.
        [Test]
        public void UpdateUiReportsNothingWhileTheAimIsInactive()
        {
            WithAim((owned, character, ability, definition) =>
            {
                Assert.That(ability.UpdateUI(0.1f, null, null), Is.False);
            });
        }
    }
}
