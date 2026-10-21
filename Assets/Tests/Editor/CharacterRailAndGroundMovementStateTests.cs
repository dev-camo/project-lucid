using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
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
    // The rail movement, rail switch, ground boost, ground braking, rolling, spin dash charge, hub preparation and orb
    // collect character states. Expected values come from the 1.10.1 x86-64 bodies cited per test (the shared generic
    // bodies at 0xE6F680..0xE6FA00 and 0xE76220..0xE78910). The enter, update and leave bodies reach the camera, form,
    // stamina, tracker and animation graphs of a started Character, so they are checked as call sequences read from the
    // compiled IL; the helpers that can run on their own (the queued action hand over, the remain timer, the animation
    // choice) run against a real Character on an owned inactive host. The *InTheNativeOrder-style witnesses are
    // emission-only call-sequence checks of the reconstruction's own IL (policy: separate from behaviour oracles).
    // Not covered by any test, because they need a started Character (tracker, camera, form, stamina graphs): the
    // RailSwitch state enter and update, the SpinDashCharge update, the GroundBraking enter, and the comparison
    // predicates inside them (rolling auto-exit and ledge braking thresholds, the rail orientation 0.0001 limit).
    // The enter, update and leave witnesses below check only which calls are made and in which order, not the branch
    // polarity of these predicates: the GroundBoostBase enter speed gate (0xE6F898, ucomiss/jbe, so NaN turns), the
    // boost leave gate IsTriggered && SuspendTime > 0 (0xE6F9A9..0xE6F9D0, 0xE76350), the RailMovement enter lock time
    // (0xE767F6, jbe) and attach speed floor (0xE76972, maxss), the RailMovement update on-rail conjunction
    // (0xE76FE4..0xE77344), the RailMovement leave snap (0xE778B6, snap == distanceFromEnd means no snap) and the
    // RailSwitch state update (0x77EFC3, elapsed == duration resumes).
    public sealed class CharacterRailAndGroundMovementStateTests
    {
        private static readonly Type StandingType = typeof(CharacterState_RailMovement<CharacterAbility_MovementRailStanding, CharacterAbilityDefinition_MovementRailStanding>);

        private static string Name() => "Lucid-char-moves-06-" + Guid.NewGuid().ToString("N");

        private static IFSMState NewState(string node, string json) =>
            FSMClassFactory.ConstructState(new FiniteStateMachine(Name(), skipAddToManager: true), node, new FSMIdentifier(Name()), json);

        private static string AbilityJson(ActorAbilityType type) => "{\"AbilityType\":" + (int)type + "}";

        private static object InvokeStatic(Type owner, string name, params object[] arguments)
        {
            MethodInfo method = owner.GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            try
            {
                return method.Invoke(null, arguments);
            }
            catch (TargetInvocationException wrapped) when (wrapped.InnerException != null)
            {
                ExceptionDispatchInfo.Capture(wrapped.InnerException).Throw();
                throw;
            }
        }

        // ---- Construction from JSON and declarations ----

        // The leaf states take the plain ability arguments; the orb collect state its own nested arguments.
        // 0x776380, 0x7764B0, 0x7765E0, 0x776790, 0x776C70, 0x77DB20, 0x77DFE0, 0x77E110, 0x77E240 and 0x77E370 pass the
        // arguments on to their bases, and the factory builds each through its ConstructInstance.
        [Test]
        public void LeafStatesReadTheirAbilityTypeFromJson()
        {
            // The ability types are deliberately unrelated to the states, to show the value comes from the JSON and is
            // not fixed by the state (m_abilityType is a property of ActorState_Ability despite its field-style name).
            foreach ((Type type, ActorAbilityType ability) in new[]
            {
                (typeof(CharacterState_GroundBoost), ActorAbilityType.Character_Jump),
                (typeof(CharacterState_GroundBoostOneShot), ActorAbilityType.Character_MovementAir),
                (typeof(CharacterState_GroundBraking), ActorAbilityType.Character_LedgeMount),
                (typeof(CharacterState_GroundRollingMovement), ActorAbilityType.Character_Jump),
                (typeof(CharacterState_GroundSpinDashCharge), ActorAbilityType.Character_MovementAir),
                (typeof(CharacterState_PrepareHub), ActorAbilityType.Character_LedgeMount),
                (typeof(CharacterState_RailMovementBoost), ActorAbilityType.Character_MovementRailBoost),
                (typeof(CharacterState_RailMovementBoostOneShot), ActorAbilityType.Character_MovementAir),
                (typeof(CharacterState_RailMovementStanding), ActorAbilityType.Character_RailSwitch),
                (typeof(CharacterState_RailSwitch), ActorAbilityType.Character_RailSwitch),
            })
            {
                IFSMState state = NewState(type.Name, AbilityJson(ability));
                Assert.That(state.GetType(), Is.EqualTo(type));
                Assert.That(typeof(ActorState_Ability).GetProperty("m_abilityType", Instance).GetValue(state), Is.EqualTo(ability), type.Name);
            }
        }

        // 0x786260: the base reads the animation name; the ghosted animation is looked up only when its own name is not
        // null or empty (String.IsNullOrEmpty at 0x7862DD), so an empty name leaves it unset without any lookup.
        [Test]
        public void OrbCollectWithoutAGhostedAnimationNameLeavesItUnset()
        {
            foreach (string json in new[] { "{}", "{\"GhostedOrbCollectionAnimationDefinition\":\"\"}" })
            {
                IFSMState state = NewState(nameof(CharacterState_OrbCollect), json);
                Assert.That(state.GetType(), Is.EqualTo(typeof(CharacterState_OrbCollect)), json);
                Assert.That(Get<ActorAnimationDefinition>(state, "m_ghostedOrbAnimationDefinition"), Is.Null, json);
                Assert.That(Get<ActorAnimationDefinition>(state, "m_stateAnimationDefinition"), Is.Null, json);
            }
            Type args = typeof(CharacterState_OrbCollect).GetNestedType("OrbCollectJSONCtorArgs", BindingFlags.NonPublic);
            Assert.That(args.IsNestedFamily, Is.True);
            Assert.That(args.IsSerializable, Is.True);
            Assert.That(args.GetField("GhostedOrbCollectionAnimationDefinition").FieldType, Is.EqualTo(typeof(string)));
            Assert.That(args.GetField("GhostedOrbCollectionAnimationDefinition").GetCustomAttributes(typeof(GraphUnityObjectAttribute), false), Has.Length.EqualTo(1));
        }

        // Node names and menu formats of the concrete states, read from the metadata of the original types.
        [Test]
        public void StatesKeepTheirGraphNodeNames()
        {
            foreach ((Type type, string name) in new[]
            {
                (typeof(CharacterState_GroundBoost), "GroundBoost"), (typeof(CharacterState_GroundBoostOneShot), "GroundBoostOneShot"),
                (typeof(CharacterState_GroundBraking), "GroundBraking"), (typeof(CharacterState_GroundRollingMovement), "GroundRollingMovement"),
                (typeof(CharacterState_GroundSpinDashCharge), "GroundSpinDashCharge"), (typeof(CharacterState_PrepareHub), "PrepareHub"),
                (typeof(CharacterState_RailMovementBoost), "RailMovementBoost"), (typeof(CharacterState_RailMovementBoostOneShot), "RailMovementBoostOneShot"),
                (typeof(CharacterState_RailMovementStanding), "RailMovementStanding"), (typeof(CharacterState_RailSwitch), "RailSwitch"),
                (typeof(CharacterState_OrbCollect), "OrbCollect"), (typeof(CharacterState_RailMovement<,>), "RailMovement"),
            })
            {
                Assert.That(type.GetCustomAttributes(typeof(GraphNodeDefaultNameAttribute), false).Cast<GraphNodeDefaultNameAttribute>().Single().DefaultName, Is.EqualTo(name), type.Name);
                Assert.That(type.GetCustomAttributes(typeof(GraphNodeMenuFormatAttribute), false).Cast<GraphNodeMenuFormatAttribute>().Single().Format, Is.EqualTo("Character/{0}"), type.Name);
            }
            foreach (Type type in new[] { typeof(CharacterState_GroundBoostBase<,>), typeof(CharacterState_RailMovementBoostBase<,>) })
            {
                Assert.That(type.GetCustomAttributes(typeof(GraphNodeDefaultNameAttribute), false), Is.Empty, type.Name);
            }
        }

        // The two abstract generic bases keep the parameter names and constraints of the metadata.
        [Test]
        public void GenericStatesKeepTheirParametersAndConstraints()
        {
            AssertParameters(typeof(CharacterState_RailMovement<,>), ("AbilityType", typeof(CharacterAbility_MovementRail<>)), ("AbilityDefType", typeof(CharacterAbilityDefinition_MovementRail)));
            AssertParameters(typeof(CharacterState_RailMovementBoostBase<,>), ("TAbility", typeof(CharacterAbility_MovementRailBoostBase<>)), ("TAbilityDef", typeof(CharacterAbilityDefinition_MovementRailBoostBase)));
            AssertParameters(typeof(CharacterState_GroundBoostBase<,>), ("AbilityDefType", typeof(CharacterAbilityDefinition_MovementGroundBoostBase)), ("AbilityType", typeof(CharacterAbility_MovementGroundBoostBase<>)));
            foreach (Type type in new[] { typeof(CharacterState_RailMovement<,>), typeof(CharacterState_RailMovementBoostBase<,>), typeof(CharacterState_GroundBoostBase<,>) })
                Assert.That(type.IsAbstract, Is.True, type.Name);
            Assert.That(typeof(CharacterState_RailMovement<,>).BaseType.GetGenericTypeDefinition(), Is.EqualTo(typeof(CharacterState_Rail<>)));
            Assert.That(typeof(CharacterState_RailSwitch).BaseType, Is.EqualTo(typeof(CharacterState_Rail<CharacterAbility_RailSwitch>)));
            Assert.That(typeof(CharacterState_OrbCollect).BaseType, Is.EqualTo(typeof(ActorState_Effect)));
        }

        private static void AssertParameters(Type type, params (string name, Type constraint)[] expected)
        {
            Type[] parameters = type.GetGenericArguments();
            Assert.That(parameters.Select(parameter => parameter.Name).ToArray(), Is.EqualTo(expected.Select(item => item.name).ToArray()), type.Name);
            for (int index = 0; index < expected.Length; index++)
            {
                Type constraint = parameters[index].GetGenericParameterConstraints().Single();
                Type actual = constraint.IsGenericType ? constraint.GetGenericTypeDefinition() : constraint;
                Assert.That(actual, Is.EqualTo(expected[index].constraint), type.Name + " " + expected[index].name);
            }
        }

        // ---- CanQueueActions ----

        // 0xE70670 and 0xE78010 return true, 0xE6F9F0 and 0xE76260 return false: the boost states never take over
        // the queued actions.
        [Test]
        public void BoostStatesDoNotQueueActionsAndTheOthersDo()
        {
            foreach ((string node, bool expected) in new[]
            {
                (nameof(CharacterState_GroundBoost), false), (nameof(CharacterState_GroundBoostOneShot), false),
                (nameof(CharacterState_RailMovementBoost), false), (nameof(CharacterState_RailMovementBoostOneShot), false),
                (nameof(CharacterState_GroundBraking), true), (nameof(CharacterState_GroundRollingMovement), true),
                (nameof(CharacterState_PrepareHub), true), (nameof(CharacterState_RailMovementStanding), true),
            })
            {
                Assert.That((bool)Call(NewState(node, "{}"), "CanQueueActions"), Is.EqualTo(expected), node);
            }
        }

        // ---- Rail movement queued actions (0xE784F0, 0xE785D0, 0xE787A0, 0xE78020) ----

        private sealed class Rail
        {
            public CharacterGravityVectorProbe Character;
            public CharacterAbility_MovementRailStanding Standing;
            public CharacterAbility_MovementRailBoost Boost;
            public IFSMState StandingState;
            public IFSMState BoostState;
        }

        private static T NewRailAbility<T, TDefinition>(Owned owned, Character character, Action<TDefinition> configure = null)
            where T : CharacterAbility, new() where TDefinition : CharacterAbilityDefinition
        {
            var definition = owned.Track(ScriptableObject.CreateInstance<TDefinition>());
            configure?.Invoke(definition);
            var ability = new T();
            Set(ability, "m_character", character);
            Set(ability, "m_definition", definition);
            return ability;
        }

        // The character's clock is cached at ten seconds, so a queued jump stamped 10.2 has waited 0.2 (the elapsed time
        // of a jump is the stamp minus the clock) and a queued boost stamped 9.8 has waited 0.2 (the clock minus the stamp).
        private static void WithRail(Action<Owned, Rail> body) =>
            WithAbility<CharacterAbility_MovementRailStanding, CharacterAbilityDefinition_MovementRailStanding>((owned, character, standing, definition) =>
            {
                Set(character, "m_totalFixedTime", 10f);
                SetProperty(character, "QueuedActions", new CharacterQueuedActions(character));
                body(owned, new Rail
                {
                    Character = character,
                    Standing = standing,
                    Boost = NewRailAbility<CharacterAbility_MovementRailBoost, CharacterAbilityDefinition_MovementRailBoost>(owned, character),
                    StandingState = NewState(nameof(CharacterState_RailMovementStanding), AbilityJson(ActorAbilityType.Character_RailSwitch)),
                    BoostState = NewState(nameof(CharacterState_RailMovementBoost), AbilityJson(ActorAbilityType.Character_MovementRailBoost)),
                });
            });

        private static Dictionary<ActorAbilityType, ActorAbility> Abilities(Character character) =>
            Get<Dictionary<ActorAbilityType, ActorAbility>>(character, "m_abilities");

        private static bool Stored(Character character, GraphStorageKey key) => character.Storage.TryGetValue(key, out bool value) && value;

        private static bool IsStored(Character character, GraphStorageKey key) => character.Storage.TryGetValue(key, out bool _);

        private static bool QueuedJump(object ability) => Get<bool>(ability, "m_queuedJump");

        private static bool QueuedBoost(object ability) => Get<bool>(ability, "m_queuedBoost");

        private static void QueueJumpOnCharacter(Character character, float stamp)
        {
            Set(character.QueuedActions, "m_jump", true);
            Set(character.QueuedActions, "m_jumpTime", stamp);
        }

        private static void QueueBoostOnCharacter(Character character, float stamp)
        {
            Set(character.QueuedActions, "m_boost", true);
            Set(character.QueuedActions, "m_boostTime", stamp);
        }

        // 0xE7853C..0xE785BD: nothing happens without a queued jump. With one, the one-shot takes it while its elapsed
        // time is under the window (jl in CanTriggerBefore, 0xE7855A) and the plain ability takes it afterwards, and
        // each hand over leaves the rail (0xE785D0: the rail ability's queued jump flag, RailActive false and the
        // jump-on-rail flag true). Unlike the ground state, the ability that did not take the jump is left as it was.
        [Test]
        public void QueuedJumpGoesToTheOneShotInsideItsWindowAndToThePlainAbilityAfterwards()
        {
            WithRail((owned, rail) =>
            {
                var oneShot = NewRailAbility<CharacterAbility_MovementJumpOneShot, CharacterAbilityDefinition_JumpOneShot>(owned, rail.Character, definition => definition.TriggerTimeBefore = 0.5f);
                var plain = NewRailAbility<CharacterAbility_MovementJump, CharacterAbilityDefinition_Jump>(owned, rail.Character);

                InvokeStatic(StandingType, "ProcessQueuedJump", rail.Character, rail.Standing, plain, oneShot);
                Assert.That((QueuedJump(plain), QueuedJump(oneShot), QueuedJump(rail.Standing)), Is.EqualTo((false, false, false)), "nothing queued");
                Assert.That(IsStored(rail.Character, ActorFSMKeys.RailActive), Is.False);

                Set(plain, "m_queuedJump", true);
                QueueJumpOnCharacter(rail.Character, 10.2f);
                InvokeStatic(StandingType, "ProcessQueuedJump", rail.Character, rail.Standing, plain, oneShot);
                Assert.That((QueuedJump(oneShot), QueuedJump(plain), QueuedJump(rail.Standing)), Is.EqualTo((true, true, true)), "inside the window");
                Assert.That((IsStored(rail.Character, ActorFSMKeys.RailActive), Stored(rail.Character, ActorFSMKeys.RailActive), Stored(rail.Character, ActorFSMKeys.JumpOnRailTracker)),
                    Is.EqualTo((true, false, true)));

                Set(oneShot, "m_queuedJump", false);
                Set(plain, "m_queuedJump", false);
                Set(rail.Standing, "m_queuedJump", false);
                QueueJumpOnCharacter(rail.Character, 10.8f);
                InvokeStatic(StandingType, "ProcessQueuedJump", rail.Character, rail.Standing, plain, oneShot);
                Assert.That((QueuedJump(oneShot), QueuedJump(plain), QueuedJump(rail.Standing)), Is.EqualTo((false, true, true)), "past the window");

                Set(plain, "m_queuedJump", false);
                Set(rail.Standing, "m_queuedJump", false);
                InvokeStatic(StandingType, "ProcessQueuedJump", rail.Character, rail.Standing, null, oneShot);
                Assert.That((QueuedJump(oneShot), QueuedJump(plain), QueuedJump(rail.Standing)), Is.EqualTo((false, false, false)), "past the window with no plain ability");
            });
        }

        // 0xE787DB..0xE78839: the same routing for the queued boost, through the boost abilities' QueueBoost.
        [Test]
        public void QueuedBoostGoesToTheOneShotInsideItsWindowAndToThePlainAbilityAfterwards()
        {
            WithRail((owned, rail) =>
            {
                var oneShot = NewRailAbility<CharacterAbility_MovementRailBoostOneShot, CharacterAbilityDefinition_MovementRailBoostOneShot>(owned, rail.Character, definition => definition.TriggerTimeBefore = 0.5f);

                InvokeStatic(StandingType, "ProcessQueuedBoost", rail.Character, rail.Boost, oneShot);
                Assert.That((QueuedBoost(rail.Boost), QueuedBoost(oneShot)), Is.EqualTo((false, false)), "nothing queued");

                QueueBoostOnCharacter(rail.Character, 9.8f);
                InvokeStatic(StandingType, "ProcessQueuedBoost", rail.Character, rail.Boost, oneShot);
                Assert.That((QueuedBoost(rail.Boost), QueuedBoost(oneShot)), Is.EqualTo((false, true)), "inside the window");

                Set(oneShot, "m_queuedBoost", false);
                QueueBoostOnCharacter(rail.Character, 9.2f);
                InvokeStatic(StandingType, "ProcessQueuedBoost", rail.Character, rail.Boost, oneShot);
                Assert.That((QueuedBoost(rail.Boost), QueuedBoost(oneShot)), Is.EqualTo((true, false)), "past the window");

                Set(rail.Boost, "m_queuedBoost", false);
                InvokeStatic(StandingType, "ProcessQueuedBoost", rail.Character, null, oneShot);
                Assert.That(QueuedBoost(oneShot), Is.False, "past the window with no plain ability");
                InvokeStatic(StandingType, "ProcessQueuedBoost", rail.Character, rail.Boost, null);
                Assert.That(QueuedBoost(rail.Boost), Is.True, "no one-shot");
            });
        }

        // 0xE78041..0xE781A3: when the countdown is asked to check the stored timer, a missing (zero) timer ends the call
        // before the abilities are looked up, so the queued boost stays queued on the character.
        [Test]
        public void RemainTimerWithNothingStoredIsSkippedWhenChecked()
        {
            WithRail((owned, rail) =>
            {
                Abilities(rail.Character)[ActorAbilityType.Character_MovementRailBoost] = rail.Boost;
                QueueBoostOnCharacter(rail.Character, 9.8f);
                Call(rail.StandingState, "UpdateRemainTimer", rail.Character, rail.Standing, true, 0.25f);
                Assert.That(QueuedBoost(rail.Boost), Is.False);
                Call(rail.StandingState, "UpdateRemainTimer", rail.Character, rail.Standing, false, 0.25f);
                Assert.That(QueuedBoost(rail.Boost), Is.True, "unchecked: the abilities take the queued boost");
            });
        }

        // 0xE78221..0xE78234: without any of the jump and boost abilities the countdown is not run at all.
        [Test]
        public void RemainTimerWithoutAnyJumpOrBoostAbilityDoesNothing()
        {
            WithRail((owned, rail) =>
            {
                QueueBoostOnCharacter(rail.Character, 9.8f);
                rail.Character.Storage.SetValue(ActorFSMKeys.StateRemainTimer, 1f);
                Call(rail.StandingState, "UpdateRemainTimer", rail.Character, rail.Standing, true, 0.25f);
                Assert.That(rail.Character.Storage.GetValue(ActorFSMKeys.StateRemainTimer, -1f, false), Is.EqualTo(1f));
                Assert.That(rail.Character.QueuedActions.Boost, Is.True);
            });
        }

        // 0xE78248..0xE7833B and 0xE783E9: while time remains the timer counts down and, when the state queues actions
        // (CanQueueActions, vcall 0xE78248), the abilities take the queued boost; the character's queue is not reset.
        // A state that does not queue actions (the boost state, 0xE76260) hands nothing over yet.
        [Test]
        public void RemainTimerStillRunningCountsDownAndHandsOverOnlyForQueueingStates()
        {
            WithRail((owned, rail) =>
            {
                Abilities(rail.Character)[ActorAbilityType.Character_MovementRailBoost] = rail.Boost;
                QueueBoostOnCharacter(rail.Character, 9.8f);

                rail.Character.Storage.SetValue(ActorFSMKeys.StateRemainTimer, 1f);
                Call(rail.StandingState, "UpdateRemainTimer", rail.Character, rail.Standing, true, 0.25f);
                Assert.That(rail.Character.Storage.GetValue(ActorFSMKeys.StateRemainTimer, -1f, false), Is.EqualTo(0.75f));
                Assert.That(QueuedBoost(rail.Boost), Is.True, "handed over");
                Assert.That(rail.Character.QueuedActions.Boost, Is.True, "the character's queue is kept");

                Set(rail.Boost, "m_queuedBoost", false);
                rail.Character.Storage.SetValue(ActorFSMKeys.StateRemainTimer, 1f);
                Call(rail.BoostState, "UpdateRemainTimer", rail.Character, rail.Boost, true, 0.25f);
                Assert.That(rail.Character.Storage.GetValue(ActorFSMKeys.StateRemainTimer, -1f, false), Is.EqualTo(0.75f));
                Assert.That(QueuedBoost(rail.Boost), Is.False, "a boost state hands nothing over while time remains");
            });
        }

        // 0xE783C0..0xE784D3: an expired timer is clamped to zero (maxss) and, only with an air movement ability to
        // reset (0xB79C3514), the queued boost is handed over once more and the character's queue is reset.
        [Test]
        public void ExpiredRemainTimerHandsOverAndResetsTheQueueOnlyWithAnAirAbility()
        {
            WithRail((owned, rail) =>
            {
                Abilities(rail.Character)[ActorAbilityType.Character_MovementRailBoost] = rail.Boost;
                QueueBoostOnCharacter(rail.Character, 9.8f);

                rail.Character.Storage.SetValue(ActorFSMKeys.StateRemainTimer, 0.1f);
                Call(rail.BoostState, "UpdateRemainTimer", rail.Character, rail.Boost, true, 0.25f);
                Assert.That(rail.Character.Storage.GetValue(ActorFSMKeys.StateRemainTimer, -1f, false), Is.EqualTo(0f), "clamped, not negative");
                Assert.That(QueuedBoost(rail.Boost), Is.False, "no air ability: nothing is handed over by a boost state");
                Assert.That(rail.Character.QueuedActions.Boost, Is.True);

                Abilities(rail.Character)[ActorAbilityType.Character_MovementAir] = new CharacterAbility_MovementAir();
                rail.Character.Storage.SetValue(ActorFSMKeys.StateRemainTimer, 0.1f);
                Call(rail.BoostState, "UpdateRemainTimer", rail.Character, rail.Boost, true, 0.25f);
                Assert.That(QueuedBoost(rail.Boost), Is.True, "handed over once the time ran out");
                Assert.That(rail.Character.QueuedActions.Boost, Is.False, "the queue is reset after the hand over");

                // 0xE783E9 (ja): a timer that reaches exactly zero has run out as well.
                Set(rail.Boost, "m_queuedBoost", false);
                QueueBoostOnCharacter(rail.Character, 9.8f);
                rail.Character.Storage.SetValue(ActorFSMKeys.StateRemainTimer, 0.25f);
                Call(rail.BoostState, "UpdateRemainTimer", rail.Character, rail.Boost, true, 0.25f);
                Assert.That(QueuedBoost(rail.Boost), Is.True, "handed over at exactly zero");
                Assert.That(rail.Character.QueuedActions.Boost, Is.False, "reset at exactly zero");
            });
        }

        // 0xE78282, 0xE7843A and 0xE784B9..0xE784CA: an expired timer with an air ability hands the queued jump over
        // (to the plain ability, which leaves the rail through the rail ability's QueueJump at 0xE785D0) and then
        // resets the queue because the jump is queued, the jump flag (+0x18) being tested before the boost flag
        // (+0x20); without the air ability nothing is handed over and nothing is reset.
        [Test]
        public void ExpiredRemainTimerHandsTheQueuedJumpOverAndResetsTheQueueOnlyWithAnAirAbility()
        {
            WithRail((owned, rail) =>
            {
                var plain = NewRailAbility<CharacterAbility_MovementJump, CharacterAbilityDefinition_Jump>(owned, rail.Character);
                Abilities(rail.Character)[ActorAbilityType.Character_Jump] = plain;
                QueueJumpOnCharacter(rail.Character, 10.8f);

                rail.Character.Storage.SetValue(ActorFSMKeys.StateRemainTimer, 0.1f);
                Call(rail.BoostState, "UpdateRemainTimer", rail.Character, rail.Boost, true, 0.25f);
                Assert.That((QueuedJump(plain), QueuedJump(rail.Boost)), Is.EqualTo((false, false)), "no air ability: nothing is handed over");
                Assert.That(rail.Character.QueuedActions.Jump, Is.True, "no air ability: nothing is reset");
                Assert.That(IsStored(rail.Character, ActorFSMKeys.RailActive), Is.False);

                Abilities(rail.Character)[ActorAbilityType.Character_MovementAir] = new CharacterAbility_MovementAir();
                rail.Character.Storage.SetValue(ActorFSMKeys.StateRemainTimer, 0.1f);
                Call(rail.BoostState, "UpdateRemainTimer", rail.Character, rail.Boost, true, 0.25f);
                Assert.That(QueuedJump(plain), Is.True, "handed over to the plain jump ability");
                Assert.That(QueuedJump(rail.Boost), Is.True, "and the rail ability is told to leave the rail");
                Assert.That((IsStored(rail.Character, ActorFSMKeys.RailActive), Stored(rail.Character, ActorFSMKeys.RailActive), Stored(rail.Character, ActorFSMKeys.JumpOnRailTracker)),
                    Is.EqualTo((true, false, true)));
                Assert.That(rail.Character.QueuedActions.Jump, Is.False, "the queued jump alone resets the queue");
                Assert.That(rail.Character.Storage.GetValue(ActorFSMKeys.StateRemainTimer, -1f, false), Is.EqualTo(0f));
            });
        }

        // 0xE78191..0xE781A3, 0xE78338..0xE7833B and 0xE783E1..0xE783E9: an unordered timer is not skipped by the
        // checked zero test (ucomiss then jne/jnp), is stored as zero (maxss returns its second operand for NaN) and
        // counts as run out (ja is false), so the air path hands the boost over and resets the queue.
        [Test]
        public void UnorderedRemainTimerIsStoredAsZeroAndCountsAsRunOut()
        {
            WithRail((owned, rail) =>
            {
                Abilities(rail.Character)[ActorAbilityType.Character_MovementRailBoost] = rail.Boost;
                Abilities(rail.Character)[ActorAbilityType.Character_MovementAir] = new CharacterAbility_MovementAir();
                QueueBoostOnCharacter(rail.Character, 9.8f);
                rail.Character.Storage.SetValue(ActorFSMKeys.StateRemainTimer, float.NaN);

                Call(rail.BoostState, "UpdateRemainTimer", rail.Character, rail.Boost, true, 0.25f);

                Assert.That(rail.Character.Storage.GetValue(ActorFSMKeys.StateRemainTimer, -1f, false), Is.EqualTo(0f));
                Assert.That(QueuedBoost(rail.Boost), Is.True);
                Assert.That(rail.Character.QueuedActions.Boost, Is.False);
            });
        }

        // ---- OrbCollect.GetAnimationDefinition (0x7864A0) ----

        // The outro sequence stored under OutroSequenceType picks the animation: the two replay sequences (0xd630cc8b,
        // 0x0a9e9bfd) take the ghosted one, every other value the state animation. A missing key reads the default
        // sequence (0x23e4013a).
        [Test]
        public void OrbCollectPicksTheGhostedAnimationForTheReplayOutros()
        {
            WithCharacter((owned, character) =>
            {
                var stateAnimation = owned.Track(ScriptableObject.CreateInstance<ActorAnimationDefinition>());
                var ghostedAnimation = owned.Track(ScriptableObject.CreateInstance<ActorAnimationDefinition>());
                IFSMState state = NewState(nameof(CharacterState_OrbCollect), "{}");
                Set(state, "m_stateAnimationDefinition", stateAnimation);
                Set(state, "m_ghostedOrbAnimationDefinition", ghostedAnimation);

                character.Storage.RemoveValue<OutroSequenceIdentifier>(ActorFSMKeys.OutroSequenceType);
                Assert.That(Call(state, "GetAnimationDefinition", character), Is.SameAs(stateAnimation), "no sequence stored");
                foreach ((OutroSequenceIdentifier sequence, bool ghosted) in new[]
                {
                    (OutroSequenceIdentifier.Default, false), (OutroSequenceIdentifier.Moon, false),
                    (OutroSequenceIdentifier.MoonReplay, true), (OutroSequenceIdentifier.Replay, true),
                    ((OutroSequenceIdentifier)7, false),
                })
                {
                    character.Storage.SetValue(ActorFSMKeys.OutroSequenceType, sequence);
                    Assert.That(Call(state, "GetAnimationDefinition", character), Is.SameAs(ghosted ? ghostedAnimation : stateAnimation), sequence.ToString());
                }
            });
        }

        // ---- Call sequences of the bodies that need a started Character ----

        // 0x786400, 0x786650 and 0x786660: the orb collect state never calls its base; entering activates the state
        // effect and leaving deactivates it, and the update is empty (a single ret).
        [Test]
        public void OrbCollectDoesNotCallItsBaseAndHasAnEmptyUpdate()
        {
            foreach (string method in new[] { "DoOnEnter", "DoOnLeave" })
            {
                List<MethodBase> calls = Calls(typeof(CharacterState_OrbCollect), method);
                Assert.That(calls.Any(call => call.DeclaringType == typeof(ActorState_Effect) || call.DeclaringType == typeof(ActorState) && call.Name == method), Is.False, method);
                Assert.That(Count(calls, "CharacterState_OrbCollect", "GetAnimationDefinition"), Is.EqualTo(1), method);
            }
            Assert.That(Count(Calls(typeof(CharacterState_OrbCollect), "DoOnEnter"), "ActorState", "ActivateStateEffect"), Is.EqualTo(1));
            Assert.That(Count(Calls(typeof(CharacterState_OrbCollect), "DoOnLeave"), "ActorState", "DeactivateStateEffect"), Is.EqualTo(1));
            Assert.That(Calls(typeof(CharacterState_OrbCollect), "DoUpdate"), Is.Empty);
        }

        // 0x7770B0: after the base leave the proxy's rotation is handed to the character, the override is dropped and
        // smoothing is re-enabled before the launch (OnLaunch) and the launch animation.
        [Test]
        public void SpinDashChargeLeavesInTheNativeOrder()
        {
            List<MethodBase> calls = Calls(typeof(CharacterState_GroundSpinDashCharge), "DoOnLeave");
            int baseLeave = IndexOf(calls, "CharacterState_Ground", "DoOnLeave");
            int setRotation = IndexOf(calls, "Actor", "SetWorldRotation", baseLeave);
            int stop = IndexOf(calls, "SmoothedTransformRotator", "StopOverridingRotation", setRotation);
            int smoothing = IndexOf(calls, "SmoothedTransformRotator", "ToggleSmoothing", stop);
            int launch = IndexOf(calls, "CharacterAbility_MovementGroundSpinDashCharge", "OnLaunch", smoothing);
            IndexOf(calls, "CharacterState_GroundSpinDashCharge", "SetLaunchAnimation", launch);
        }

        // 0x776DA0: the charge is announced to the ability and smoothing of the proxy is switched off.
        [Test]
        public void SpinDashChargeEntersByAnnouncingTheCharge()
        {
            List<MethodBase> calls = Calls(typeof(CharacterState_GroundSpinDashCharge), "DoOnEnter");
            int baseEnter = IndexOf(calls, "CharacterState_Ground", "DoOnEnter");
            int charge = IndexOf(calls, "CharacterAbility_MovementGroundSpinDashCharge", "OnCharge", baseEnter);
            IndexOf(calls, "SmoothedTransformRotator", "ToggleSmoothing", charge);
        }

        // 0x77FA80: the rail switch ability is reset with the cooldown after the base leave, then the three stored values
        // (elapsed time, start position, resume speed) are removed under their own types. The start velocity is kept.
        [Test]
        public void RailSwitchLeavesByResettingTheAbilityAndRemovingItsValues()
        {
            List<MethodBase> calls = Calls(typeof(CharacterState_RailSwitch), "DoOnLeave");
            int baseLeave = IndexOf(calls, "CharacterState_Rail", "DoOnLeave");
            int reset = IndexOf(calls, "CharacterAbility_RailSwitch", "Reset", baseLeave);
            int firstRemove = IndexOf(calls, "IGraphStorage", "RemoveValue", reset);
            Assert.That(firstRemove, Is.GreaterThan(reset));
            Assert.That(calls.Where(call => call.Name == "RemoveValue").SelectMany(call => ((MethodInfo)call).GetGenericArguments()).ToArray(),
                Is.EqualTo(new[] { typeof(float), typeof(Vector3), typeof(float) }));
        }

        // 0xE6F6C0: after the base enter the rolling flag is removed (as a bool) and the boost stamina is activated; a
        // turn to the intended rotation (0xE6F8B2) through the virtual SetWorldRotation (the tail jump at 0xE6F8D9)
        // follows. Only the presence and order of these calls is checked; the speed gate before the turn (0xE6F898)
        // is not covered.
        [Test]
        public void GroundBoostEntersByActivatingTheStaminaThenTurningToTheInput()
        {
            List<MethodBase> calls = Calls(typeof(CharacterState_GroundBoostBase<,>), "DoOnEnter");
            int baseEnter = IndexOf(calls, "CharacterState_Ground", "DoOnEnter");
            int remove = IndexOf(calls, "IGraphStorage", "RemoveValue", baseEnter);
            int activate = IndexOf(calls, "CharacterStamina", "Activate", remove);
            int turn = IndexOf(calls, "CharacterMovementUtilities", "GetIntendedTurnRotation", activate);
            IndexOf(calls, "Actor", "SetWorldRotation", turn);
            Assert.That(((MethodInfo)calls[remove]).GetGenericArguments(), Is.EqualTo(new[] { typeof(bool) }));
        }

        // 0xE6F8E0 and 0xE76350: both boost bases suspend the boost stamina on leaving (the suspend timer is written at
        // 0xE6F9D9 and 0xE7640E); the rail one then also marks its ability inactive (the vcall at 0xE76431).
        [Test]
        public void BoostStatesSuspendTheStaminaOnLeaving()
        {
            List<MethodBase> ground = Calls(typeof(CharacterState_GroundBoostBase<,>), "DoOnLeave");
            IndexOf(ground, "CharacterStamina", "set_SuspendTimer", IndexOf(ground, "CharacterState_Ground", "DoOnLeave"));
            List<MethodBase> rail = Calls(typeof(CharacterState_RailMovementBoostBase<,>), "DoOnLeave");
            int suspend = IndexOf(rail, "CharacterStamina", "set_SuspendTimer", IndexOf(rail, "CharacterState_RailMovement", "DoOnLeave"));
            IndexOfMovementCall(rail, "SetActive", suspend);
        }

        // 0xE76270: after the rail enter the boost stamina is activated and the ability marked active.
        [Test]
        public void RailBoostEntersByActivatingTheStaminaAndTheAbility()
        {
            List<MethodBase> calls = Calls(typeof(CharacterState_RailMovementBoostBase<,>), "DoOnEnter");
            int baseEnter = IndexOf(calls, "CharacterState_RailMovement", "DoOnEnter");
            int activate = IndexOf(calls, "CharacterStamina", "Activate", baseEnter);
            IndexOfMovementCall(calls, "SetActive", activate);
        }

        // 0xE76470: the tracker is switched to the surface tracker and teleported to the character, the surface change
        // handler is subscribed and called once with the current tracker location, and the attach finishes by clearing
        // the jump flag, activating the ability and running the remain timer unchecked.
        [Test]
        public void RailMovementEntersInTheNativeOrder()
        {
            List<MethodBase> calls = Calls(typeof(CharacterState_RailMovement<,>), "DoOnEnter");
            int baseEnter = IndexOf(calls, "CharacterState_Rail", "DoOnEnter");
            int tracking = IndexOf(calls, "Character", "SetTrackingType", baseEnter);
            int teleport = IndexOf(calls, "CharacterTracker", "OnTeleport", tracking);
            int subscribe = IndexOf(calls, "Delegate", "Combine", teleport);
            int location = IndexOf(calls, "CharacterTracker", "get_TrackerLocation", subscribe);
            int changed = IndexOf(calls, "CharacterState_RailMovement", "OnSurfaceChanged", location);
            int clear = IndexOfMovementCall(calls, "ClearJump", changed);
            int active = IndexOfMovementCall(calls, "SetActive", clear);
            IndexOf(calls, "CharacterState_RailMovement", "UpdateRemainTimer", active);
        }

        // 0xE76DA0: after the base update either the turn around or the velocity update runs, then the ground
        // orientation, the movement processing, the turn around of the character and the checked remain timer.
        [Test]
        public void RailMovementUpdatesInTheNativeOrder()
        {
            List<MethodBase> calls = Calls(typeof(CharacterState_RailMovement<,>), "DoUpdate");
            int baseUpdate = IndexOf(calls, "CharacterState_Ability", "DoUpdate");
            int turnAround = IndexOfMovementCall(calls, "UpdateTurnAround", baseUpdate);
            int velocity = IndexOf(calls, "CharacterState_RailMovement", "UpdateVelocity", baseUpdate);
            int orientate = IndexOf(calls, "CharacterState_RailMovement", "OrientateToGround", Math.Max(turnAround, velocity));
            int movement = IndexOf(calls, "CharacterState_RailMovement", "ProcessMovement", orientate);
            int characterTurn = IndexOf(calls, "Character", "TurnAround", movement);
            IndexOf(calls, "CharacterState_RailMovement", "UpdateRemainTimer", characterTurn);
        }

        // 0x7768C0 and 0x776A80: the rolling state ends the roll impulses before removing the rolling flag, and its
        // ledge braking override defers to the ground one exactly once.
        [Test]
        public void RollingMovementEndsTheRollAndDefersLedgeBraking()
        {
            List<MethodBase> update = Calls(typeof(CharacterState_GroundRollingMovement), "DoUpdate");
            int baseUpdate = IndexOf(update, "CharacterState_Ground", "DoUpdate");
            int end = IndexOf(update, "Character", "EndRollImpulses", baseUpdate);
            int remove = IndexOf(update, "IGraphStorage", "RemoveValue", end);
            Assert.That(((MethodInfo)update[remove]).GetGenericArguments(), Is.EqualTo(new[] { typeof(bool) }));
            Assert.That(Count(Calls(typeof(CharacterState_GroundRollingMovement), "UpdateLedgeBraking"), "CharacterState_Ground", "UpdateLedgeBraking"), Is.EqualTo(1));
        }

        private static int IndexOfMovementCall(List<MethodBase> calls, string name, int from = 0)
        {
            int index = calls.FindIndex(from, call => call.Name == name && call.DeclaringType.Name.StartsWith("CharacterAbility_Movement", StringComparison.Ordinal));
            Assert.That(index, Is.GreaterThanOrEqualTo(0), name + " is called on the movement ability");
            return index;
        }

        // 0xE76C40: the ability is told of the new surface before the track manager is asked whether the rail exists,
        // and the answer is then stored.
        [Test]
        public void RailMovementSurfaceChangeInformsTheAbilityBeforeStoringTheRailValidity()
        {
            List<MethodBase> calls = Calls(StandingType, "OnSurfaceChanged");
            int changed = IndexOfMovementCall(calls, "OnSurfaceChanged");
            int exists = IndexOf(calls, "TrackManager", "TrackableRailExists", changed);
            IndexOf(calls, "IGraphStorage", "SetValue", exists);
        }

        // 0xE77370: whichever way the state was left, it ends with the exit definition override, the surface change to
        // nothing, the unsubscription from the tracker, the tracking detection and the inactive ability, in that order.
        [Test]
        public void RailMovementLeaveEndsByDetachingTheAbility()
        {
            List<MethodBase> calls = Calls(StandingType, "DoOnLeave");
            int baseLeave = IndexOf(calls, "CharacterState_Rail", "DoOnLeave");
            IndexOf(calls, "Character", "SetAirControlsLockTime", baseLeave);
            IndexOf(calls, "CharacterTracker", "MoveTrackerByDistance", baseLeave);
            int exit = IndexOfMovementCall(calls, "GetExitAbilityDefinitionOverride", baseLeave);
            int surface = IndexOfMovementCall(calls, "OnSurfaceChanged", exit);
            int remove = IndexOf(calls, "Delegate", "Remove", surface);
            int detect = IndexOf(calls, "Character", "DetectTracking", remove);
            IndexOfMovementCall(calls, "SetActive", detect);
        }
    }
}
