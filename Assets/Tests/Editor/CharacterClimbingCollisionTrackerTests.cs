using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.CharacterTriggerTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // Restored Game.Runtime CharacterClimbingCollisionTracker (TypeDef 0x0200040c, x86-64 0x795240..0x795E63). The tracker
    // is a plain object built by its real constructor around a real Character component on an owned host that never wakes.
    // Contacts (a Collision cannot be built here) and the ground raycast (it needs the track manager) are witnessed on the
    // compiled bodies; the ground check itself is run with an empty layer mask, which no ray can hit.
    public sealed class CharacterClimbingCollisionTrackerTests
    {
        private const int NoLayers = 0;

        private static CharacterClimbingCollisionTracker NewTracker(Owned owned, Character character, LayerMask mask)
        {
            // The ground check reads the character's bounds, its half height, its up direction and its collider settings.
            Set(character, "m_capsuleColliderCollision", owned.Host("Lucid climbing capsule", false).AddComponent<CapsuleCollider>());
            var settings = new CharacterSettings { Collider = new CharacterSettings.ColliderSettings() };
            typeof(Character).GetProperty("Settings").GetSetMethod(true).Invoke(character, new object[] { settings });
            SetActorProperty(character, "UpDirection", Vector3.up);
            return new CharacterClimbingCollisionTracker(character, mask);
        }

        private static void SetPrivate(object target, string property, object value) =>
            target.GetType().GetProperty(property).GetSetMethod(true).Invoke(target, new[] { value });

        private static string[] CallNames(MethodInfo method) => OriginalILCallSequence.Names(OriginalILCallSequence.Calls(method));

        private static MethodInfo Method(string name) => typeof(CharacterClimbingCollisionTracker).GetMethod(name, Declared);

        // The declarations come from the original DummyDll metadata.
        [Test]
        public void DeclarationsKeepTheOriginalFlagsFieldsAndMethods()
        {
            AssertMembers(typeof(CharacterClimbingCollisionTracker), 0x00100001,
                new[]
                {
                    ("LayerMask", 0x0026), ("<WorldVelocityAtLastTerrainEnter>k__BackingField", 0x0001), ("<CollisionNormal>k__BackingField", 0x0001),
                    ("<IsColliding>k__BackingField", 0x0001), ("m_accruedCollisionNormals", 0x0001), ("m_collisionCount", 0x0001),
                    ("m_character", 0x0021), ("m_started", 0x0001)
                },
                new[]
                {
                    ("get_WorldVelocityAtLastTerrainEnter", 0x0886), ("set_WorldVelocityAtLastTerrainEnter", 0x0881), ("get_CollisionNormal", 0x0886),
                    ("set_CollisionNormal", 0x0881), ("get_IsColliding", 0x0886), ("set_IsColliding", 0x0881), (".ctor", 0x1886),
                    ("Start", 0x0086), ("Stop", 0x0086), ("Update", 0x0086), ("OnCollisionDataEnter", 0x0081), ("OnCollisionDataStay", 0x0081),
                    ("TryAddCollision", 0x0081), ("TerrainClimbable", 0x0096), ("PerformClimbableGroundCheck", 0x0081)
                });
            AssertIl2CppOptions(typeof(CharacterClimbingCollisionTracker), 1, 2);
            Assert.That(typeof(CharacterClimbingCollisionTracker).BaseType, Is.EqualTo(typeof(object)));
        }

        // The constructor 0x7952A0 stores the character (+0x40) and the layer mask (+0x10); everything else starts at zero.
        [Test]
        public void ConstructorStoresTheCharacterAndTheLayerMask()
        {
            WithManager((owned, manager, character) =>
            {
                LayerMask mask = 1 << 9;
                var tracker = new CharacterClimbingCollisionTracker(character, mask);

                Assert.That(tracker.LayerMask.value, Is.EqualTo(1 << 9));
                Assert.That(Get<Character>(tracker, "m_character"), Is.SameAs(character));
                Assert.That(tracker.IsColliding, Is.False);
                Assert.That(tracker.CollisionNormal, Is.EqualTo(Vector3.zero));
                Assert.That(tracker.WorldVelocityAtLastTerrainEnter, Is.EqualTo(Vector3.zero));
                Assert.That(Get<int>(tracker, "m_collisionCount"), Is.EqualTo(0));
                Assert.That(Get<bool>(tracker, "m_started"), Is.False);
            });
        }

        // Start 0x7952E0 combines OnCollisionDataEnter into the character's enter event (+0x3b8) and OnCollisionDataStay into
        // its stay event (+0x3c0) without testing for an earlier subscription, and marks the tracker started (+0x48). Stop
        // 0x795470 clears the state (colliding flag, normal, count, accrued normals), removes both handlers and clears the
        // started flag; the exit event is never used.
        [Test]
        public void StartSubscribesToTheEnterAndStayEventsAndStopClearsTheState()
        {
            WithManager((owned, manager, character) =>
            {
                var tracker = new CharacterClimbingCollisionTracker(character, NoLayers);

                tracker.Start();
                Assert.That(HandlerNames(character.OnCollisionDataEnterEvent), Is.EqualTo(new[] { "OnCollisionDataEnter" }));
                Assert.That(HandlerNames(character.OnCollisionDataStayEvent), Is.EqualTo(new[] { "OnCollisionDataStay" }));
                Assert.That(character.OnCollisionDataExitEvent, Is.Null);
                Assert.That(character.OnCollisionDataEnterEvent.GetInvocationList().Select(handler => handler.Target), Is.EqualTo(new object[] { tracker }));
                Assert.That(Get<bool>(tracker, "m_started"), Is.True);

                tracker.Start();
                Assert.That(HandlerNames(character.OnCollisionDataEnterEvent).Length, Is.EqualTo(2), "starting again subscribes again");
                Assert.That(HandlerNames(character.OnCollisionDataStayEvent).Length, Is.EqualTo(2));

                SetPrivate(tracker, "IsColliding", true);
                SetPrivate(tracker, "CollisionNormal", new Vector3(7f, 7f, 7f));
                Set(tracker, "m_collisionCount", 4);
                Set(tracker, "m_accruedCollisionNormals", new Vector3(1f, 2f, 3f));
                SetPrivate(tracker, "WorldVelocityAtLastTerrainEnter", new Vector3(5f, 5f, 5f));

                tracker.Stop();
                Assert.That(HandlerNames(character.OnCollisionDataEnterEvent), Is.EqualTo(new[] { "OnCollisionDataEnter" }), "one subscription removed");
                Assert.That(HandlerNames(character.OnCollisionDataStayEvent), Is.EqualTo(new[] { "OnCollisionDataStay" }));
                Assert.That(Get<bool>(tracker, "m_started"), Is.False);
                Assert.That(tracker.IsColliding, Is.False);
                Assert.That(tracker.CollisionNormal, Is.EqualTo(Vector3.zero));
                Assert.That(Get<int>(tracker, "m_collisionCount"), Is.EqualTo(0));
                Assert.That(Get<Vector3>(tracker, "m_accruedCollisionNormals"), Is.EqualTo(Vector3.zero));
                Assert.That(tracker.WorldVelocityAtLastTerrainEnter, Is.EqualTo(new Vector3(5f, 5f, 5f)), "the last terrain velocity is kept");

                tracker.Stop();
                Assert.That(HandlerNames(character.OnCollisionDataEnterEvent), Is.Empty);
                Assert.That(HandlerNames(character.OnCollisionDataStayEvent), Is.Empty);
            });
        }

        private static (bool colliding, Vector3 normal) RunUpdate(CharacterClimbingCollisionTracker tracker, int count, Vector3 accrued)
        {
            Set(tracker, "m_collisionCount", count);
            Set(tracker, "m_accruedCollisionNormals", accrued);
            tracker.Update();
            Assert.That(Get<int>(tracker, "m_collisionCount"), Is.EqualTo(0), "the count is consumed");
            Assert.That(Get<Vector3>(tracker, "m_accruedCollisionNormals"), Is.EqualTo(Vector3.zero), "the accrued normals are consumed");
            return (tracker.IsColliding, tracker.CollisionNormal);
        }

        // Update 0x795660: contacts since the last update make the tracker colliding (setg, 0x795670); without any it asks the
        // ground check (here a ray that no layer can hit). Colliding trackers publish the accrued normals divided by their
        // length when that length exceeds 1e-5 (ucomiss 0x29a6df4, jbe 0x7956db) and a zero normal otherwise; trackers that
        // are not colliding publish zero whatever was accrued. The count and the accrued sum always reset (0x7956f8,
        // 0x79577c).
        [Test]
        public void UpdatePublishesTheAveragedNormalOfTheContactsAndResetsTheAccrual()
        {
            WithManager((owned, manager, character) =>
            {
                CharacterClimbingCollisionTracker tracker = NewTracker(owned, character, NoLayers);

                var contacts = RunUpdate(tracker, 2, new Vector3(0f, 0f, -2f));
                Assert.That(contacts.colliding, Is.True);
                Assert.That(contacts.normal.x, Is.EqualTo(0f).Within(1e-6f));
                Assert.That(contacts.normal.y, Is.EqualTo(0f).Within(1e-6f));
                Assert.That(contacts.normal.z, Is.EqualTo(-1f).Within(1e-6f), "the normal is the unit direction of the accrued sum");

                var slanted = RunUpdate(tracker, 1, new Vector3(3f, 0f, 4f));
                Assert.That(slanted.normal.x, Is.EqualTo(0.6f).Within(1e-6f));
                Assert.That(slanted.normal.z, Is.EqualTo(0.8f).Within(1e-6f));

                var cancelled = RunUpdate(tracker, 3, Vector3.zero);
                Assert.That(cancelled.colliding, Is.True, "contacts keep the tracker colliding");
                Assert.That(cancelled.normal, Is.EqualTo(Vector3.zero), "opposing contacts leave no direction");

                var tiny = RunUpdate(tracker, 1, new Vector3(5e-6f, 0f, 0f));
                Assert.That(tiny.normal, Is.EqualTo(Vector3.zero), "a sum no longer than 1e-5 leaves no direction");
                var small = RunUpdate(tracker, 1, new Vector3(2e-5f, 0f, 0f));
                Assert.That(small.normal.x, Is.EqualTo(1f).Within(1e-5f), "a sum longer than 1e-5 is normalised");

                SetPrivate(tracker, "CollisionNormal", new Vector3(7f, 7f, 7f));
                var none = RunUpdate(tracker, 0, new Vector3(0f, 0f, -2f));
                Assert.That(none.colliding, Is.False, "no contacts and nothing under the character");
                Assert.That(none.normal, Is.EqualTo(Vector3.zero), "the accrued sum is not published, and the old normal is replaced");
            });
        }

        // TerrainClimbable 0x795E10 is true only for a collision data whose game object is on a layer of the mask
        // (LayerMaskExtensions.IncludesLayer 0x795e35) and that is climbable (+0x5b, 0x795e3e).
        [Test]
        public void TerrainIsClimbableOnlyOnAMaskedLayerAndWhenMarkedClimbable()
        {
            WithManager((owned, manager, character) =>
            {
                GameObject host = owned.Host("Lucid climbable terrain", false);
                host.layer = 9;
                var data = host.AddComponent<CharacterCollisionData>();
                LayerMask included = 1 << 9, excluded = 1 << 10, both = (1 << 9) | (1 << 10);
                (LayerMask mask, bool climbable, bool expected, string label)[] table =
                {
                    (included, true, true, "masked and climbable"),
                    (both, true, true, "one of several layers"),
                    (included, false, false, "masked but not climbable"),
                    (excluded, true, false, "climbable on another layer"),
                    (NoLayers, true, false, "empty mask"),
                };
                foreach ((LayerMask mask, bool climbable, bool expected, string label) in table)
                {
                    SetPrivate(data, "IsClimbable", climbable);
                    Assert.That(CharacterClimbingCollisionTracker.TerrainClimbable(mask, data), Is.EqualTo(expected), label);
                }
            });
        }

        // The contact callbacks cannot be fed a Collision here, so their compiled bodies are witnessed. TryAddCollision 0x795BE0
        // asks TerrainClimbable (inlined from 0x795c22) and then subtracts every contact normal from the accrued sum (subps,
        // 0x795cb5) before adding the contact count (0x795cd4); the ground check adds the hit normal (addps, 0x795a4c)
        // for a hit on climbable terrain. OnCollisionDataEnter 0x795AD0 also records the character's world velocity
        // (0x795bc2); OnCollisionDataStay 0x795D00 does not.
        [Test]
        public void ContactCallbacksAccrueTheNegatedNormalsAndEnterRecordsTheVelocity()
        {
            Assert.That(CallNames(Method("TryAddCollision")), Is.EqualTo(new[]
            {
                "CharacterClimbingCollisionTracker.TerrainClimbable", "Collision.get_contactCount", "Collision.GetContact",
                "ContactPoint.get_normal", "Vector3.op_Subtraction",
            }));
            Assert.That(CallNames(Method("OnCollisionDataEnter")), Is.EqualTo(new[]
            {
                "CharacterClimbingCollisionTracker.TryAddCollision", "Actor.get_WorldVelocity",
                "CharacterClimbingCollisionTracker.set_WorldVelocityAtLastTerrainEnter",
            }));
            Assert.That(CallNames(Method("OnCollisionDataStay")), Is.EqualTo(new[] { "CharacterClimbingCollisionTracker.TryAddCollision" }));
            Assert.That(CallNames(Method("TerrainClimbable")), Is.EqualTo(new[]
            {
                "Component.get_gameObject", "LayerMaskExtensions.IncludesLayer", "CharacterCollisionData.get_IsClimbable",
            }));
        }

        // PerformClimbableGroundCheck 0x7957E0 casts a ray from the character's bounds centre along its negated up direction
        // (flattened to a unit vector or zero, 0x7958bd), as far as the half height plus the minimum ground check distance of
        // its collider settings (0x795923, 0x795937), against the tracker's layer mask (LayerMask.op_Implicit, 0x795946); a hit
        // collider with climbable terrain data (TrackManager.TryGetCharacterCollisionData, 0x7959e9) adds its normal and one
        // contact. Update tests the squared length against the single-precision constant 1e-5 (0x29a6df4).
        [Test]
        public void GroundCheckAndUpdateUseTheNativeCallsAndConstant()
        {
            Assert.That(CallNames(Method("PerformClimbableGroundCheck")), Is.EqualTo(new[]
            {
                "Character.get_Bounds", "Bounds.get_center", "Actor.get_UpDirection", "Vector3.op_UnaryNegation", "Vector3.get_normalized",
                "Ray..ctor", "Character.get_HalfHeight", "Character.get_Settings", "LayerMask.op_Implicit", "Physics.Raycast",
                "Character.get_TrackManager", "RaycastHit.get_collider", "TrackManager.TryGetCharacterCollisionData",
                "CharacterClimbingCollisionTracker.TerrainClimbable", "RaycastHit.get_normal", "Vector3.op_Addition",
            }));
            MethodInfo update = Method("Update");
            var instructions = OriginalILCallSequence.Instructions(update);
            float[] constants = instructions.Where(instruction => instruction.Code == OpCodes.Ldc_R4)
                .Select(instruction => OriginalILCallSequence.SingleOperand(update, instruction.Offset)).ToArray();
            Assert.That(constants, Is.EqualTo(new[] { BitConverter.Int32BitsToSingle(0x3727c5ac) }));
            Assert.That(constants[0], Is.EqualTo(1e-5f));
        }
    }
}
