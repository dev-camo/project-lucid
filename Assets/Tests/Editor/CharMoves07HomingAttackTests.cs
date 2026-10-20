using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using ProjectLucid.Tests.CharCoreShared;
using UnityEngine;
using UnityEngine.Events;
using static ProjectLucid.Tests.CharacterAbilityTestSupport;
using static ProjectLucid.Tests.CharacterTriggerTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // The homing attack abilities (TypeDefs 0x020002fb..0x02000305 and 0x02000302): the detection box handling of
    // CharacterAbility_HomingAttackBase<T> (shared x86-64 bodies 0xE61410..0xE625E0), the combo, lock and target
    // update logic of CharacterAbility_HomingAttack<T> (0xE62660..0xE63AC0) and the air and ground leaves
    // (0x74E8D0..0x750450). Each case cites the native comparison it pins; the character is a real component on an
    // owned inactive host with a real homing pool, and the brain is the minimal concrete CharacterBrain. The members
    // that need a started character (spawning the detection box, the coroutine host, the boost stamina) are pinned by
    // their compiled call order instead.
    public sealed class CharMoves07HomingAttackTests
    {
        // The generic bodies are abstract-closed; this leaf adds only a knob over the one virtual the leaves override.
        private sealed class HomingAttackProbe : CharacterAbility_HomingAttack<CharacterAbilityDefinition_HomingAttackAirLightspeedDash>
        {
            public bool? ActivatedKnob;

            protected override bool HomingAttackActivated(CharacterBrain brain) => ActivatedKnob ?? base.HomingAttackActivated(brain);
        }

        private sealed class AppliedBrain : CharacterBrain
        {
            public override void Close()
            {
            }
        }

        private sealed class PlainMetaData : TargetObjectMetaData
        {
            public bool Targetable = true;

            public override bool IsTargetable => Targetable;

            public override void DoOnTargeted()
            {
            }
        }

        // The rows the constructors read are leased for the case: the character's island row, the App row of
        // CharacterAbility, the camera manager row of the homing ability and the message manager row of the pool. With
        // targets, a HomingTarget also reads the time and character manager rows and the time category lookup.
        private static void WithAttack<TAbility, TDefinition>(Action<Owned, CharacterGravityVectorProbe, TAbility, TDefinition> body, bool targets = false)
            where TAbility : CharacterAbility, new()
            where TDefinition : CharacterAbilityDefinition => Owned.Run(owned =>
        {
            owned.LeaseRow<GameplayIslandManager>();
            owned.LeaseRow<App>();
            owned.LeaseRow<MessageManager>();
            owned.LeaseRow<CinemachineCameraManager>();
            if (targets)
            {
                owned.LeaseRow<TimeManager>();
                owned.LeaseRow<CharacterManager>();
                FieldInfo config = typeof(TimeScaledComponent_SDT).GetField("s_config_Internal", BindingFlags.Static | BindingFlags.NonPublic);
                object prior = config.GetValue(null);
                owned.Lease(new ReleaseAction(() => config.SetValue(null, prior)));
                config.SetValue(null, owned.Track(ScriptableObject.CreateInstance<TimeCategoryLookup>()));
            }
            CharacterActionFlags.Initialise();
            CharacterGravityVectorProbe character = NewCharacter(owned);
            var priorities = new Dictionary<HomingTargetType, TargetingTypePriorityDefinition>();
            foreach (HomingTargetType type in new[] { HomingTargetType.Enemy, HomingTargetType.Ring })
            {
                var priority = owned.Track(ScriptableObject.CreateInstance<TargetingTypePriorityDefinition>());
                Set(priority, "m_targetType", type);
                Set(priority, "m_priority", 1);
                priorities[type] = priority;
            }
            SetProperty(character, "HomingPool", new CharacterHomingPool(character, priorities));
            var definition = owned.Track(ScriptableObject.CreateInstance<TDefinition>());
            var ability = new TAbility();
            Set(ability, "m_character", character);
            Set(ability, "m_definition", definition);
            body(owned, character, ability, definition);
        });

        private static HomingTarget NewTarget(Owned owned, string name, Vector3 position, HomingTargetType type = HomingTargetType.Enemy)
        {
            GameObject host = owned.Host(name);
            host.transform.position = position;
            var collider = host.AddComponent<BoxCollider>();
            var target = host.AddComponent<HomingTarget>();
            Set(target, "m_collider", collider);
            Set(target, "m_targetType", type);
            Physics.SyncTransforms();
            return target;
        }

        // A counter on one of the target's authored events, which Unity builds with the component.
        private static int[] CountEvent(HomingTarget target, string field)
        {
            var count = new int[1];
            if (field == "m_onDisable")
            {
                if (Get<UnityEvent<Collider>>(target, field) == null) Set(target, field, new UnityEvent<Collider>());
                Get<UnityEvent<Collider>>(target, field).AddListener(_ => count[0]++);
            }
            else
            {
                if (Get<UnityEvent>(target, field) == null) Set(target, field, new UnityEvent());
                Get<UnityEvent>(target, field).AddListener(() => count[0]++);
            }
            return count;
        }

        private static List<CharacterHomingPool.PooledTarget> Pooled(CharacterHomingPool pool)
        {
            var groups = (IDictionary)Get<object>(pool, "m_targetTypeToGroupMap");
            return groups.Values.Cast<object>().Distinct()
                .SelectMany(group => (List<CharacterHomingPool.PooledTarget>)group.GetType().GetProperty("PooledTargets").GetValue(group)).ToList();
        }

        private static void SetPending(CharacterHomingPool pool, IHomingAbility ability, GameObject target, TargetObjectMetaData metaData = null) =>
            Set(pool, "m_pendingTarget", new CharacterHomingPool.PooledTarget
            {
                Abilities = new List<IHomingAbility> { ability },
                Object = new CharacterTargetObjectData { Target = target, WorldRotation = Quaternion.identity, MetaData = metaData },
            });

        private static void Activate(CharacterHomingPool pool, IHomingAbility ability, GameObject target, TargetObjectMetaData metaData) =>
            pool.SetTargetImmediately(ability, new CharacterTargetObjectData { Target = target, WorldRotation = Quaternion.identity, MetaData = metaData }, Vector3.zero, 5f);

        private static HomingObjectMetaData MetaOf(HomingTarget target) => new HomingObjectMetaData { HomingTarget = target };

        private static AppliedBrain Brain(Character character, float timestamp, params GameAction[] actions)
        {
            var flags = new CharacterActionFlags();
            foreach (GameAction action in actions) flags.SetValue(action, true);
            var brain = new AppliedBrain();
            brain.ApplyActions(flags, timestamp);
            Set(character, "m_brain", brain);
            return brain;
        }

        // One brain that saw the action pressed and then released at the same time, so its timestamp for the action is
        // that time and nothing is queued.
        private static AppliedBrain Released(Character character, float timestamp, GameAction action)
        {
            var pressed = new CharacterActionFlags();
            pressed.SetValue(action, true);
            var brain = new AppliedBrain();
            brain.ApplyActions(pressed, timestamp);
            brain.ApplyActions(new CharacterActionFlags(), timestamp);
            Set(character, "m_brain", brain);
            return brain;
        }

        // ---- Detection box size and offset (0xE61410, 0xE61510, 0xE615F0) ----

        // The authored size (or the camera size while projecting from the camera) plus the stored adjustment, componentwise.
        // The camera choice is ShouldProjectFromCamera: the authored flag with no controller movement.
        [Test]
        public void TargetZoneSizeAddsTheAdjustmentToTheBoxOfTheCurrentProjection()
        {
            WithAttack<HomingAttackProbe, CharacterAbilityDefinition_HomingAttackAirLightspeedDash>((owned, character, ability, definition) =>
            {
                definition.TargetZoneSize = new Vector3(1f, 2f, 3f);
                definition.TargetZoneSizeFromCamera = new Vector3(10f, 20f, 30f);
                Set(ability, "m_targetZoneSizeAdjustment", new Vector3(0.5f, 0.25f, 2f));

                definition.ProjectFromCameraWhenNoDirectionInput = false;
                Assert.That(ability.TargetZoneSize, Is.EqualTo(new Vector3(1.5f, 2.25f, 5f)), "the authored flag is off");
                definition.ProjectFromCameraWhenNoDirectionInput = true;
                SetProperty(character, "RawControllerMovementMagnitude", 1f);
                Assert.That(ability.TargetZoneSize, Is.EqualTo(new Vector3(1.5f, 2.25f, 5f)), "controller input is being made");
                SetProperty(character, "RawControllerMovementMagnitude", 0f);
                Assert.That(ability.TargetZoneSize, Is.EqualTo(new Vector3(10.5f, 20.25f, 32f)), "no input: the camera box");
            });
        }

        [Test]
        public void TargetZoneOffsetFollowsTheCurrentProjection()
        {
            WithAttack<HomingAttackProbe, CharacterAbilityDefinition_HomingAttackAirLightspeedDash>((owned, character, ability, definition) =>
            {
                definition.CharacterTargetZoneOffset = new Vector3(1f, 1f, 1f);
                definition.CameraTargetZoneOffset = new Vector3(2f, 2f, 2f);
                SetProperty(character, "RawControllerMovementMagnitude", 1f);
                Assert.That(ability.TargetZoneOffset, Is.EqualTo(new Vector3(1f, 1f, 1f)), "controller input");
                SetProperty(character, "RawControllerMovementMagnitude", 0f);
                Assert.That(ability.TargetZoneOffset, Is.EqualTo(new Vector3(2f, 2f, 2f)), "no input");
                definition.ProjectFromCameraWhenNoDirectionInput = false;
                Assert.That(ability.TargetZoneOffset, Is.EqualTo(new Vector3(1f, 1f, 1f)), "the authored flag is off");
            });
        }

        [Test]
        public void CameraProjectionFlagIsTheAuthoredOne()
        {
            WithAttack<HomingAttackProbe, CharacterAbilityDefinition_HomingAttackAirLightspeedDash>((owned, character, ability, definition) =>
            {
                foreach (bool flag in new[] { true, false })
                {
                    definition.ProjectFromCameraWhenNoDirectionInput = flag;
                    Assert.That((bool)Call(ability, "get_ProjectFromCameraWhenNoDirectionInput"), Is.EqualTo(flag));
                }
            });
        }

        // ---- Detection box placement (0xE61DE0, 0xE62440, 0xE61D50) ----

        private static void WithBox(Action<Owned, CharacterGravityVectorProbe, HomingAttackProbe, CharacterAbilityDefinition_HomingAttackAirLightspeedDash, BoxCollider> body) =>
            WithAttack<HomingAttackProbe, CharacterAbilityDefinition_HomingAttackAirLightspeedDash>((owned, character, ability, definition) =>
            {
                GameObject host = owned.Host("Lucid detection box");
                var box = host.AddComponent<BoxCollider>();
                var attack = host.AddComponent<HomingAttack>();
                Set(attack, "m_collider", box);
                Set(ability, "m_homingAttack", attack);
                body(owned, character, ability, definition, box);
            });

        private static void AssertRotation(Quaternion actual, Quaternion expected, string message) =>
            Assert.That(Quaternion.Angle(actual, expected), Is.LessThan(0.01f), message);

        // 0xE62440: the box object is moved to the pose, the collider takes the size and offset, and only the depth
        // component of the stored adjustment is replaced (a store to +0x1A8).
        [Test]
        public void SettingTheBoxTransformReplacesOnlyTheDepthAdjustment()
        {
            WithBox((owned, character, ability, definition, box) =>
            {
                Set(ability, "m_targetZoneSizeAdjustment", new Vector3(1f, 2f, 3f));
                Quaternion rotation = Quaternion.Euler(0f, 90f, 0f);
                Call(ability, "SetHomingTargetTransform", new Vector3(4f, 5f, 6f), rotation, new Vector3(2f, 2f, 4f), new Vector3(0f, 1f, 0f), 0.75f);
                Assert.That(box.transform.position, Is.EqualTo(new Vector3(4f, 5f, 6f)));
                AssertRotation(box.transform.rotation, rotation, "rotation");
                Assert.That(box.size, Is.EqualTo(new Vector3(2f, 2f, 4f)));
                Assert.That(box.center, Is.EqualTo(new Vector3(0f, 1f, 2f)), "offset + forward * half the depth");
                Assert.That(Get<Vector3>(ability, "m_targetZoneSizeAdjustment"), Is.EqualTo(new Vector3(1f, 2f, 0.75f)));
            });
        }

        // 0xE61DE0, camera branch (0xE61E07..0xE6214C): the box sits at the camera pose with the camera box; the new depth
        // adjustment is the character's distance along the camera forward, stored after the box was sized with the old one.
        [Test]
        public void ProjectingFromTheCameraPlacesTheBoxAtTheCameraAndStoresTheDepthToTheCharacter()
        {
            WithBox((owned, character, ability, definition, box) =>
            {
                definition.TargetZoneSizeFromCamera = new Vector3(2f, 2f, 4f);
                definition.CameraTargetZoneOffset = new Vector3(0f, 1f, 0f);
                SetActorProperty(character, "WorldPosition", new Vector3(3f, 5f, 0f));
                Quaternion cameraRotation = Quaternion.Euler(0f, 90f, 0f);
                Set(character, "m_cameraPosition", new Vector3(0f, 5f, 0f));
                Set(character, "m_cameraRotation", cameraRotation);
                Set(character, "m_cameraForward", new Vector3(1f, 0f, 0f));

                Call(ability, "UpdateHomingTargetLocation");
                Assert.That(box.transform.position, Is.EqualTo(new Vector3(0f, 5f, 0f)));
                AssertRotation(box.transform.rotation, cameraRotation, "camera rotation");
                Assert.That(box.size, Is.EqualTo(new Vector3(2f, 2f, 4f)), "sized with the previous (zero) adjustment");
                Assert.That(box.center, Is.EqualTo(new Vector3(0f, 1f, 2f)));
                Assert.That(Get<Vector3>(ability, "m_targetZoneSizeAdjustment"), Is.EqualTo(new Vector3(0f, 0f, 3f)));

                Call(ability, "UpdateHomingTargetLocation");
                Assert.That(box.size, Is.EqualTo(new Vector3(2f, 2f, 7f)), "the next update sizes with the stored depth");
                Assert.That(box.center, Is.EqualTo(new Vector3(0f, 1f, 3.5f)));
            });
        }

        // 0xE61F65..0xE62148: with the camera projection off and ProjectFromCharacterForward set, the stored adjustment
        // is zeroed (+0x1A0..0x1A8) and the box takes the character's pose and the authored box.
        [Test]
        public void ProjectingFromTheCharactersForwardClearsTheAdjustmentAndUsesItsPose()
        {
            WithBox((owned, character, ability, definition, box) =>
            {
                definition.ProjectFromCameraWhenNoDirectionInput = false;
                definition.ProjectFromCharacterForward = true;
                definition.TargetZoneSize = new Vector3(1f, 1f, 2f);
                definition.CharacterTargetZoneOffset = new Vector3(0f, 0.5f, 0f);
                Set(ability, "m_targetZoneSizeAdjustment", new Vector3(1f, 2f, 3f));
                Quaternion rotation = Quaternion.Euler(0f, 30f, 0f);
                SetActorProperty(character, "WorldPosition", new Vector3(3f, 5f, 0f));
                SetActorProperty(character, "WorldRotation", rotation);

                Call(ability, "UpdateHomingTargetLocation");
                Assert.That(Get<Vector3>(ability, "m_targetZoneSizeAdjustment"), Is.EqualTo(Vector3.zero));
                Assert.That(box.transform.position, Is.EqualTo(new Vector3(3f, 5f, 0f)));
                AssertRotation(box.transform.rotation, rotation, "character rotation");
                Assert.That(box.size, Is.EqualTo(new Vector3(1f, 1f, 2f)), "no adjustment");
                Assert.That(box.center, Is.EqualTo(new Vector3(0f, 0.5f, 1f)));
            });
        }

        // 0xE62151..0xE62416: otherwise the box looks along the raw intended forward with the character's up direction.
        [Test]
        public void ProjectingAlongTheInputLooksAtTheIntendedForward()
        {
            WithBox((owned, character, ability, definition, box) =>
            {
                definition.ProjectFromCameraWhenNoDirectionInput = false;
                definition.TargetZoneSize = new Vector3(1f, 1f, 2f);
                Set(ability, "m_targetZoneSizeAdjustment", new Vector3(1f, 2f, 3f));
                SetActorProperty(character, "WorldPosition", new Vector3(3f, 5f, 0f));
                SetProperty(character, "RawIntendedForward", new Vector3(1f, 0f, 0f));
                SetActorProperty(character, "UpDirection", Vector3.up);

                Call(ability, "UpdateHomingTargetLocation");
                Assert.That(Get<Vector3>(ability, "m_targetZoneSizeAdjustment"), Is.EqualTo(Vector3.zero));
                Assert.That(box.transform.position, Is.EqualTo(new Vector3(3f, 5f, 0f)));
                AssertRotation(box.transform.rotation, Quaternion.LookRotation(new Vector3(1f, 0f, 0f), Vector3.up), "looks along +x");
                Assert.That(box.size, Is.EqualTo(new Vector3(1f, 1f, 2f)));
            });
        }

        // 0x74EC70: the one shot attack always places its box in front of the character, whatever the projection flags.
        [Test]
        public void TheOneShotAttackAlwaysPlacesItsBoxAtTheCharacter()
        {
            WithAttack<CharacterAbility_HomingAttackAirOneShot, CharacterAbilityDefinition_HomingAttackAirOneShot>((owned, character, ability, definition) =>
            {
                GameObject host = owned.Host("Lucid detection box");
                var box = host.AddComponent<BoxCollider>();
                var attack = host.AddComponent<HomingAttack>();
                Set(attack, "m_collider", box);
                Set(ability, "m_homingAttack", attack);
                definition.TargetZoneSize = new Vector3(1f, 1f, 2f);
                definition.TargetZoneSizeFromCamera = new Vector3(9f, 9f, 9f);
                Place(character, new Vector3(3f, 5f, 0f), Quaternion.Euler(0f, 30f, 0f));
                Set(character, "m_cameraPosition", new Vector3(100f, 100f, 100f));
                Set(ability, "m_targetZoneSizeAdjustment", new Vector3(0f, 0f, 3f));

                Call(ability, "UpdateHomingTargetLocation");
                Assert.That(box.transform.position, Is.EqualTo(new Vector3(3f, 5f, 0f)));
                AssertRotation(box.transform.rotation, Quaternion.Euler(0f, 30f, 0f), "character rotation");
                Assert.That(box.size, Is.EqualTo(new Vector3(9f, 9f, 12f)), "the camera box while no input is made, plus the stored depth, which is then reset");
                Assert.That(Get<Vector3>(ability, "m_targetZoneSizeAdjustment"), Is.EqualTo(Vector3.zero));
            });
        }

        private static void Place(Character character, Vector3 position, Quaternion rotation)
        {
            SetActorProperty(character, "WorldPosition", position);
            SetActorProperty(character, "WorldRotation", rotation);
        }

        // 0xE61D50: the box object follows the request unless the box has been destroyed (Unity null test).
        [Test]
        public void ActivatingTheBoxObjectTogglesItsActiveState()
        {
            WithBox((owned, character, ability, definition, box) =>
            {
                Call(ability, "ActivateHomingAttackObject", false);
                Assert.That(box.gameObject.activeSelf, Is.False);
                Call(ability, "ActivateHomingAttackObject", true);
                Assert.That(box.gameObject.activeSelf, Is.True);
                Call(ability, "<Initialise>b__11_1");
                Assert.That(box.gameObject.activeSelf, Is.False, "the disable callback");
                Call(ability, "<Initialise>b__11_0");
                Assert.That(box.gameObject.activeSelf, Is.True, "the enable callback");

                UnityEngine.Object.DestroyImmediate(box.gameObject);
                Assert.DoesNotThrow(() => Call(ability, "ActivateHomingAttackObject", true), "a destroyed box is skipped");
            });
        }

        // ---- Homing attack lock (0xE63040) ----

        private static void WithLock(Action<Owned, CharacterGravityVectorProbe, HomingAttackProbe, CharacterAbilityDefinition_HomingAttackAirLightspeedDash, GameObject> body) =>
            WithAttack<HomingAttackProbe, CharacterAbilityDefinition_HomingAttackAirLightspeedDash>((owned, character, ability, definition) =>
            {
                definition.HomingVelocityMin = 5f;
                definition.ComboTimeSettings = new TimeSettings_SDT[0];
                GameObject target = owned.Host("Lucid lock target");
                target.transform.position = new Vector3(0f, 0f, 20f);
                body(owned, character, ability, definition, target);
            });

        private static bool HandleAttack(object ability, float deltaTime = 0.02f) => (bool)Call(ability, "HandleAttack", null, deltaTime);

        // 0xE63040..0xE63461: the speed is the velocity along the direction to the target, raised to HomingVelocityMin and
        // capped at HomingVelocityMax (maxss then minss), the lock keeps the character's velocity as the restore velocity.
        [Test]
        public void TheLockSpeedIsTheVelocityAlongTheTargetClampedBetweenTheMinimumAndMaximum()
        {
            foreach ((float velocity, float expected) in new[] { (10f, 10f), (1f, 5f), (-10f, 5f), (200f, 80f), (80f, 80f) })
            {
                WithLock((owned, character, ability, definition, target) =>
                {
                    character.SetWorldVelocity(new Vector3(0f, 0f, velocity));
                    SetPending(character.HomingPool, ability, target, new PlainMetaData());
                    Assert.That(HandleAttack(ability), Is.True, "velocity " + velocity);
                    Assert.That(character.HomingPool.ActiveTarget.Speed, Is.EqualTo(expected).Within(1e-4f), "velocity " + velocity);
                    Assert.That(character.HomingPool.ActiveTarget.RestoreVelocity, Is.EqualTo(new Vector3(0f, 0f, velocity)));
                });
            }
        }

        // 0xE631D8..0xE63296: velocity from time divides the distance by the time, without the clamp; a zero time (ja at
        // 0xE63239 fails) falls back to the velocity path.
        [Test]
        public void TheLockSpeedCanComeFromTheTimeToTheTarget()
        {
            WithLock((owned, character, ability, definition, target) =>
            {
                definition.HomingVelocityFromTime = true;
                definition.HomingVelocityTime = 0.2f;
                character.SetWorldVelocity(new Vector3(0f, 0f, 10f));
                SetPending(character.HomingPool, ability, target, new PlainMetaData());
                Assert.That(HandleAttack(ability), Is.True);
                Assert.That(character.HomingPool.ActiveTarget.Speed, Is.EqualTo(100f).Within(1e-3f), "20 / 0.2, above the maximum of 80");
            });
            WithLock((owned, character, ability, definition, target) =>
            {
                definition.HomingVelocityFromTime = true;
                definition.HomingVelocityTime = 0f;
                character.SetWorldVelocity(new Vector3(0f, 0f, 10f));
                SetPending(character.HomingPool, ability, target, new PlainMetaData());
                Assert.That(HandleAttack(ability), Is.True);
                Assert.That(character.HomingPool.ActiveTarget.Speed, Is.EqualTo(10f).Within(1e-4f), "a zero time uses the velocity path");
            });
            WithLock((owned, character, ability, definition, target) =>
            {
                definition.HomingVelocityFromTime = false;
                definition.HomingVelocityTime = 0.2f;
                character.SetWorldVelocity(new Vector3(0f, 0f, 10f));
                SetPending(character.HomingPool, ability, target, new PlainMetaData());
                Assert.That(HandleAttack(ability), Is.True);
                Assert.That(character.HomingPool.ActiveTarget.Speed, Is.EqualTo(10f).Within(1e-4f), "the flag is off");
            });
        }

        [Test]
        public void NothingLocksWithoutAValidActivatedTargetableTarget()
        {
            WithLock((owned, character, ability, definition, target) =>
            {
                character.SetWorldVelocity(new Vector3(0f, 0f, 10f));
                Assert.That(HandleAttack(ability), Is.False, "no pending target");
                SetPending(character.HomingPool, ability, target, new PlainMetaData { Targetable = false });
                Assert.That(HandleAttack(ability), Is.False, "the target is not targetable");
                Assert.That(character.HomingPool.ActiveTarget, Is.Null);
                SetPending(character.HomingPool, ability, target, new PlainMetaData());
                ability.ActivatedKnob = false;
                Assert.That(HandleAttack(ability), Is.False, "the attack was not activated");
                Assert.That(character.HomingPool.ActiveTarget, Is.Null);
                ability.ActivatedKnob = null;
                Assert.That(HandleAttack(ability), Is.True);
                Assert.That(character.HomingPool.ActiveTarget, Is.Not.Null);
            });
        }

        // 0xE63000: a direction override only lets the attack through while the active target overrides targeting.
        [Test]
        public void ADirectionOverrideNeedsAnOverridingActiveTarget()
        {
            WithLock((owned, character, ability, definition, target) =>
            {
                Assert.That((bool)Call(ability, "HomingAttackActivated", new object[] { null }), Is.True, "no override");
                character.AddModifierOverride((int)DirectionModifierType.Override, Vector3.right);
                Assert.That((bool)Call(ability, "HomingAttackActivated", new object[] { null }), Is.False, "an override and no active target");
                var oneShot = new CharacterAbility_HomingAttackAirOneShot();
                Set(oneShot, "m_character", character);
                Activate(character.HomingPool, oneShot, target, new PlainMetaData());
                Assert.That((bool)Call(ability, "HomingAttackActivated", new object[] { null }), Is.False, "the active ability is not triggered");
                oneShot.IsTriggered = true;
                Assert.That((bool)Call(ability, "HomingAttackActivated", new object[] { null }), Is.True, "a triggered ability that overrides targeting");
            });
        }

        // ---- Combo (0xE63480, 0xE636A0, 0xE636E0) ----

        // With no combo time settings (a count at or past the array length) a hit only counts and restarts the buffer.
        [Test]
        public void AComboHitCountsAndRestartsTheBufferTimer()
        {
            WithLock((owned, character, ability, definition, target) =>
            {
                definition.ComboBufferTime = 0.3f;
                Call(ability, "AddComboHit");
                Call(ability, "AddComboHit");
                Assert.That(Get<int>(ability, "m_comboCount"), Is.EqualTo(2));
                Assert.That(Get<float>(ability, "m_comboTimer"), Is.EqualTo(0.3f));
            });
        }

        // 0xE636A0: a running buffer counts down and clamps at zero; once it is no longer positive the combo is cleared.
        [Test]
        public void TheComboBufferCountsDownAndClearsTheComboWhenItRunsOut()
        {
            foreach ((float timer, float delta, float expectedTimer, int expectedCount) in new[]
            {
                (0.2f, 0.1f, 0.1f, 3), (0.1f, 0.1f, 0f, 0), (0.2f, 0.5f, 0f, 0), (0f, 0.1f, 0f, 0),
            })
            {
                WithLock((owned, character, ability, definition, target) =>
                {
                    Set(ability, "m_comboCount", 3);
                    Set(ability, "m_comboTimer", timer);
                    Call(ability, "UpdateComboHits", delta);
                    Assert.That(Get<float>(ability, "m_comboTimer"), Is.EqualTo(expectedTimer).Within(1e-6f), "timer " + timer + ", delta " + delta);
                    Assert.That(Get<int>(ability, "m_comboCount"), Is.EqualTo(expectedCount), "timer " + timer + ", delta " + delta);
                });
            }
        }

        [Test]
        public void ClearingTheComboResetsTheCount()
        {
            WithLock((owned, character, ability, definition, target) =>
            {
                Set(ability, "m_comboCount", 4);
                Call(ability, "ClearComboHits");
                Assert.That(Get<int>(ability, "m_comboCount"), Is.EqualTo(0));
                Assert.That(Get<StackableDataHandle>(ability, "m_comboTimeOverride"), Is.Null);
            });
        }

        // ---- Pool registration (0xE619F0, 0xE61AF0, 0xE61B90, 0xE61BE0, 0xE62E30, 0xE62E90, 0xE63780) ----

        private static void WithTargets(Action<Owned, CharacterGravityVectorProbe, HomingAttackProbe, CharacterAbilityDefinition_HomingAttackAirLightspeedDash> body) =>
            WithAttack<HomingAttackProbe, CharacterAbilityDefinition_HomingAttackAirLightspeedDash>((owned, character, ability, definition) =>
            {
                definition.TargetTypes = new[] { HomingTargetType.Enemy };
                definition.ComboTimeSettings = new TimeSettings_SDT[0];
                body(owned, character, ability, definition);
            }, targets: true);

        // 0xE63780 (mask test, then HasTargetType on the target's type) feeds 0xE619F0, which pools the target with a
        // homing metadata record pointing back at it, and 0xE62E90, which removes it only for a valid target.
        [Test]
        public void AddingAValidTargetPoolsItWithItsMetaDataAndRemovingTakesItBack()
        {
            WithTargets((owned, character, ability, definition) =>
            {
                HomingTarget enemy = NewTarget(owned, "Lucid enemy", Vector3.zero);
                HomingTarget ring = NewTarget(owned, "Lucid ring", Vector3.zero, HomingTargetType.Ring);
                ability.AddTarget(ring);
                Assert.That(Pooled(character.HomingPool), Is.Empty, "the type is not listed");
                ability.AddTarget(enemy);
                CharacterHomingPool.PooledTarget pooled = Pooled(character.HomingPool).Single();
                Assert.That(pooled.Object.Target, Is.SameAs(enemy.gameObject));
                Assert.That(pooled.Abilities, Is.EqualTo(new IHomingAbility[] { ability }));
                Assert.That(((HomingObjectMetaData)pooled.Object.MetaData).HomingTarget, Is.SameAs(enemy));
                Assert.That(pooled.Object.Type, Is.EqualTo(HomingTargetType.Enemy));

                ability.RemoveTarget(ring);
                Assert.That(Pooled(character.HomingPool), Has.Count.EqualTo(1), "an unlisted target is ignored");
                ability.RemoveTarget(enemy);
                Assert.That(Pooled(character.HomingPool), Is.Empty);
            });
        }

        [Test]
        public void TheHomingMaskKeepsOutTargetsOnOtherLayers()
        {
            WithTargets((owned, character, ability, definition) =>
            {
                HomingTarget enemy = NewTarget(owned, "Lucid enemy", Vector3.zero);
                enemy.gameObject.layer = 5;
                definition.HomingMask = 1 << 5;
                ability.AddTarget(enemy);
                Assert.That(Pooled(character.HomingPool), Has.Count.EqualTo(1), "the mask includes the layer");
                ability.RemoveTarget(enemy);
                definition.HomingMask = 1 << 6;
                ability.AddTarget(enemy);
                Assert.That(Pooled(character.HomingPool), Is.Empty, "the mask excludes the layer");
            });
        }

        // 0xE61BE0: valid only while the ability is enabled, and for a target the mask and type lists accept.
        [Test]
        public void ATargetIsValidOnlyWhileTheAbilityIsEnabled()
        {
            WithTargets((owned, character, ability, definition) =>
            {
                HomingTarget enemy = NewTarget(owned, "Lucid enemy", Vector3.zero);
                HomingTarget ring = NewTarget(owned, "Lucid ring", Vector3.zero, HomingTargetType.Ring);
                Assert.That(ability.IsHomingTargetValid(enemy), Is.False, "not entered");
                ability.Enter();
                Assert.That(ability.IsHomingTargetValid(enemy), Is.True);
                Assert.That(ability.IsHomingTargetValid(ring), Is.False, "type not listed");
            });
        }

        // 0xE62E30: a hit counts as a combo hit, requests a target refresh, clears the active target, removes the target
        // from the pool and tells the target to disable (with the character's collider, here none).
        [Test]
        public void AHitCountsTheComboClearsTheActiveTargetAndDisablesTheTarget()
        {
            WithTargets((owned, character, ability, definition) =>
            {
                definition.ComboBufferTime = 0.3f;
                HomingTarget enemy = NewTarget(owned, "Lucid enemy", Vector3.zero);
                int[] disabled = CountEvent(enemy, "m_onDisable");
                ability.AddTarget(enemy);
                Activate(character.HomingPool, ability, enemy.gameObject, MetaOf(enemy));
                Set(ability, "m_targetRequiresRefresh", false);

                ability.OnHomingTargetHit(enemy);
                Assert.That(Get<int>(ability, "m_comboCount"), Is.EqualTo(1));
                Assert.That(Get<float>(ability, "m_comboTimer"), Is.EqualTo(0.3f));
                Assert.That(Get<bool>(ability, "m_targetRequiresRefresh"), Is.True);
                Assert.That(character.HomingPool.ActiveTarget, Is.Null);
                Assert.That(Pooled(character.HomingPool), Is.Empty);
                Assert.That(disabled[0], Is.EqualTo(1));
            });
        }

        // ---- Target update (0xE629B0) ----

        private static void WithTracking(Action<Owned, CharacterGravityVectorProbe, HomingAttackProbe, CharacterAbilityDefinition_HomingAttackAirLightspeedDash, HomingTarget> body) =>
            WithTargets((owned, character, ability, definition) =>
            {
                GameObject host = owned.Host("Lucid character bounds");
                Set(character, "m_capsuleColliderCollision", host.AddComponent<CapsuleCollider>());
                HomingTarget target = NewTarget(owned, "Lucid tracked", new Vector3(0f, 0f, 10f));
                Activate(character.HomingPool, ability, target.gameObject, MetaOf(target));
                character.Storage.SetValue(ActorFSMKeys.HomingTargetTime, 0f);
                body(owned, character, ability, definition, target);
            });

        private static bool UpdateTarget(HomingAttackProbe ability, float deltaTime, out ActiveHomingTarget active, out float time, out Vector3 toTarget, out float distanceSqr) =>
            ability.UpdateTarget(deltaTime, out active, out time, out toTarget, out distanceSqr);

        [Test]
        public void WithoutAnActiveTargetNothingIsUpdated()
        {
            WithTargets((owned, character, ability, definition) =>
            {
                Assert.That(UpdateTarget(ability, 0.1f, out ActiveHomingTarget active, out float time, out Vector3 toTarget, out float distanceSqr), Is.False);
                Assert.That((active, time, toTarget, distanceSqr), Is.EqualTo(((ActiveHomingTarget)null, 0f, Vector3.zero, 0f)));
            });
        }

        // 0xE62AA6: an active target whose metadata is not a homing record is reported but not touched.
        [Test]
        public void AnActiveTargetWithoutHomingMetaDataIsLeftAlone()
        {
            WithTargets((owned, character, ability, definition) =>
            {
                Activate(character.HomingPool, ability, owned.Host("Lucid plain target"), new PlainMetaData());
                Assert.That(UpdateTarget(ability, 0.1f, out ActiveHomingTarget active, out float time, out Vector3 toTarget, out float distanceSqr), Is.False);
                Assert.That(active, Is.SameAs(character.HomingPool.ActiveTarget));
                Assert.That((time, toTarget, distanceSqr), Is.EqualTo((0f, Vector3.zero, 0f)));
                Assert.That(character.HomingPool.ActiveTarget, Is.Not.Null, "nothing was cleared");
            });
        }

        // 0xE62AC8..0xE62B1F: the vector and its squared length are measured from the character's bounds centre to the
        // target's collider centre; a target in reach, in time and unobstructed (an empty collision mask casts nothing
        // that can hit) stays active and the clock advances by the step.
        [Test]
        public void AValidTargetInReachStaysActiveAndAdvancesTheClock()
        {
            WithTracking((owned, character, ability, definition, target) =>
            {
                int[] cancelled = CountEvent(target, "m_onCancelled");
                Assert.That(UpdateTarget(ability, 0.1f, out ActiveHomingTarget active, out float time, out Vector3 toTarget, out float distanceSqr), Is.True);
                Assert.That(time, Is.EqualTo(0.1f).Within(1e-6f));
                Assert.That(toTarget, Is.EqualTo(new Vector3(0f, 0f, 10f)).Using(new UnityEngine.TestTools.Utils.Vector3EqualityComparer(1e-4f)));
                Assert.That(distanceSqr, Is.EqualTo(100f).Within(1e-3f));
                Assert.That(character.HomingPool.ActiveTarget, Is.SameAs(active));
                Assert.That(cancelled[0], Is.EqualTo(0));
                Assert.That(character.Storage.GetValue(ActorFSMKeys.HomingTargetTime, -1f, false), Is.EqualTo(0.1f).Within(1e-6f), "the stored clock advanced");
            });
        }

        // 0xE62B82..0xE62C9E: the deactivation distance is compared with the squared distance with ja, so a target inside
        // it is released without being cancelled, while an expired one is cancelled either way.
        [Test]
        public void ATargetInsideTheDeactivationDistanceIsReleasedWithoutCancelling()
        {
            WithTracking((owned, character, ability, definition, target) =>
            {
                int[] cancelled = CountEvent(target, "m_onCancelled");
                definition.DeactivationDistance = 1000f;
                Assert.That(UpdateTarget(ability, 0.1f, out ActiveHomingTarget active, out float time, out Vector3 toTarget, out float distanceSqr), Is.False);
                Assert.That(character.HomingPool.ActiveTarget, Is.Null, "released");
                Assert.That(cancelled[0], Is.EqualTo(0));
            });
            WithTracking((owned, character, ability, definition, target) =>
            {
                int[] cancelled = CountEvent(target, "m_onCancelled");
                definition.DeactivationDistance = 100f;
                Assert.That(UpdateTarget(ability, 0.1f, out ActiveHomingTarget active, out float time, out Vector3 toTarget, out float distanceSqr), Is.True,
                    "a distance equal to the squared distance is not under it");
                Assert.That(cancelled[0], Is.EqualTo(0));
            });
            WithTracking((owned, character, ability, definition, target) =>
            {
                int[] cancelled = CountEvent(target, "m_onCancelled");
                definition.DeactivationDistance = 1000f;
                character.Storage.SetValue(ActorFSMKeys.HomingTargetTime, 5f);
                Assert.That(UpdateTarget(ability, 0.1f, out ActiveHomingTarget active, out float time, out Vector3 toTarget, out float distanceSqr), Is.False);
                Assert.That(cancelled[0], Is.EqualTo(1), "released and expired");
            });
        }

        // 0xE62C97: setb after ucomiss, so a time equal to the timeout is already out, one under it is in, and an
        // unordered (NaN) time counts as inside.
        [Test]
        public void TheTimeoutIsExclusiveAndAnUnorderedTimeCountsAsInside()
        {
            foreach ((float delta, bool expected) in new[] { (2.99f, true), (3f, false), (3.01f, false), (float.NaN, true) })
            {
                WithTracking((owned, character, ability, definition, target) =>
                {
                    int[] cancelled = CountEvent(target, "m_onCancelled");
                    Assert.That(UpdateTarget(ability, delta, out ActiveHomingTarget active, out float time, out Vector3 toTarget, out float distanceSqr), Is.EqualTo(expected), "step " + delta);
                    Assert.That(cancelled[0], Is.EqualTo(expected ? 0 : 1), "step " + delta);
                    Assert.That(character.HomingPool.ActiveTarget == null, Is.EqualTo(!expected), "step " + delta);
                });
            }
        }

        [Test]
        public void ATargetThatLeftTheHierarchyIsCancelled()
        {
            WithTracking((owned, character, ability, definition, target) =>
            {
                int[] cancelled = CountEvent(target, "m_onCancelled");
                target.gameObject.SetActive(false);
                Assert.That(UpdateTarget(ability, 0.1f, out ActiveHomingTarget active, out float time, out Vector3 toTarget, out float distanceSqr), Is.False);
                Assert.That(cancelled[0], Is.EqualTo(1));
                Assert.That(character.HomingPool.ActiveTarget, Is.Null);
            });
        }

        // ---- Activation (0xE626D0) ----

        [Test]
        public void ActivatingNotifiesTheTargetAndRecordsItsTypeAndAResetClock()
        {
            WithTracking((owned, character, ability, definition, target) =>
            {
                int[] active = CountEvent(target, "m_onActive");
                character.Storage.SetValue(ActorFSMKeys.HomingTargetTime, 5f);
                ability.SetActive(true);
                Assert.That(active[0], Is.EqualTo(1));
                Assert.That(character.Storage.TryGetValue(ActorFSMKeys.HomingTargetType, out HomingTargetType type) && type == HomingTargetType.Enemy, Is.True);
                Assert.That(character.Storage.GetValue(ActorFSMKeys.HomingTargetTime, -1f, false), Is.EqualTo(0f));
            });
        }

        [Test]
        public void ActivatingWithoutATargetOnlyResetsTheClock()
        {
            WithTargets((owned, character, ability, definition) =>
            {
                character.Storage.SetValue(ActorFSMKeys.HomingTargetTime, 5f);
                ability.SetActive(true);
                Assert.That(character.Storage.TryGetValue(ActorFSMKeys.HomingTargetType, out HomingTargetType _), Is.False);
                Assert.That(character.Storage.GetValue(ActorFSMKeys.HomingTargetTime, -1f, false), Is.EqualTo(0f));
            });
        }

        // 0xE62840..0xE62898: deactivating drops the active target unless the one shot flag is stored as true.
        [Test]
        public void DeactivatingDropsTheActiveTargetUnlessAOneShotIsActive()
        {
            WithTracking((owned, character, ability, definition, target) =>
            {
                character.Storage.SetValue(ActorFSMKeys.HomingAttackOneShotActive, true);
                ability.SetActive(false);
                Assert.That(character.HomingPool.ActiveTarget, Is.Not.Null, "a one shot is active");
                character.Storage.SetValue(ActorFSMKeys.HomingAttackOneShotActive, false);
                ability.SetActive(false);
                Assert.That(character.HomingPool.ActiveTarget, Is.Null, "a stored false does not hold it");
            });
            WithTracking((owned, character, ability, definition, target) =>
            {
                ability.SetActive(false);
                Assert.That(character.HomingPool.ActiveTarget, Is.Null, "no flag stored");
            });
        }

        // ---- Debug text (0xE63890) ----

        [Test]
        public void TheDebugTextAddsTheBoxDepthAndTheTargetingAngles()
        {
            CultureInfo prior = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            try
            {
                WithAttack<HomingAttackProbe, CharacterAbilityDefinition_HomingAttackAirLightspeedDash>((owned, character, ability, definition) =>
                {
                    definition.TargetZoneSize = new Vector3(1f, 2f, 3f);
                    definition.InputDiscardTargetAngle = 45f;
                    definition.TargetMaxPitchAngle = 12.5f;
                    var text = new StringBuilder();
                    Call(ability, "GetDebugExtraInfo", text);
                    Assert.That(text.ToString(), Is.EqualTo("Target = None ProjectDistance = 3 InputDiscardTargetAngle = 45.0 TargetMaxPitchAngle = 12.5 "));
                });
            }
            finally
            {
                CultureInfo.CurrentCulture = prior;
            }
        }

        // ---- Air leaves ----

        // 0x74E920 and 0x74E980: the lightspeed dash attack also needs the dash action applied and consumes it on a lock.
        [Test]
        public void TheLightspeedDashAttackNeedsAndConsumesTheDashAction()
        {
            WithAttack<CharacterAbility_HomingAttackAirLightspeedDash, CharacterAbilityDefinition_HomingAttackAirLightspeedDash>((owned, character, ability, definition) =>
            {
                definition.ComboTimeSettings = new TimeSettings_SDT[0];
                GameObject target = owned.Host("Lucid dash target");
                target.transform.position = new Vector3(0f, 0f, 20f);
                AppliedBrain idle = Brain(character, 1f);
                Assert.That((bool)Call(ability, "HomingAttackActivated", idle), Is.False, "the dash action is not applied");
                AppliedBrain dash = Brain(character, 2f, GameAction.CharacterLightspeedDash);
                Assert.That((bool)Call(ability, "HomingAttackActivated", dash), Is.True);

                character.SetWorldVelocity(new Vector3(0f, 0f, 10f));
                SetPending(character.HomingPool, ability, target, new PlainMetaData());
                Assert.That((bool)Call(ability, "HandleAttack", dash, 0.02f), Is.True);
                Assert.That(dash.ActionStateQueued.GetValue(GameAction.CharacterLightspeedDash), Is.False, "the dash action was ended");

                character.HomingPool.ClearTarget();
                AppliedBrain second = Brain(character, 3f, GameAction.CharacterLightspeedDash);
                Assert.That((bool)Call(ability, "HandleAttack", second, 0.02f), Is.False, "no pending target any more");
                Assert.That(second.ActionStateQueued.GetValue(GameAction.CharacterLightspeedDash), Is.True, "nothing ended it");
            });
        }

        // 0x74F590 and 0x74F690.
        [Test]
        public void TheAirTargetAttackNeedsAndConsumesTheHomingAttackPress()
        {
            WithAttack<CharacterAbility_HomingAttackAirTarget, CharacterAbilityDefinition_HomingAttackAirTarget>((owned, character, ability, definition) =>
            {
                definition.ComboTimeSettings = new TimeSettings_SDT[0];
                GameObject target = owned.Host("Lucid air target");
                target.transform.position = new Vector3(0f, 0f, 20f);
                Assert.That((bool)Call(ability, "HomingAttackActivated", Brain(character, 1f)), Is.False, "no press");
                AppliedBrain press = Brain(character, 6f, GameAction.CharacterHomingAttack);
                Assert.That((bool)Call(ability, "HomingAttackActivated", press), Is.True);

                character.SetWorldVelocity(new Vector3(0f, 0f, 10f));
                SetPending(character.HomingPool, ability, target, new PlainMetaData());
                Assert.That((bool)Call(ability, "HandleAttack", press, 0.02f), Is.True);
                Assert.That(character.Storage.GetValue(ActorFSMKeys.HomingAttackActionTimestamp, -1f, false), Is.EqualTo(6f), "the press time of the homing attack action");
                Assert.That(press.ActionStateQueued.GetValue(GameAction.CharacterHomingAttack), Is.False, "the press was consumed");
            });
        }

        // ---- One shot leaf ----

        private static void WithOneShot(Action<Owned, CharacterGravityVectorProbe, CharacterAbility_HomingAttackAirOneShot, CharacterAbilityDefinition_HomingAttackAirOneShot, GameObject> body,
            bool targets = false) =>
            WithAttack<CharacterAbility_HomingAttackAirOneShot, CharacterAbilityDefinition_HomingAttackAirOneShot>((owned, character, ability, definition) =>
            {
                definition.ComboTimeSettings = new TimeSettings_SDT[0];
                GameObject target = owned.Host("Lucid one shot target");
                target.transform.position = new Vector3(0f, 0f, 20f);
                body(owned, character, ability, definition, target);
            }, targets);

        // 0x74F130 and 0x74F190 compare the window with the elapsed time using ja: strictly inside.
        [Test]
        public void TheOneShotWindowsAreExclusive()
        {
            WithOneShot((owned, character, ability, definition, target) =>
            {
                definition.TriggerTimeBefore = 0.3f;
                definition.TriggerTimeAfter = 0.1f;
                Assert.That(ability.CanTriggerBefore(0.2f), Is.True);
                Assert.That(ability.CanTriggerBefore(0.3f), Is.False);
                Assert.That(ability.CanTriggerBefore(0.4f), Is.False);
                Assert.That(ability.CanTriggerAfter(0.05f), Is.True);
                Assert.That(ability.CanTriggerAfter(0.1f), Is.False);
                Assert.That(ability.OverridesTargeting, Is.True);
            });
        }

        private static void ReadyForOneShot(Owned owned, CharacterGravityVectorProbe character, CharacterAbility_HomingAttackAirOneShot ability, GameObject target, float storedTimestamp)
        {
            SetPending(character.HomingPool, ability, target);
            character.Storage.SetValue(ActorFSMKeys.HomingAttackActionTimestamp, storedTimestamp);
            Set(character, "m_totalFixedTime", 10f);
        }

        // 0x74ED50..0x74EF4A: the base gate, no active target, a valid pending target and a stored previous timestamp are all
        // required; a queued one shot press fires at once; otherwise the press time decides inside the before or after window
        // around the clock, unless it equals the stored previous timestamp.
        [Test]
        public void TheOneShotNeedsAPendingTargetNoActiveTargetAndAStoredTimestamp()
        {
            WithOneShot((owned, character, ability, definition, target) =>
            {
                AppliedBrain queued = Brain(character, 9f, GameAction.CharacterHomingAttackOneShot);
                Assert.That((bool)Call(ability, "HomingAttackActivated", queued), Is.False, "no pending target");
                SetPending(character.HomingPool, ability, target);
                Assert.That((bool)Call(ability, "HomingAttackActivated", queued), Is.False, "no stored timestamp");
                Assert.That(ability.IsTriggered, Is.False);
                character.Storage.SetValue(ActorFSMKeys.HomingAttackActionTimestamp, 1f);
                Assert.That((bool)Call(ability, "HomingAttackActivated", queued), Is.True);
                Assert.That(ability.IsTriggered, Is.True);

                ability.IsTriggered = false;
                Activate(character.HomingPool, ability, target, new PlainMetaData());
                Assert.That((bool)Call(ability, "HomingAttackActivated", queued), Is.False, "a target is already active");
                Assert.That(ability.IsTriggered, Is.False);
            });
        }

        [Test]
        public void ACompletedOneShotPressFiresInsideTheWindowAroundTheClock()
        {
            foreach ((float pressTime, float before, float after, bool expected) in new[]
            {
                (9.9f, 0.25f, 0.05f, true), (9.9f, 0.05f, 0.25f, false), (10.1f, 0.05f, 0.25f, true), (10.1f, 0.25f, 0.05f, false),
            })
            {
                WithOneShot((owned, character, ability, definition, target) =>
                {
                    definition.TriggerTimeBefore = before;
                    definition.TriggerTimeAfter = after;
                    ReadyForOneShot(owned, character, ability, target, 1f);
                    AppliedBrain released = Released(character, pressTime, GameAction.CharacterHomingAttackOneShot);
                    Assert.That((bool)Call(ability, "HomingAttackActivated", released), Is.EqualTo(expected), "press " + pressTime + ", before " + before + ", after " + after);
                    Assert.That(ability.IsTriggered, Is.EqualTo(expected));
                });
            }
        }

        [Test]
        public void AOneShotPressAtTheStoredTimestampIsIgnored()
        {
            WithOneShot((owned, character, ability, definition, target) =>
            {
                definition.TriggerTimeBefore = 5f;
                definition.TriggerTimeAfter = 5f;
                ReadyForOneShot(owned, character, ability, target, 10f);
                AppliedBrain released = Released(character, 10.00005f, GameAction.CharacterHomingAttackOneShot);
                Assert.That((bool)Call(ability, "HomingAttackActivated", released), Is.False, "within 0.0001 of the stored timestamp");
                Assert.That(ability.IsTriggered, Is.False);
            });
        }

        // 0x74F200: a lock stores the time of the one shot press for the next attack.
        [Test]
        public void ALockRecordsWhenTheOneShotWasPressed()
        {
            WithOneShot((owned, character, ability, definition, target) =>
            {
                character.SetWorldVelocity(new Vector3(0f, 0f, 10f));
                SetPending(character.HomingPool, ability, target, new PlainMetaData());
                character.Storage.SetValue(ActorFSMKeys.HomingAttackActionTimestamp, 1f);
                AppliedBrain pressed = Brain(character, 7f, GameAction.CharacterHomingAttackOneShot);
                Assert.That((bool)Call(ability, "HandleAttack", pressed, 0.02f), Is.True);
                Assert.That(character.Storage.GetValue(ActorFSMKeys.HomingAttackActionTimestamp, -1f, false), Is.EqualTo(7f));
            });
            WithOneShot((owned, character, ability, definition, target) =>
            {
                character.Storage.SetValue(ActorFSMKeys.HomingAttackActionTimestamp, 1f);
                AppliedBrain pressed = Brain(character, 7f, GameAction.CharacterHomingAttackOneShot);
                Assert.That((bool)Call(ability, "HandleAttack", pressed, 0.02f), Is.False, "no pending target");
                Assert.That(character.Storage.GetValue(ActorFSMKeys.HomingAttackActionTimestamp, -1f, false), Is.EqualTo(1f), "nothing recorded without a lock");
            });
        }

        // 0x74F350: the one shot flag is stored before the base activation and removed before it, so deactivating
        // reaches the base with the flag already gone and drops the active target.
        [Test]
        public void TheOneShotFlagTracksActivationAndIsRemovedBeforeTheBaseDeactivates()
        {
            WithOneShot((owned, character, ability, definition, target) =>
            {
                ability.SetActive(true);
                Assert.That(character.Storage.TryGetValue(ActorFSMKeys.HomingAttackOneShotActive, out bool active) && active, Is.True);
                Activate(character.HomingPool, ability, target, new PlainMetaData());
                ability.SetActive(false);
                Assert.That(character.Storage.TryGetValue(ActorFSMKeys.HomingAttackOneShotActive, out bool _), Is.False, "the flag was removed");
                Assert.That(character.HomingPool.ActiveTarget, Is.Null, "the base saw no flag and dropped the target");
            });
        }

        // 0x74EA30 and 0x74EB40: the hit callback is hooked onto every added target and unhooked on removal, whether or
        // not the pool accepted the target, and a hit simply removes the target.
        [Test]
        public void TheOneShotHooksTheHitCallbackOntoEveryTargetItIsGiven()
        {
            WithOneShot((owned, character, ability, definition, target) =>
            {
                definition.TargetTypes = new[] { HomingTargetType.Enemy };
                HomingTarget enemy = NewTarget(owned, "Lucid enemy", Vector3.zero);
                HomingTarget ring = NewTarget(owned, "Lucid ring", Vector3.zero, HomingTargetType.Ring);
                ability.AddTarget(enemy);
                ability.AddTarget(ring);
                Assert.That(enemy.ActionOnCharacterHit.GetInvocationList().Single().Method.Name, Is.EqualTo("ActionOnCharacterHit"));
                Assert.That(ring.ActionOnCharacterHit, Is.Not.Null, "hooked although the pool refused it");
                Assert.That(Pooled(character.HomingPool), Has.Count.EqualTo(1));

                enemy.ActionOnCharacterHit(enemy);
                Assert.That(Pooled(character.HomingPool), Is.Empty, "a hit removes the target");
                ability.RemoveTarget(enemy);
                ability.RemoveTarget(ring);
                Assert.That(enemy.ActionOnCharacterHit, Is.Null);
                Assert.That(ring.ActionOnCharacterHit, Is.Null);
            }, targets: true);
        }

        // ---- Ground leaves ----

        // 0x750010 and 0x750110: a queued spin dash charge blocks the rolling attack, and the trigger follows a lock.
        [Test]
        public void TheRollingAttackIsBlockedByASpinDashChargeAndTriggersOnALock()
        {
            WithAttack<CharacterAbility_HomingAttackGroundRolling, CharacterAbilityDefinition_HomingAttackGroundRolling>((owned, character, ability, definition) =>
            {
                definition.ComboTimeSettings = new TimeSettings_SDT[0];
                GameObject target = owned.Host("Lucid rolling target");
                target.transform.position = new Vector3(0f, 0f, 20f);
                character.SetWorldVelocity(new Vector3(0f, 0f, 10f));
                AppliedBrain charging = Brain(character, 1f, GameAction.CharacterSpinDashCharge);
                Assert.That((bool)Call(ability, "HomingAttackActivated", charging), Is.False);
                SetPending(character.HomingPool, ability, target, new PlainMetaData());
                Assert.That((bool)Call(ability, "HandleAttack", charging, 0.02f), Is.False);
                Assert.That(ability.IsTriggered, Is.False);

                AppliedBrain free = Brain(character, 2f);
                Assert.That((bool)Call(ability, "HomingAttackActivated", free), Is.True);
                Assert.That((bool)Call(ability, "HandleAttack", free, 0.02f), Is.True);
                Assert.That(ability.IsTriggered, Is.True);
            });
        }

        // 0x750210: the running attack needs a queued homing attack press.
        [Test]
        public void TheRunningAttackNeedsAQueuedHomingAttackPress()
        {
            WithAttack<CharacterAbility_HomingAttackGroundRunning, CharacterAbilityDefinition_HomingAttackGroundRunning>((owned, character, ability, definition) =>
            {
                Assert.That((bool)Call(ability, "HomingAttackActivated", Brain(character, 1f)), Is.False);
                Assert.That((bool)Call(ability, "HomingAttackActivated", Brain(character, 2f, GameAction.CharacterHomingAttack)), Is.True);
            });
        }

        // 0x74FF30 hands the definition's forward motion (+0x170) to the ability base with the step, so a standing character
        // gains the motion's own acceleration towards its minimum speed.
        [Test]
        public void TheGroundAttackAcceleratesWithItsForwardMotion()
        {
            WithAttack<CharacterAbility_HomingAttackGroundRolling, CharacterAbilityDefinition_HomingAttackGroundRolling>((owned, character, ability, definition) =>
            {
                Vector3 forward = Vector3.forward;
                foreach ((float acceleration, float expected) in new[] { (5f, 0.1f), (10f, 0.2f) })
                {
                    definition.ForwardMotion = new CharacterAbilityDefinition.Motion { SpeedMin = 2f, SpeedMax = 10f, Acceleration = acceleration };
                    float speed = 0f;
                    ability.UpdateForwardSpeed(ref speed, ref forward, 0.02f);
                    Assert.That(speed, Is.EqualTo(expected).Within(1e-5f), "acceleration " + acceleration);
                }
            });
        }

        // ---- Call order of the members that need a started character ----

        // The CallOrder tests are emission checks of the compiled call sequence: they see no branch polarity, so the
        // conditions around these calls still lack native behaviour coverage.
        private static void AssertOrder(List<MethodBase> calls, params (string Type, string Name)[] expected)
        {
            int at = -1;
            foreach ((string type, string name) in expected) at = IndexOf(calls, type, name, at + 1);
        }

        // 0xE61660: the ability base initialises first, then the box prefab is spawned beside the character, initialised
        // with this ability, its rigid body fetched and the two callbacks allocated before the box is disabled.
        [Test]
        public void CallOrderInitialiseSpawnsTheBoxThenHooksItsCallbacksAndDisablesIt()
        {
            List<MethodBase> calls = Calls(typeof(CharacterAbility_HomingAttackBase<>), "Initialise");
            AssertOrder(calls, ("CharacterAbility", "Initialise"), ("ObjectUtils", "SpawnPrefab"), ("HomingAttack", "Initialise"),
                ("Component", "GetComponent"), ("Action", ".ctor"), ("CharacterAbility_HomingAttackBase", "EnableHomingTarget"));
            Assert.That(Count(calls, "Action", ".ctor"), Is.EqualTo(2));
        }

        // 0xE61850: the base closes first, then the box object is destroyed.
        [Test]
        public void CallOrderCloseDestroysTheBoxAfterTheBase()
        {
            AssertOrder(Calls(typeof(CharacterAbility_HomingAttackBase<>), "Close"), ("CharacterAbility", "Close"), ("Component", "get_gameObject"), ("Object", "Destroy"));
        }

        // 0xE618D0 and 0xE61970: the targeting entry or exit first, then the box is enabled or disabled.
        [Test]
        public void CallOrderEnteringAndLeavingToggleTheBoxAfterTheBase()
        {
            AssertOrder(Calls(typeof(CharacterAbility_HomingAttackBase<>), "DoOnEnter"), ("CharacterAbility_TargetingHoming", "DoOnEnter"), ("CharacterAbility_HomingAttackBase", "EnableHomingTarget"));
            AssertOrder(Calls(typeof(CharacterAbility_HomingAttackBase<>), "DoOnLeave"), ("CharacterAbility_TargetingHoming", "DoOnLeave"), ("CharacterAbility_HomingAttackBase", "EnableHomingTarget"));
        }

        // 0xE61D10: collisions are switched first, then the matching callback is scheduled for the next frame.
        [Test]
        public void CallOrderEnablingTheBoxSwitchesCollisionsBeforeSchedulingTheCallback()
        {
            AssertOrder(Calls(typeof(CharacterAbility_HomingAttackBase<>), "EnableHomingTarget"), ("Rigidbody", "set_detectCollisions"), ("CoroutineUtils", "OnNextFrame"));
        }

        // 0xE61C30.
        [Test]
        public void CallOrderUpdatingRunsTheBaseThenPlacesTheBoxThenHandlesTheAttack()
        {
            AssertOrder(Calls(typeof(CharacterAbility_HomingAttackBase<>), "DoOnUpdate"), ("CharacterAbility_Targeting", "DoOnUpdate"),
                ("CharacterAbility_HomingAttackBase", "UpdateHomingTargetLocation"), ("CharacterAbility_HomingAttackBase", "HandleAttack"));
        }

        // 0xE62F40: the base leave, the combo cleared, the trigger lowered, then an owned active target dropped.
        [Test]
        public void CallOrderLeavingClearsTheComboLowersTheTriggerAndDropsAnOwnedTarget()
        {
            AssertOrder(Calls(typeof(CharacterAbility_HomingAttack<>), "DoOnLeave"), ("CharacterAbility_HomingAttackBase", "DoOnLeave"), ("CharacterAbility_HomingAttack", "ClearComboHits"),
                ("CharacterAbility_TargetingHoming", "set_IsTriggered"), ("CharacterHomingPool", "IsTargetActive"), ("CharacterHomingPool", "ClearTarget"));
        }

        // 0x74E8D0: the box is enabled by the base, then the dash impulse of the character ends.
        [Test]
        public void CallOrderTheLightspeedDashAttackEndsTheDashImpulseOnEntry()
        {
            AssertOrder(Calls(typeof(CharacterAbility_HomingAttackAirLightspeedDash), "DoOnEnter"), ("CharacterAbility_HomingAttackBase", "DoOnEnter"), ("Character", "EndLightspeedDashImpulse"));
        }

        // 0x74F8A0: the box update, the controller test, the box enabled for it, an unmoved character taken off the
        // pool, and a triggered attack feeding the boost stamina.
        [Test]
        public void CallOrderTheGroundAttackUpdateFeedsTheBoostStaminaLast()
        {
            AssertOrder(Calls(typeof(CharacterAbility_HomingAttackGround), "DoOnUpdate"), ("CharacterAbility_HomingAttackBase", "DoOnUpdate"), ("Character", "get_HasControllerMovement"),
                ("CharacterAbility_HomingAttackBase", "EnableHomingTarget"), ("CharacterHomingPool", "RemoveTarget"), ("Character", "get_BoostStamina"), ("CharacterStamina", "UpdateFromAbility"));
        }

        // 0x7502B0 and 0x7503B0: the running attack's trigger follows the boost stamina; its target test also needs the
        // stamina free and able to trigger.
        [Test]
        public void CallOrderTheRunningAttackReadsTheBoostStaminaAfterTheBase()
        {
            AssertOrder(Calls(typeof(CharacterAbility_HomingAttackGroundRunning), "HandleAttack"), ("CharacterAbility_HomingAttack", "HandleAttack"), ("Character", "get_BoostStamina"),
                ("CharacterStamina", "get_IsTriggered"), ("CharacterBrain", "get_ActionStateQueued"), ("CharacterBrain", "SetState"));
            AssertOrder(Calls(typeof(CharacterAbility_HomingAttackGroundRunning), "ValidateTargetType"), ("CharacterAbility_HomingAttackGround", "ValidateTargetType"),
                ("Character", "get_BoostStamina"), ("CharacterStamina", "get_IsTriggered"), ("Character", "get_BoostStamina"), ("CharacterStamina", "CanTrigger"));
        }
    }
}
