using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using ProjectLucid.Tests.CharCoreShared;
using UnityEngine;
using static ProjectLucid.Tests.CharacterAbilityTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // CharacterAbility_TargetingHoming<T> (TypeDef 0x0200030c, shared x86-64 bodies 0xE68BA0..0xE690D0), exercised
    // through a concrete leaf closed over the rails homing definition probe. The character is a real component on an
    // owned inactive host with a real homing pool; the ability is built by its real constructor. The one branch that
    // needs a live camera manager (the heading override) is pinned by its call order instead.
    public sealed class CharacterTargetingHomingAbilityTests
    {
        // The generic base is abstract; this leaf adds only knobs for its two protected virtual members, which fall
        // back to the base implementation while unset.
        private sealed class HomingProbe : CharacterAbility_TargetingHoming<RailsHomingDefinitionProbe>
        {
            public bool? ProjectFromCameraKnob;
            public AnimationCurve CurveKnob;

            protected override bool ProjectFromCameraWhenNoDirectionInput => ProjectFromCameraKnob ?? base.ProjectFromCameraWhenNoDirectionInput;

            protected override AnimationCurve AdditionalSpeedCurve => CurveKnob ?? base.AdditionalSpeedCurve;
        }

        // The absent rows the constructors read are leased for the case: the character's island row, the App row of
        // CharacterAbility, the camera manager row of the homing ability and the message manager row of the pool.
        private static void WithHoming(Action<Owned, CharacterGravityVectorProbe, HomingProbe, RailsHomingDefinitionProbe> body,
            Dictionary<HomingTargetType, TargetingTypePriorityDefinition> priorities = null) => Owned.Run(owned =>
        {
            owned.LeaseRow<GameplayIslandManager>();
            owned.LeaseRow<App>();
            owned.LeaseRow<MessageManager>();
            owned.LeaseRow<CinemachineCameraManager>();
            CharacterGravityVectorProbe character = NewCharacter(owned);
            SetProperty(character, "HomingPool", new CharacterHomingPool(character, priorities ?? new Dictionary<HomingTargetType, TargetingTypePriorityDefinition>()));
            var definition = owned.Track(ScriptableObject.CreateInstance<RailsHomingDefinitionProbe>());
            var ability = new HomingProbe();
            Set(ability, "m_character", character);
            Set(ability, "m_definition", definition);
            body(owned, character, ability, definition);
        });

        private static CharacterTargetObjectData Target(GameObject target = null) => new CharacterTargetObjectData { Target = target };

        // ---- Properties ----

        // 0xE68BC0 and 0xE68BD0 return zero; 0xE68BE0 then needs the input test (false && ...), so an unset ability never
        // projects from the camera. The trigger starts false (0xE68BA0 reads the byte at +0x180).
        [Test]
        public void AFreshAbilityIsNotTriggeredAndNeverOverridesOrProjectsFromTheCamera()
        {
            WithHoming((owned, character, ability, definition) =>
            {
                Assert.That(ability.IsTriggered, Is.False);
                Assert.That(ability.OverridesTargeting, Is.False);
                Assert.That((bool)Call(ability, "get_ProjectFromCameraWhenNoDirectionInput"), Is.False);
                Assert.That(ability.ShouldProjectFromCamera(), Is.False);
                ability.IsTriggered = true;
                Assert.That(ability.IsTriggered, Is.True, "the trigger is a plain read and write");
            });
        }

        // 0xE68C7C..0xE68CAF with no camera manager (0xE68C33 IsValid false): the abstract projection flag and no
        // controller movement (Character.HasControllerMovement, true above 0.0001 of the raw magnitude) together.
        [Test]
        public void WithoutACameraManagerTheCameraProjectionNeedsTheFlagAndNoControllerMovement()
        {
            WithHoming((owned, character, ability, definition) =>
            {
                foreach ((bool flag, float magnitude, bool expected) in new[]
                {
                    (true, 0f, true), (true, 0.5f, false), (false, 0f, false), (false, 0.5f, false), (true, 0.0001f, true), (true, 0.00011f, false),
                })
                {
                    ability.ProjectFromCameraKnob = flag;
                    SetProperty(character, "RawControllerMovementMagnitude", magnitude);
                    Assert.That(ability.ShouldProjectFromCamera(), Is.EqualTo(expected), "flag " + flag + ", magnitude " + magnitude);
                }
            });
        }

        // 0xE68BE0 asks the camera manager reference whether it is valid, takes the manager, reads the heading override
        // setting and only then the abstract flag and the controller movement. The heading override branch needs a live
        // camera manager, so the order is read from the compiled IL.
        [Test]
        public void CallOrderTheCameraProjectionTestsTheHeadingOverrideBeforeTheInput()
        {
            List<MethodBase> calls = Calls(typeof(CharacterAbility_TargetingHoming<>), nameof(CharacterAbility_TargetingHoming<RailsHomingDefinitionProbe>.ShouldProjectFromCamera));
            int valid = IndexOf(calls, "SystemRef", "IsValid");
            int manager = IndexOf(calls, "SystemRef", "Get", valid + 1);
            int heading = IndexOf(calls, "CinemachineCameraManager", "GetCameraSettingOverride", manager + 1);
            int flag = IndexOf(calls, "CharacterAbility_TargetingHoming", "get_ProjectFromCameraWhenNoDirectionInput", heading + 1);
            IndexOf(calls, "Character", "get_HasControllerMovement", flag + 1);
        }

        // 0xE68CB0..0xE68E00: each getter picks the ability definition override when one is assigned (Object.op_Inequality
        // on the override at +0xB0, indexing +0xA8) and reads +0xCC, +0xD0 and +0xD4 of it.
        [Test]
        public void TargetingValuesComeFromTheDefinitionOrItsOverride()
        {
            WithHoming((owned, character, ability, definition) =>
            {
                definition.TargetingIntervalSeconds = 0.4f;
                definition.CooldownTimeSeconds = 0.6f;
                definition.UseTimeScaling = true;
                Assert.That(ability.TargetingDefinition, Is.SameAs(definition));
                Assert.That((float)Call(ability, "get_TargetingIntervalSeconds"), Is.EqualTo(0.4f));
                Assert.That((float)Call(ability, "get_CooldownTimeSeconds"), Is.EqualTo(0.6f));
                Assert.That((bool)Call(ability, "get_UseTimeScaling"), Is.True);

                var replacement = owned.Track(ScriptableObject.CreateInstance<RailsHomingDefinitionProbe>());
                replacement.TargetingIntervalSeconds = 1.5f;
                replacement.CooldownTimeSeconds = 2.5f;
                replacement.UseTimeScaling = false;
                Set(ability, "m_definitionOverride", replacement);
                Assert.That(ability.TargetingDefinition, Is.SameAs(replacement));
                Assert.That((float)Call(ability, "get_TargetingIntervalSeconds"), Is.EqualTo(1.5f));
                Assert.That((float)Call(ability, "get_CooldownTimeSeconds"), Is.EqualTo(2.5f));
                Assert.That((bool)Call(ability, "get_UseTimeScaling"), Is.False);
            });
        }

        // 0xE690C0 tail-calls Character.IsTargetingAvailable, the TargetingAvailable modifier (true by default).
        [Test]
        public void ActivationFollowsTheCharactersTargetingAvailability()
        {
            WithHoming((owned, character, ability, definition) =>
            {
                Assert.That(ability.CanActivate, Is.True);
                character.AddModifierOverride((int)GameplayModifierType.TargetingAvailable, false);
                Assert.That(ability.CanActivate, Is.False);
            });
        }

        // ---- Curve ----

        // 0xE69050: no curve, or fewer than two keys (cmp eax, 2 / jge), gives zero; otherwise the curve at the time.
        [Test]
        public void AdditionalSpeedNeedsACurveOfAtLeastTwoKeys()
        {
            WithHoming((owned, character, ability, definition) =>
            {
                Assert.That(ability.GetAdditionalSpeed(0.5f), Is.EqualTo(0f), "no curve");
                ability.CurveKnob = new AnimationCurve(new Keyframe(0f, 7f));
                Assert.That(ability.GetAdditionalSpeed(0.5f), Is.EqualTo(0f), "one key");
                ability.CurveKnob = AnimationCurve.Linear(0f, 0f, 1f, 10f);
                Assert.That(ability.GetAdditionalSpeed(0.5f), Is.EqualTo(5f).Within(1e-4f), "two keys");
                Assert.That(ability.GetAdditionalSpeed(2f), Is.EqualTo(10f).Within(1e-4f), "clamped after the last key");
            });
        }

        // ---- Entering and leaving ----

        // 0xE68F20..0xE68F97: after the targeting base entry, a raised trigger is re-derived from whether the pool's
        // active target belongs to this ability; a trigger that is down is never raised (cmp byte [+0x180], 0).
        [Test]
        public void EnteringKeepsTheTriggerOnlyWhileTheAbilityOwnsTheActiveTarget()
        {
            WithHoming((owned, character, ability, definition) =>
            {
                ability.IsTriggered = true;
                ability.Enter();
                Assert.That(ability.IsTriggered, Is.False, "no active target");

                character.HomingPool.SetTargetImmediately(ability, Target(), Vector3.zero, 5f);
                ability.IsTriggered = true;
                ability.Enter();
                Assert.That(ability.IsTriggered, Is.True, "the active target is this ability's");

                ability.IsTriggered = false;
                ability.Enter();
                Assert.That(ability.IsTriggered, Is.False, "a lowered trigger stays down");
            });
        }

        [Test]
        public void EnteringAnotherAbilitysActiveTargetDropsTheTrigger()
        {
            WithHoming((owned, character, ability, definition) =>
            {
                var other = new HomingProbe();
                Set(other, "m_character", character);
                Set(other, "m_definition", definition);
                character.HomingPool.SetTargetImmediately(other, Target(), Vector3.zero, 5f);
                ability.IsTriggered = true;
                ability.Enter();
                Assert.That(ability.IsTriggered, Is.False);
            });
        }

        // 0xE68FA0: the targeting base records the cooldown end (clock + CooldownTimeSeconds, +0x168) and the ability
        // is taken off every pooled target (CharacterHomingPool.RemoveTarget).
        [Test]
        public void LeavingStartsTheCooldownAndRemovesTheAbilityFromThePool()
        {
            var priority = ScriptableObject.CreateInstance<TargetingTypePriorityDefinition>();
            Set(priority, "m_targetType", HomingTargetType.Enemy);
            Set(priority, "m_priority", 1);
            try
            {
                WithHoming((owned, character, ability, definition) =>
                {
                    definition.CooldownTimeSeconds = 0.5f;
                    GameObject target = owned.Host("Lucid pooled target");
                    character.HomingPool.AddPotentialTarget(target, Vector3.zero, Quaternion.identity, HomingTargetType.Enemy, ability);
                    Assert.That(PooledTargetCount(character.HomingPool), Is.EqualTo(1), "the target was pooled");
                    ability.Enter();
                    Set(ability, "m_currentTime", 2f);
                    ability.Leave();
                    Assert.That(Get<float>(ability, "m_lastUpdateTime"), Is.EqualTo(2.5f));
                    Assert.That(PooledTargetCount(character.HomingPool), Is.EqualTo(0), "the ability left the pooled target");
                }, new Dictionary<HomingTargetType, TargetingTypePriorityDefinition> { { HomingTargetType.Enemy, priority } });
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(priority);
            }
        }

        private static int PooledTargetCount(CharacterHomingPool pool)
        {
            var groups = (IDictionary)Get<object>(pool, "m_targetTypeToGroupMap");
            return groups.Values.Cast<object>().Sum(group =>
                ((List<CharacterHomingPool.PooledTarget>)group.GetType().GetProperty("PooledTargets").GetValue(group)).Count);
        }

        // 0xE69000: only a pending target that is valid for this ability (CharacterHomingPool.PendingTargetIsValidForAbility,
        // which needs CanActivate, a pending target and this ability on it) is cleared.
        [Test]
        public void ResetClearsOnlyAPendingTargetThatIsValidForTheAbility()
        {
            WithHoming((owned, character, ability, definition) =>
            {
                GameObject target = owned.Host("Lucid pending target");
                var pool = character.HomingPool;
                var other = new HomingProbe();
                Set(pool, "m_pendingTarget", new CharacterHomingPool.PooledTarget { Abilities = new List<IHomingAbility> { other }, Object = Target(target) });
                ability.Reset();
                Assert.That(pool.PendingTarget.Object.Target, Is.SameAs(target), "another ability's pending target");

                Set(pool, "m_pendingTarget", new CharacterHomingPool.PooledTarget { Abilities = new List<IHomingAbility> { ability }, Object = Target(target) });
                character.AddModifierOverride((int)GameplayModifierType.TargetingAvailable, false);
                ability.Reset();
                Assert.That(pool.PendingTarget.Object.Target, Is.SameAs(target), "the ability cannot activate");
            });
        }

        [Test]
        public void ResetClearsThePendingTargetOfAnAbilityThatCanActivate()
        {
            WithHoming((owned, character, ability, definition) =>
            {
                GameObject target = owned.Host("Lucid pending target");
                Set(character.HomingPool, "m_pendingTarget", new CharacterHomingPool.PooledTarget { Abilities = new List<IHomingAbility> { ability }, Object = Target(target) });
                ability.Reset();
                Assert.That(character.HomingPool.PendingTarget.Object.Target == null, Is.True);
            });
        }

        // ---- Immediate target ----

        // 0xE68E80: Character.IsActive first (0xE68EAA, it needs a live body), then no active target already
        // (CharacterHomingPool.IsTargetActive, 0xE68EC3) and finally SetTargetImmediately with the ability itself
        // (0xE68F0C); those are the only native calls. The HomingPool getter, read once for each pool call, is
        // inlined there and stays a call in IL.
        [Test]
        public void CallOrderTheImmediateTargetNeedsAnActiveCharacterAndNoActiveTarget()
        {
            List<MethodBase> calls = Calls(typeof(CharacterAbility_TargetingHoming<>), "TrySetTargetImmediately");
            int active = IndexOf(calls, "Character", "IsActive");
            int targetActive = IndexOf(calls, "CharacterHomingPool", "IsTargetActive", active + 1);
            IndexOf(calls, "CharacterHomingPool", "SetTargetImmediately", targetActive + 1);
            Assert.That(calls.Count(call => call.Name == "get_HomingPool"), Is.EqualTo(2));
            Assert.That(calls.Count(call => call.Name != "get_HomingPool"), Is.EqualTo(3));
        }
    }
}
