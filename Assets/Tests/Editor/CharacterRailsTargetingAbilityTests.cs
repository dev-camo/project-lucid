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
using static ProjectLucid.Tests.CharacterAbilityTestSupport;
using static ProjectLucid.Tests.CharacterTriggerTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // CharacterAbility_RailsTargeting (TypeDef 0x02000309, x86-64 bodies 0x750780..0x751FD0): the lock of the pending rail
    // target on the rail target action, the target projection gates, the reset, the surface filter and the debug text.
    // The ability is built by its real constructor on a real Character on an owned inactive host with a real homing pool.
    // Not covered by any test, because they need a track manager with an octree or a started character: Initialise (it
    // reads the data manager's rail definitions), the cone projection and the potential target hand over of
    // ProjectToTargetedObject past its early exits (Character.IsActive needs a rigid body, then TryGetProjectedRailHit,
    // 0x75147D..0x75176F), and the bounds calculation of OnDrawGizmosSelected, whose drawing calls are absent from the binary.
    public sealed class CharacterRailsTargetingAbilityTests
    {
        private static void WithTargeting(Action<Owned, CharacterGravityVectorProbe, CharacterAbility_RailsTargeting, CharacterAbilityDefinition_RailsTargeting> body) => Owned.Run(owned =>
        {
            owned.LeaseRow<GameplayIslandManager>();
            owned.LeaseRow<App>();
            owned.LeaseRow<MessageManager>();
            owned.LeaseRow<CinemachineCameraManager>();
            CharacterActionFlags.Initialise();
            CharacterGravityVectorProbe character = NewCharacter(owned);
            var priority = owned.Track(ScriptableObject.CreateInstance<TargetingTypePriorityDefinition>());
            Set(priority, "m_targetType", HomingTargetType.Rail);
            Set(priority, "m_priority", 1);
            SetProperty(character, "HomingPool", new CharacterHomingPool(character, new Dictionary<HomingTargetType, TargetingTypePriorityDefinition> { [HomingTargetType.Rail] = priority }));
            Set(character, "m_trackManagerRef", new SystemRef<TrackManager>("TrackManager", null));
            var definition = owned.Track(ScriptableObject.CreateInstance<CharacterAbilityDefinition_RailsTargeting>());
            CharacterAbility_RailsTargeting ability = Build(character, definition);
            body(owned, character, ability, definition);
        });

        private static CharacterAbility_RailsTargeting Build(Character character, CharacterAbilityDefinition_RailsTargeting definition)
        {
            var ability = new CharacterAbility_RailsTargeting();
            Set(ability, "m_character", character);
            Set(ability, "m_definition", definition);
            return ability;
        }

        private static RailsBrain Brain(Character character, params GameAction[] actions)
        {
            var flags = new CharacterActionFlags();
            foreach (GameAction action in actions) flags.SetValue(action, true);
            var brain = new RailsBrain();
            brain.ApplyActions(flags, 1f);
            Set(character, "m_brain", brain);
            return brain;
        }

        private static void SetPending(CharacterHomingPool pool, IHomingAbility ability, GameObject target) =>
            Set(pool, "m_pendingTarget", new CharacterHomingPool.PooledTarget
            {
                Abilities = new List<IHomingAbility> { ability },
                Object = new CharacterTargetObjectData { Target = target, WorldRotation = Quaternion.identity, Type = HomingTargetType.Rail },
            });

        private static void Activate(CharacterHomingPool pool, IHomingAbility ability, GameObject target, HomingTargetType type = HomingTargetType.Rail) =>
            pool.SetTargetImmediately(ability, new CharacterTargetObjectData { Target = target, WorldRotation = Quaternion.identity, Type = type }, Vector3.zero, 5f);

        private static List<CharacterHomingPool.PooledTarget> Pooled(CharacterHomingPool pool)
        {
            var groups = (IDictionary)Get<object>(pool, "m_targetTypeToGroupMap");
            return groups.Values.Cast<object>().Distinct()
                .SelectMany(group => (List<CharacterHomingPool.PooledTarget>)group.GetType().GetProperty("PooledTargets").GetValue(group)).ToList();
        }

        private static void Update(object ability, CharacterBrain brain) => Call(ability, "DoOnUpdate", brain, 0.02f);

        // ---- Properties ----

        // 0x750780 and 0x7507D0 read the camera projection flag (+0x11C) and the additional speed curve (+0x100) of the definition.
        [Test]
        public void TheCameraProjectionFlagAndSpeedCurveComeFromTheDefinition()
        {
            WithTargeting((owned, character, ability, definition) =>
            {
                Assert.That((bool)Call(ability, "get_ProjectFromCameraWhenNoDirectionInput"), Is.False);
                definition.ProjectFromCameraWhenNoDirectionInput = true;
                Assert.That((bool)Call(ability, "get_ProjectFromCameraWhenNoDirectionInput"), Is.True);
                definition.HomingAdditionalSpeed = AnimationCurve.Linear(0f, 2f, 2f, 6f);
                Assert.That(Call(ability, "get_AdditionalSpeedCurve"), Is.SameAs(definition.HomingAdditionalSpeed));
                Assert.That(ability.GetAdditionalSpeed(1f), Is.EqualTo(4f).Within(1e-5f));
            });
        }

        // ---- Update ----

        // 0x750AB1..0x750B8A: a valid pending target and a queued rail target action lock the target at the definition's homing
        // velocity (+0x118) and rotational velocity (+0xF8) with the character's velocity as the restore velocity, trigger the
        // ability and clear the refresh request.
        [Test]
        public void TheRailTargetActionLocksThePendingTargetAtTheDefinitionVelocities()
        {
            WithTargeting((owned, character, ability, definition) =>
            {
                SuppressTargetingRefresh(ability);
                definition.HomingVelocity = 33f;
                definition.HomingRotationalVelocity = 77f;
                character.SetWorldVelocity(new Vector3(1f, 2f, 3f));
                SetPending(character.HomingPool, ability, owned.Host("Lucid rail target"));
                RailsBrain brain = Brain(character, GameAction.CharacterRailTarget);
                Set(ability, "m_targetRequiresRefresh", true);
                Update(ability, brain);
                ActiveHomingTarget active = character.HomingPool.ActiveTarget;
                Assert.That(active, Is.Not.Null);
                Assert.That(active.Speed, Is.EqualTo(33f));
                Assert.That(active.RotationalSpeed, Is.EqualTo(77f));
                Assert.That(active.RestoreVelocity, Is.EqualTo(new Vector3(1f, 2f, 3f)));
                Assert.That(ability.IsTriggered, Is.True);
                Assert.That(RefreshRequested(ability), Is.False);
                Assert.That(brain.ActionStateQueued.GetValue(GameAction.CharacterRailTarget), Is.False, "the press is consumed");
            });
        }

        // 0x750B03: the lock reads the applied state (get_RailTargeting, m_actionStateApplied at CharacterBrain+0x18), not the
        // queued one (+0x14). An action ended after being applied is still locked on; one only queued is not.
        [Test]
        public void TheLockReadsTheAppliedRailTargetStateNotTheQueuedOne()
        {
            foreach ((bool appliedOnly, bool locks) in new[] { (true, true), (false, false) })
            {
                WithTargeting((owned, character, ability, definition) =>
                {
                    SuppressTargetingRefresh(ability);
                    SetPending(character.HomingPool, ability, owned.Host("Lucid rail target"));
                    RailsBrain brain;
                    if (appliedOnly)
                    {
                        brain = Brain(character, GameAction.CharacterRailTarget);
                        brain.EndAction(GameAction.CharacterRailTarget);
                    }
                    else
                    {
                        brain = Brain(character);
                        brain.Press(GameAction.CharacterRailTarget);
                    }
                    Assert.That(brain.ActionStateQueued.GetValue(GameAction.CharacterRailTarget), Is.EqualTo(!appliedOnly), "precondition: queued");
                    Assert.That(brain.RailTargeting, Is.EqualTo(appliedOnly), "precondition: applied");
                    Update(ability, brain);
                    Assert.That(character.HomingPool.ActiveTarget != null, Is.EqualTo(locks), "applied only " + appliedOnly);
                    Assert.That(ability.IsTriggered, Is.EqualTo(locks), "applied only " + appliedOnly);
                });
            }
        }

        // The lock needs both the pending target and the queued action; the action is consumed (SetState false, 0x750B99) whenever
        // the update gets that far.
        [Test]
        public void TheLockNeedsBothAValidPendingTargetAndTheQueuedAction()
        {
            WithTargeting((owned, character, ability, definition) =>
            {
                SuppressTargetingRefresh(ability);
                SetPending(character.HomingPool, ability, owned.Host("Lucid rail target"));
                RailsBrain idle = Brain(character);
                Update(ability, idle);
                Assert.That(character.HomingPool.ActiveTarget, Is.Null, "no action");
                Assert.That(ability.IsTriggered, Is.False);
            });
            WithTargeting((owned, character, ability, definition) =>
            {
                SuppressTargetingRefresh(ability);
                RailsBrain pressed = Brain(character, GameAction.CharacterRailTarget);
                Update(ability, pressed);
                Assert.That(character.HomingPool.ActiveTarget, Is.Null, "no pending target");
                Assert.That(ability.IsTriggered, Is.False);
                Assert.That(pressed.ActionStateQueued.GetValue(GameAction.CharacterRailTarget), Is.False, "consumed all the same");
            });
        }

        // 0x7509C1..0x7509F9: an ability that already owns the active target leaves before touching its trigger or the action.
        [Test]
        public void AnAbilityThatOwnsTheActiveTargetLeavesAtOnce()
        {
            WithTargeting((owned, character, ability, definition) =>
            {
                SuppressTargetingRefresh(ability);
                GameObject target = owned.Host("Lucid rail target");
                Activate(character.HomingPool, ability, target);
                ability.IsTriggered = true;
                RailsBrain brain = Brain(character, GameAction.CharacterRailTarget);
                Update(ability, brain);
                Assert.That(ability.IsTriggered, Is.True, "the trigger is not cleared");
                Assert.That(brain.ActionStateQueued.GetValue(GameAction.CharacterRailTarget), Is.True, "the action is not consumed");
                Assert.That(character.HomingPool.IsTargetActive(ability), Is.True);
            });
        }

        // 0x7509FF: with no active target of its own the trigger is cleared first.
        [Test]
        public void AnIdleAbilityClearsItsTrigger()
        {
            WithTargeting((owned, character, ability, definition) =>
            {
                SuppressTargetingRefresh(ability);
                ability.IsTriggered = true;
                Update(ability, Brain(character));
                Assert.That(ability.IsTriggered, Is.False);
            });
        }

        // 0x750A4F..0x750AAB: a jump on the rail tracker (GetValueOnly<bool>, any true) or a rail switch time above zero (ja, so
        // zero and negative times do not block) end the update before the lock and before the action is consumed.
        [Test]
        public void TheRailTrackerJumpAndThePositiveRailSwitchTimeBlockTheLock()
        {
            foreach ((bool jump, float switchTime, bool locks) in new[]
            {
                (false, 0f, true), (true, 0f, false), (false, 0.5f, false), (false, -1f, true),
            })
            {
                WithTargeting((owned, character, ability, definition) =>
                {
                    SuppressTargetingRefresh(ability);
                    character.Storage.SetValue(ActorFSMKeys.JumpOnRailTracker, jump);
                    character.Storage.SetValue(ActorFSMKeys.RailSwitchTimeElapsed, switchTime);
                    SetPending(character.HomingPool, ability, owned.Host("Lucid rail target"));
                    RailsBrain brain = Brain(character, GameAction.CharacterRailTarget);
                    Update(ability, brain);
                    string row = "jump " + jump + ", switch time " + switchTime;
                    Assert.That(character.HomingPool.ActiveTarget != null, Is.EqualTo(locks), row);
                    Assert.That(brain.ActionStateQueued.GetValue(GameAction.CharacterRailTarget), Is.EqualTo(!locks), row);
                });
            }
        }

        // 0x750BA0..0x750BEB: a refresh request removes the ability from the pooled targets and, when the pending target was
        // valid for it, clears the active target (which may belong to another ability) before projecting again. Without a
        // track manager the projection ends at once, so the request is spent.
        [Test]
        public void ARefreshRequestRemovesTheAbilityFromThePooledTargetsAndSpendsTheRequest()
        {
            WithTargeting((owned, character, ability, definition) =>
            {
                character.HomingPool.AddPotentialTarget(owned.Host("Lucid pooled rail"), Vector3.zero, Quaternion.identity, HomingTargetType.Rail, ability);
                Assert.That(Pooled(character.HomingPool), Has.Count.EqualTo(1));
                Update(ability, Brain(character));
                Assert.That(RefreshRequested(ability), Is.False, "the request is spent");
                Assert.That(Pooled(character.HomingPool), Is.Empty);
            });
        }

        [Test]
        public void ARefreshRequestClearsAnotherAbilitiesActiveTargetOnlyForAValidPendingTarget()
        {
            foreach (bool pendingIsValid in new[] { true, false })
            {
                WithTargeting((owned, character, ability, definition) =>
                {
                    CharacterAbility_RailsTargeting other = Build(character, definition);
                    Activate(character.HomingPool, other, owned.Host("Lucid active rail"));
                    if (pendingIsValid) SetPending(character.HomingPool, ability, owned.Host("Lucid pending rail"));
                    Update(ability, Brain(character));
                    Assert.That(character.HomingPool.ActiveTarget == null, Is.EqualTo(pendingIsValid), "pending valid " + pendingIsValid);
                });
            }
        }

        // ---- Reset ----

        // 0x751C35..0x751D0B: a rail as the active target marks the rail target cancelled; any other target or none leaves the
        // flag alone. The base reset (0xE69000) then clears the active target when the pending target is valid for the ability.
        [Test]
        public void ResettingWhileARailIsTargetedMarksTheRailTargetCancelled()
        {
            foreach ((HomingTargetType? type, bool cancelled) in new (HomingTargetType?, bool)[]
            {
                (HomingTargetType.Rail, true), (HomingTargetType.Enemy, false), (null, false),
            })
            {
                WithTargeting((owned, character, ability, definition) =>
                {
                    if (type.HasValue) Activate(character.HomingPool, ability, owned.Host("Lucid rail target"), type.Value);
                    ability.Reset();
                    Assert.That(character.Storage.TryGetValue(ActorFSMKeys.RailTargetCancelled, out bool flag) && flag, Is.EqualTo(cancelled), "type " + type);
                });
            }
        }

        // 0x751C35..0x751C54 reads the pool's active target without asking who owns it.
        [Test]
        public void ResettingMarksTheRailTargetCancelledWhoeverOwnsTheActiveRail()
        {
            WithTargeting((owned, character, ability, definition) =>
            {
                CharacterAbility_RailsTargeting other = Build(character, definition);
                Activate(character.HomingPool, other, owned.Host("Lucid rail target"));
                ability.Reset();
                Assert.That(character.Storage.TryGetValue(ActorFSMKeys.RailTargetCancelled, out bool flag) && flag, Is.True);
            });
        }

        [Test]
        public void ResettingAlsoRunsTheBaseResetAfterTheCancelMark()
        {
            foreach (bool pendingIsValid in new[] { true, false })
            {
                WithTargeting((owned, character, ability, definition) =>
                {
                    Activate(character.HomingPool, ability, owned.Host("Lucid rail target"));
                    if (pendingIsValid) SetPending(character.HomingPool, ability, owned.Host("Lucid pending rail"));
                    ability.Reset();
                    Assert.That(character.Storage.TryGetValue(ActorFSMKeys.RailTargetCancelled, out bool flag) && flag, Is.True, "marked before the base reset clears the target");
                    Assert.That(character.HomingPool.ActiveTarget == null, Is.EqualTo(pendingIsValid), "pending valid " + pendingIsValid);
                });
            }
        }

        // ---- Surface filter ----

        // 0x751AF0: the surface's metadata group for the rail definition's group key is asked for the targetable key; a missing
        // entry keeps the surface targetable, a stored value decides (parsed with bool.TryParse).
        [Test]
        public void ASurfaceIsTargetableUnlessItsMetadataSaysOtherwise()
        {
            WithTargeting((owned, character, ability, definition) =>
            {
                var groupKey = owned.Track(ScriptableObject.CreateInstance<MetadataGroupKey>());
                Set(groupKey, "m_guid", "rails-group");
                var targetableKey = owned.Track(ScriptableObject.CreateInstance<MetadataKeyType>());
                Set(targetableKey, "m_guid", "rails-targetable");
                var otherKey = owned.Track(ScriptableObject.CreateInstance<MetadataKeyType>());
                Set(otherKey, "m_guid", "rails-other");
                var group = owned.Track(ScriptableObject.CreateInstance<MetadataFlexiGroup>());
                List<MetadataFlexi> rows = Get<List<MetadataFlexi>>(Get<MetadataFlexiGroupData>(group, "m_data"), "m_metadata");
                var groups = new MetadataGroups();
                Get<List<MetadataKeyGroupPair>>(groups, "m_groups").Add(new MetadataKeyGroupPair { Key = groupKey, Group = group });
                InertSpline surface = owned.Host("Lucid metadata rail", false).AddComponent<InertSpline>();
                Set(surface, "m_metadata", groups);
                var rail = owned.Track(ScriptableObject.CreateInstance<RailDefinition>());
                Set(rail, "m_metadataGroupKey", groupKey);
                Set(rail, "m_metadataTargetableKey", targetableKey);
                Set(ability, "m_railDefinition", rail);

                rows.Add(new MetadataFlexi(otherKey, "false"));
                Assert.That((bool)Call(ability, "IsSurfaceTargetable", surface), Is.True, "no entry for the targetable key");
                rows.Add(new MetadataFlexi(targetableKey, "false"));
                Assert.That((bool)Call(ability, "IsSurfaceTargetable", surface), Is.False, "stored false");
                rows.Clear();
                rows.Add(new MetadataFlexi(targetableKey, "true"));
                Assert.That((bool)Call(ability, "IsSurfaceTargetable", surface), Is.True, "stored true");
            });
        }

        // ---- Debug text ----

        // 0x751D30: after the base text the project distance (+0xFC, default formatting) and the cone angles (+0x10C, F1).
        [Test]
        public void TheDebugTextEndsWithTheProjectDistanceAndConeAngles()
        {
            WithInvariantCulture(() => WithTargeting((owned, character, ability, definition) =>
            {
                var text = new StringBuilder();
                Call(ability, "GetDebugExtraInfo", text);
                Assert.That(text.ToString(), Does.EndWith("ProjectDistance = 1 ProjectConeAngles = (30.0, 30.0) "));
                definition.HomingDistance = 2.5f;
                definition.HomingProjectConeAngle = new Vector2(10f, 20.5f);
                text.Clear();
                Call(ability, "GetDebugExtraInfo", text);
                Assert.That(text.ToString(), Does.EndWith("ProjectDistance = 2.5 ProjectConeAngles = (10.0, 20.5) "));
            }));
        }

        // ---- Declarations ----

        // The declaration rows of the original DummyDll tables; this is declaration shape, not behaviour.
        [Test]
        public void TheAbilityKeepsTheOriginalDeclarationShape()
        {
            AssertMembers(typeof(CharacterAbility_RailsTargeting), 0x00100001,
                new[] { ("m_cone", 0x0001), ("m_railDefinition", 0x0001) },
                new[]
                {
                    ("get_ProjectFromCameraWhenNoDirectionInput", 0x08c4), ("get_AdditionalSpeedCurve", 0x08c4), ("Initialise", 0x00c6), ("DoOnUpdate", 0x00c4),
                    ("ProjectToTargetedObject", 0x0081), ("IsSurfaceTargetable", 0x0081), ("Reset", 0x00c6), ("GetDebugExtraInfo", 0x00c4),
                    ("OnDrawGizmosSelected", 0x00c6), (".ctor", 0x1886),
                });
            Assert.That(typeof(CharacterAbility_RailsTargeting).BaseType, Is.EqualTo(typeof(CharacterAbility_TargetingHoming<CharacterAbilityDefinition_RailsTargeting>)));
            Assert.That(typeof(CharacterAbility_RailsTargeting).GetInterfaces().Select(i => i.Name), Does.Contain("IAbilityTrigger"));
            Assert.That(AttributeNames(typeof(CharacterAbility_RailsTargeting)), Is.EqualTo(new[] { "Il2CppSetOption:2", "Il2CppSetOption:1" }));
            Assert.That(typeof(CharacterAbility_RailsTargeting).GetField("m_cone", BindingFlags.NonPublic | BindingFlags.Instance).FieldType, Is.EqualTo(typeof(ConeVolume.ConeDescription)));
            Assert.That(typeof(CharacterAbility_RailsTargeting).GetField("m_railDefinition", BindingFlags.NonPublic | BindingFlags.Instance).FieldType, Is.EqualTo(typeof(RailDefinition)));
        }
    }
}
