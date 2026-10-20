using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
    // The modifier abilities: the generic modifier base, the animation-playing base and the seven leaf abilities.
    // Expected values come from the 1.10.1 x86-64 bodies cited per test. Collectable modifiers run against a real
    // CollectableManager registered into its absent row; a Character is a real component that has not been
    // initialised, so Actor.TriggerAnimationEnter/Leave return at their initialised guard and only the modifier's
    // own bookkeeping is observable. The deferred add of a modifier with listed abilities needs the coroutine host
    // and is checked from the compiled IL.
    public sealed class CharacterModifierAbilityTests
    {
        // ---- fixtures ----

        // A modifier that records the abilities it is asked to modify.
        private sealed class RecordingModifier : CharacterAbility_Modifier<CharacterAbilityDefinition_ModifierSwitchAvailable>
        {
            public readonly List<string> Events = new List<string>();
            public readonly List<CharacterAbility> Added = new List<CharacterAbility>();
            public readonly List<CharacterAbility> Removed = new List<CharacterAbility>();

            protected override void AddModifier(CharacterAbility modifyAbility)
            {
                Events.Add("add");
                Added.Add(modifyAbility);
            }

            protected override void RemoveModifier(CharacterAbility modifyAbility)
            {
                Events.Add("remove");
                Removed.Add(modifyAbility);
            }
        }

        // The animation-playing base has no abstract member left, so a leaf only needs to expose its protected members.
        private sealed class EffectProbe : CharacterAbility_ModifierEffect<CharacterAbilityDefinition_ModifierSwitchAvailable>
        {
            public void AddModifierNoTarget() => AddModifier(null);
            public void RemoveModifierNoTarget() => RemoveModifier(null);
            public void EnterAnimation(ActorAnimationDefinition animation) => TriggerAnimationEnter(animation);
            public void LeaveAnimation() => TriggerAnimationLeave();
        }

        // Rows that constructors create: the island manager for the Character, the app for every CharacterAbility,
        // the save manager for the CollectableManager. The CollectableManager is registered before any ability that
        // caches a reference to it is built.
        private static void WithModifierWorld(Action<Owned, CharacterGravityVectorProbe, CollectableManager> body) => Owned.Run(owned =>
        {
            owned.LeaseRow<GameplayIslandManager>();
            owned.LeaseRow<App>();
            owned.LeaseRow<SaveManager>();
            CollectableManager manager = owned.Register(new CollectableManager());
            CharacterGravityVectorProbe character = NewCharacter(owned);
            character.Storage.SetValue(ActorFSMKeys.FullscreenEffectDictionary, new Dictionary<int, List<FullscreenShaderManager.ParametersHandle>>());
            body(owned, character, manager);
        });

        private static TAbility Bind<TAbility, TDefinition>(Owned owned, Character character, Action<TDefinition> configure = null)
            where TAbility : ActorAbility, new()
            where TDefinition : AbilityDefinition
        {
            var definition = owned.Track(ScriptableObject.CreateInstance<TDefinition>());
            configure?.Invoke(definition);
            var ability = new TAbility();
            Set(ability, "m_character", character);
            SetProperty(ability, "Definition", definition);
            return ability;
        }

        private static CollectableTier TierOf(CollectableManager manager, CollectableType type) =>
            Get<Dictionary<CollectableType, CollectableTier>>(manager, "m_collectableTiers").TryGetValue(type, out CollectableTier tier) ? tier : null;

        private static CollectableState StateOf(CollectableManager manager, CollectableType type, int collected)
        {
            manager.RegisterCollectable(type, 0);
            manager.TryGetCollectableState(type, out CollectableState state);
            state.AdjustCollected(collected - state.Collected, false);
            return state;
        }

        private static void InvokeAdd(object ability, CharacterAbility target = null) => Call(ability, "AddModifier", target);

        private static void InvokeRemove(object ability, CharacterAbility target = null) => Call(ability, "RemoveModifier", target);

        private static bool ModifiesAbilities(object ability)
        {
            for (Type type = ability.GetType(); type != null; type = type.BaseType)
            {
                PropertyInfo property = type.GetProperty("ModifiesAbilities", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (property != null) return (bool)property.GetValue(ability);
            }
            throw new MissingMemberException("ModifiesAbilities");
        }

        // ---- CharacterAbility_Modifier<T> (020002e9) ----

        // 0xE63E00: the ActorAbility initialisation first, then the character is the actor taken as a Character and the
        // definition the definition taken as T; a definition of another type leaves none (isinst, null on mismatch).
        [Test]
        public void InitialiseTakesTheCharacterAndTheMatchingDefinition()
        {
            WithModifierWorld((owned, character, manager) =>
            {
                var definition = owned.Track(ScriptableObject.CreateInstance<CharacterAbilityDefinition_ModifierSwitchAvailable>());
                var ability = new RecordingModifier();
                ability.Initialise(character, definition);
                Assert.That(Get<Character>(ability, "m_character"), Is.SameAs(character));
                Assert.That(ability.DefinitionBase, Is.SameAs(definition));
                Assert.That(ability.UpdateEnabled, Is.True, "the ability base initialisation ran");
                Assert.That(ability.Enabled, Is.False);

                var other = owned.Track(ScriptableObject.CreateInstance<CharacterAbilityDefinition_ModifierCollectableBoost>());
                var mismatched = new RecordingModifier();
                mismatched.Initialise(character, other);
                Assert.That(Get<Character>(mismatched, "m_character"), Is.SameAs(character));
                Assert.That(mismatched.DefinitionBase, Is.Null, "no definition of the generic argument type");
            });
        }

        // 0xE63DC0: the listed ability count is above zero.
        [Test]
        public void ModifiesAbilitiesIsTrueOnlyForANonEmptyList()
        {
            WithModifierWorld((owned, character, manager) =>
            {
                RecordingModifier ability = Bind<RecordingModifier, CharacterAbilityDefinition_ModifierSwitchAvailable>(owned, character);
                var definition = (CharacterAbilityDefinition_ModifierSwitchAvailable)ability.DefinitionBase;
                Assert.That(ModifiesAbilities(ability), Is.False);
                definition.ModifyAbilities.Add(ActorAbilityType.Character_Jump);
                Assert.That(ModifiesAbilities(ability), Is.True);
            });
        }

        // 0xE63FE0 and 0xE640C0: with nothing listed the add and the remove each run once, immediately, with no target
        // (the tail call passes a null in rsi), through the entry and leave hooks (0xE63F80, 0xE63FB0).
        [Test]
        public void WithoutListedAbilitiesEnterAndLeaveModifyOnceWithNoTarget()
        {
            WithModifierWorld((owned, character, manager) =>
            {
                RecordingModifier ability = Bind<RecordingModifier, CharacterAbilityDefinition_ModifierSwitchAvailable>(owned, character);
                ability.Enter();
                Assert.That(ability.Events, Is.EqualTo(new[] { "add" }));
                Assert.That(ability.Added, Is.EqualTo(new CharacterAbility[] { null }));
                ability.Leave();
                Assert.That(ability.Events, Is.EqualTo(new[] { "add", "remove" }));
                Assert.That(ability.Removed, Is.EqualTo(new CharacterAbility[] { null }));
            });
        }

        // 0xE64180: each listed type is looked up on the character in list order, a duplicate is visited again, an absent
        // type is skipped and an ability that is not a CharacterAbility is skipped (0xE6426C null test, then the
        // CharacterAbility hierarchy test). The remove runs synchronously.
        [Test]
        public void RemovingVisitsEachPresentCharacterAbilityInListOrder()
        {
            WithModifierWorld((owned, character, manager) =>
            {
                RecordingModifier ability = Bind<RecordingModifier, CharacterAbilityDefinition_ModifierSwitchAvailable>(owned, character);
                var jump = new CharacterAbility_MovementJump();
                var boost = new CharacterAbility_MovementGroundBoost();
                var abilities = Get<Dictionary<ActorAbilityType, ActorAbility>>(character, "m_abilities");
                abilities.Add(ActorAbilityType.Character_Jump, jump);
                abilities.Add(ActorAbilityType.Character_MovementGroundBoost, boost);
                abilities.Add(ActorAbilityType.Character_JumpOneShot, ability);
                var definition = (CharacterAbilityDefinition_ModifierSwitchAvailable)ability.DefinitionBase;
                definition.ModifyAbilities.AddRange(new[]
                {
                    ActorAbilityType.Character_Jump, ActorAbilityType.Character_JumpOneShot, ActorAbilityType.Character_MovementAir,
                    ActorAbilityType.Character_MovementGroundBoost, ActorAbilityType.Character_Jump,
                });

                Call(ability, "RemoveModifiers");

                Assert.That(ability.Removed, Is.EqualTo(new CharacterAbility[] { jump, boost, jump }));
                Assert.That(ability.Added, Is.Empty);
            });
        }

        // 0xE63FE0 hands the add to CoroutineUtils.OnFixedUpdate (0x1b31cc0) through the lambda <AddModifiers>b__12_0 (0xE64330),
        // which visits the listed abilities with the add; the remove (0xE640C0) visits them directly.
        [Test]
        public void AddingListedAbilitiesWaitsForTheFixedUpdateWhereRemovingDoesNot()
        {
            Type generic = typeof(CharacterAbility_Modifier<>);
            List<MethodBase> add = Calls(generic, "AddModifiers");
            Assert.That(Count(add, "CoroutineUtils", "OnFixedUpdate"), Is.EqualTo(1));
            Assert.That(Count(add, "CharacterAbility_Modifier", "ForEachModifyAbility"), Is.EqualTo(0), "not visited directly");
            List<MethodBase> lambda = Calls(generic, "<AddModifiers>b__12_0");
            Assert.That(Count(lambda, "CharacterAbility_Modifier", "ForEachModifyAbility"), Is.EqualTo(1));
            List<MethodBase> remove = Calls(generic, "RemoveModifiers");
            Assert.That(Count(remove, "CharacterAbility_Modifier", "ForEachModifyAbility"), Is.EqualTo(1));
            Assert.That(Count(remove, "CoroutineUtils", "OnFixedUpdate"), Is.EqualTo(0));
        }

        // The deferred body itself adds to each listed ability.
        [Test]
        public void TheDeferredAddVisitsTheListedAbilitiesWithTheAdd()
        {
            WithModifierWorld((owned, character, manager) =>
            {
                RecordingModifier ability = Bind<RecordingModifier, CharacterAbilityDefinition_ModifierSwitchAvailable>(owned, character);
                var jump = new CharacterAbility_MovementJump();
                Get<Dictionary<ActorAbilityType, ActorAbility>>(character, "m_abilities").Add(ActorAbilityType.Character_Jump, jump);
                ((CharacterAbilityDefinition_ModifierSwitchAvailable)ability.DefinitionBase).ModifyAbilities.Add(ActorAbilityType.Character_Jump);

                Call(ability, "<AddModifiers>b__12_0");

                Assert.That(ability.Added, Is.EqualTo(new CharacterAbility[] { jump }));
                Assert.That(ability.Removed, Is.Empty);
            });
        }

        // ---- CharacterAbility_ModifierEffect<T> (020002ef) ----

        private static EffectProbe BindEffect(Owned owned, Character character, ActorAbilityType abilityType) =>
            Bind<EffectProbe, CharacterAbilityDefinition_ModifierSwitchAvailable>(owned, character, definition => definition.AbilityType = abilityType);

        // 0xE63B50: the handle list the character keeps for this ability's id is taken, and asking again gives the same list.
        [Test]
        public void AddingTakesTheCharactersHandleListForTheAbilityId()
        {
            WithModifierWorld((owned, character, manager) =>
            {
                EffectProbe ability = BindEffect(owned, character, ActorAbilityType.Character_Jump);
                ability.AddModifierNoTarget();
                var handles = Get<List<FullscreenShaderManager.ParametersHandle>>(ability, "m_effectHandleInstance");
                var dictionary = character.Storage.GetValue<Dictionary<int, List<FullscreenShaderManager.ParametersHandle>>>(ActorFSMKeys.FullscreenEffectDictionary);
                Assert.That(handles, Is.Not.Null.And.SameAs(dictionary[(int)ActorAbilityType.Character_Jump]));
                ability.AddModifierNoTarget();
                Assert.That(Get<List<FullscreenShaderManager.ParametersHandle>>(ability, "m_effectHandleInstance"), Is.SameAs(handles));
            });
        }

        // 0xE63C30 and 0xE63CE0: entering stores the animation; entering another leaves the first before the second is
        // stored; leaving with none is a no-op and otherwise clears it.
        [Test]
        public void EnteringAnAnimationReplacesTheOneRunningAndLeavingClearsIt()
        {
            WithModifierWorld((owned, character, manager) =>
            {
                EffectProbe ability = BindEffect(owned, character, ActorAbilityType.Character_Jump);
                ActorAnimationDefinition first = owned.Track(ScriptableObject.CreateInstance<ActorAnimationDefinition>());
                ActorAnimationDefinition second = owned.Track(ScriptableObject.CreateInstance<ActorAnimationDefinition>());
                ability.LeaveAnimation();
                Assert.That(Get<ActorAnimationDefinition>(ability, "m_animationDefinition") == null, Is.True);
                ability.EnterAnimation(first);
                Assert.That(Get<ActorAnimationDefinition>(ability, "m_animationDefinition"), Is.SameAs(first));
                ability.EnterAnimation(second);
                Assert.That(Get<ActorAnimationDefinition>(ability, "m_animationDefinition"), Is.SameAs(second));
                ability.LeaveAnimation();
                Assert.That(Get<ActorAnimationDefinition>(ability, "m_animationDefinition") == null, Is.True);
            });
        }

        // 0xE63B90: the animation is left and the handle list dropped.
        [Test]
        public void RemovingLeavesTheAnimationAndDropsTheHandleList()
        {
            WithModifierWorld((owned, character, manager) =>
            {
                EffectProbe ability = BindEffect(owned, character, ActorAbilityType.Character_Jump);
                ActorAnimationDefinition animation = owned.Track(ScriptableObject.CreateInstance<ActorAnimationDefinition>());
                ability.AddModifierNoTarget();
                ability.EnterAnimation(animation);
                ability.RemoveModifierNoTarget();
                Assert.That(Get<ActorAnimationDefinition>(ability, "m_animationDefinition") == null, Is.True);
                Assert.That(Get<List<FullscreenShaderManager.ParametersHandle>>(ability, "m_effectHandleInstance"), Is.Null);
            });
        }

        // 0xE63C30 leaves the running animation (0x5181E0 Actor.TriggerAnimationLeave) before it enters the new one
        // (0x517870 Actor.TriggerAnimationEnter); 0xE63B90 only leaves.
        [Test]
        public void EnteringLeavesBeforeItEntersTheCharacterAnimation()
        {
            Type generic = typeof(CharacterAbility_ModifierEffect<>);
            List<MethodBase> enter = Calls(generic, "TriggerAnimationEnter");
            Assert.That(IndexOf(enter, "CharacterAbility_ModifierEffect", "TriggerAnimationLeave"), Is.LessThan(IndexOf(enter, "Actor", "TriggerAnimationEnter")));
            List<MethodBase> leave = Calls(generic, "TriggerAnimationLeave");
            Assert.That(Count(leave, "Actor", "TriggerAnimationLeave"), Is.EqualTo(1));
            List<MethodBase> remove = Calls(generic, "RemoveModifier");
            Assert.That(Count(remove, "CharacterAbility_ModifierEffect", "TriggerAnimationLeave"), Is.EqualTo(1));
        }

        // ---- CharacterAbility_ModifierCollectableDamage / Revive / Start (020002ec, 020002ed, 020002ee) ----

        // 0x74BAF0 and 0x74BBE0: the definition's collectable type gets the definition's maximum damage, and zero on removal.
        [Test]
        public void DamageSetsTheMaximumDamageOfTheDefinitionsType()
        {
            WithModifierWorld((owned, character, manager) =>
            {
                var ability = Bind<CharacterAbility_ModifierCollectableDamage, CharacterAbilityDefinition_ModifierCollectableDamage>(owned, character, definition =>
                {
                    definition.CollectableType = CollectableType.BlueCoin;
                    definition.MaxDamage = 25;
                });
                InvokeAdd(ability);
                Assert.That(TierOf(manager, CollectableType.BlueCoin).MaxDamage, Is.EqualTo(25));
                Assert.That(TierOf(manager, CollectableType.Ring), Is.Null, "only the definition's type");
                InvokeRemove(ability);
                Assert.That(TierOf(manager, CollectableType.BlueCoin).MaxDamage, Is.EqualTo(0));
            });
        }

        // 0x74BCE0 and 0x74BDE0: the multiplier of the definition, and zero on removal.
        [Test]
        public void ReviveSetsTheReviveMultiplierOfTheDefinitionsType()
        {
            WithModifierWorld((owned, character, manager) =>
            {
                var ability = Bind<CharacterAbility_ModifierCollectableRevive, CharacterAbilityDefinition_ModifierCollectableRevive>(owned, character, definition =>
                {
                    definition.CollectableType = CollectableType.Moon;
                    definition.Multiplier = 0.75f;
                });
                InvokeAdd(ability);
                Assert.That(TierOf(manager, CollectableType.Moon).ReviveMultiplier, Is.EqualTo(0.75f));
                InvokeRemove(ability);
                Assert.That(TierOf(manager, CollectableType.Moon).ReviveMultiplier, Is.EqualTo(0f));
            });
        }

        // 0x74BEE0 and 0x74BFD0: the starting count of the definition, and zero on removal.
        [Test]
        public void StartSetsTheStartingCountOfTheDefinitionsType()
        {
            WithModifierWorld((owned, character, manager) =>
            {
                var ability = Bind<CharacterAbility_ModifierCollectableStart, CharacterAbilityDefinition_ModifierCollectableStart>(owned, character, definition =>
                {
                    definition.CollectableType = CollectableType.Orb;
                    definition.StartingCount = 40;
                });
                InvokeAdd(ability);
                Assert.That(manager.GetCollectableTierStartingCount(CollectableType.Orb), Is.EqualTo(40));
                InvokeRemove(ability);
                Assert.That(manager.GetCollectableTierStartingCount(CollectableType.Orb), Is.EqualTo(0));
            });
        }

        // ---- CharacterAbility_ModifierCollectableAttract (020002ea) ----

        private static CharacterAbility_ModifierCollectableAttract BindAttract(Owned owned, Character character, int tierIndex,
            CollectableType[] types, ActorAnimationDefinition animation) =>
            Bind<CharacterAbility_ModifierCollectableAttract, CharacterAbilityDefinition_ModifierCollectableAttract>(owned, character, definition =>
            {
                definition.TierIndex = tierIndex;
                definition.CollectableTypes = types;
                definition.CollectAnimationDefinition = animation;
                definition.AbilityType = ActorAbilityType.Character_Jump;
            });

        // 0x74B410 and 0x74B6E0 call the effect base and then set the tier index on every listed type: the definition's
        // index on adding, zero on removing.
        [Test]
        public void AttractSetsTheTierIndexOnTheListedTypes()
        {
            WithModifierWorld((owned, character, manager) =>
            {
                var ability = BindAttract(owned, character, 2, new[] { CollectableType.Ring, CollectableType.Moon }, null);
                InvokeAdd(ability);
                Assert.That(manager.GetCollectableTierIndex(CollectableType.Ring), Is.EqualTo(2));
                Assert.That(manager.GetCollectableTierIndex(CollectableType.Moon), Is.EqualTo(2));
                Assert.That(TierOf(manager, CollectableType.BlueCoin), Is.Null, "an unlisted type is untouched");
                InvokeRemove(ability);
                Assert.That(TierOf(manager, CollectableType.Ring).Index, Is.EqualTo(0));
                Assert.That(TierOf(manager, CollectableType.Moon).Index, Is.EqualTo(0));
            });
        }

        // 0x74B4F9 tests the list length: an empty list means every enum value (0x74B53B skips the zero value).
        [Test]
        public void AttractWithNoListedTypesAppliesToEveryTypeExceptNone()
        {
            WithModifierWorld((owned, character, manager) =>
            {
                var ability = BindAttract(owned, character, 3, new CollectableType[0], null);
                InvokeAdd(ability);
                foreach (CollectableType type in Enum.GetValues(typeof(CollectableType)))
                {
                    if (type == CollectableType.None)
                        Assert.That(TierOf(manager, type), Is.Null, "None is skipped");
                    else
                        Assert.That(TierOf(manager, type).Index, Is.EqualTo(3), type.ToString());
                }
            });
        }

        // 0x74B556 onwards: without a collect animation nothing listens to collections; with one, a positive index
        // subscribes the handler (Delegate.Combine) and removing (index zero) unsubscribes it (Delegate.Remove).
        [Test]
        public void AttractListensToCollectionsOnlyWhileItHasAnAnimationAndAPositiveTier()
        {
            WithModifierWorld((owned, character, manager) =>
            {
                var silent = BindAttract(owned, character, 2, new[] { CollectableType.Ring }, null);
                InvokeAdd(silent);
                Assert.That(manager.OnCollectableStateUpdate, Is.Null);
                InvokeRemove(silent);
                Assert.That(manager.OnCollectableStateUpdate, Is.Null);

                ActorAnimationDefinition animation = owned.Track(ScriptableObject.CreateInstance<ActorAnimationDefinition>());
                var listening = BindAttract(owned, character, 2, new[] { CollectableType.Ring }, animation);
                InvokeAdd(listening);
                Assert.That(manager.OnCollectableStateUpdate.GetInvocationList().Length, Is.EqualTo(1));
                Assert.That(manager.OnCollectableStateUpdate.Target, Is.SameAs(listening));
                InvokeRemove(listening);
                Assert.That(manager.OnCollectableStateUpdate, Is.Null);
            });
        }

        // 0x74B650: a listed type, or any type when none is listed, plays the animation; another type does not.
        [Test]
        public void AttractPlaysItsAnimationForTheListedTypes()
        {
            WithModifierWorld((owned, character, manager) =>
            {
                ActorAnimationDefinition animation = owned.Track(ScriptableObject.CreateInstance<ActorAnimationDefinition>());
                var listed = BindAttract(owned, character, 2, new[] { CollectableType.Ring }, animation);
                InvokeAdd(listed);
                var state = new CollectableState();
                manager.OnCollectableStateUpdate(CollectableType.Moon, state, default);
                Assert.That(Get<ActorAnimationDefinition>(listed, "m_animationDefinition") == null, Is.True, "Moon is not listed");
                manager.OnCollectableStateUpdate(CollectableType.Ring, state, default);
                Assert.That(Get<ActorAnimationDefinition>(listed, "m_animationDefinition"), Is.SameAs(animation));
                InvokeRemove(listed);

                var all = BindAttract(owned, character, 2, new CollectableType[0], animation);
                InvokeAdd(all);
                manager.OnCollectableStateUpdate(CollectableType.Moon, state, default);
                Assert.That(Get<ActorAnimationDefinition>(all, "m_animationDefinition"), Is.SameAs(animation));
            });
        }

        // ---- CharacterAbility_ModifierCollectableBoost (020002eb) ----

        // 0x74B7D0 and 0x74B9A0 add and remove the ability's override on the character's boost stamina after the effect base
        // ran; 0x74B8E0 answers the stamina multiplier for the definition's type and nothing for any other.
        [Test]
        public void BoostOverridesTheBoostStaminaForItsCollectableType()
        {
            WithModifierWorld((owned, character, manager) =>
            {
                var boost = new CharacterBoostStamina(character);
                try
                {
                    SetProperty(character, "BoostStamina", boost);
                    ActorAnimationDefinition animation = owned.Track(ScriptableObject.CreateInstance<ActorAnimationDefinition>());
                    var ability = Bind<CharacterAbility_ModifierCollectableBoost, CharacterAbilityDefinition_ModifierCollectableBoost>(owned, character, definition =>
                    {
                        definition.CollectableType = CollectableType.Orb;
                        definition.StaminaMultiplier = 0.25f;
                        definition.CollectAnimationDefinition = animation;
                        definition.AbilityType = ActorAbilityType.Character_Jump;
                    });
                    Assert.That(boost.OnCollectableOverride, Is.Null);
                    InvokeAdd(ability);
                    Assert.That(boost.OnCollectableOverride.Target, Is.SameAs(ability));
                    Assert.That(Get<List<FullscreenShaderManager.ParametersHandle>>(ability, "m_effectHandleInstance"), Is.Not.Null, "the effect base ran first");

                    Assert.That(boost.OnCollectableOverride(CollectableType.Ring), Is.EqualTo(0f));
                    Assert.That(Get<ActorAnimationDefinition>(ability, "m_animationDefinition") == null, Is.True, "another type plays nothing");
                    Assert.That(boost.OnCollectableOverride(CollectableType.Orb), Is.EqualTo(0.25f));
                    Assert.That(Get<ActorAnimationDefinition>(ability, "m_animationDefinition"), Is.SameAs(animation));

                    InvokeRemove(ability);
                    Assert.That(boost.OnCollectableOverride, Is.Null);
                    Assert.That(Get<ActorAnimationDefinition>(ability, "m_animationDefinition") == null, Is.True, "the effect base left the animation");
                }
                finally
                {
                    boost.Close();
                }
            });
        }

        // 0x74B96A: without a collect animation the type still answers the multiplier and plays nothing.
        [Test]
        public void BoostWithoutAnAnimationStillAnswersTheMultiplier()
        {
            WithModifierWorld((owned, character, manager) =>
            {
                var boost = new CharacterBoostStamina(character);
                try
                {
                    SetProperty(character, "BoostStamina", boost);
                    var ability = Bind<CharacterAbility_ModifierCollectableBoost, CharacterAbilityDefinition_ModifierCollectableBoost>(owned, character, definition =>
                        definition.AbilityType = ActorAbilityType.Character_Jump);
                    InvokeAdd(ability);
                    Assert.That(boost.OnCollectableOverride(CollectableType.Ring), Is.EqualTo(0.01f));
                    Assert.That(Get<ActorAnimationDefinition>(ability, "m_animationDefinition") == null, Is.True);
                    InvokeRemove(ability);
                }
                finally
                {
                    boost.Close();
                }
            });
        }

        // ---- CharacterAbility_ModifierRingSpeed (020002f0) ----

        private static void WithRingSpeed(Action<Owned, CollectableManager, CharacterAbility_MovementGroundBoost, CharacterAbility_ModifierRingSpeed, CharacterAbilityDefinition_ModifierRingSpeed> body) =>
            WithModifierWorld((owned, character, manager) =>
            {
                var target = new CharacterAbility_MovementGroundBoost();
                var ability = Bind<CharacterAbility_ModifierRingSpeed, CharacterAbilityDefinition_ModifierRingSpeed>(owned, character, definition =>
                    definition.AbilityType = ActorAbilityType.Character_Jump);
                body(owned, manager, target, ability, (CharacterAbilityDefinition_ModifierRingSpeed)ability.DefinitionBase);
            });

        private static void Apply(CharacterAbility target, ref float acceleration, ref float speedMin, ref float speedMax, ref float deceleration) =>
            target.m_modifierSpeed(ref acceleration, ref speedMin, ref speedMax, ref deceleration);

        // 0x74C0D0 and 0x74C590: the modified ability's speed function gains the ring scaling on adding and loses it
        // on removing; adding also records the held ring count (0x74C290: the collected count of the ring state, zero
        // without a state).
        [Test]
        public void RingSpeedHooksTheModifiedAbilitysSpeedFunctionAndRecordsTheRingCount()
        {
            WithRingSpeed((owned, manager, target, ability, definition) =>
            {
                Assert.That(target.m_modifierSpeed, Is.Null);
                InvokeAdd(ability, target);
                Assert.That(target.m_modifierSpeed.Target, Is.SameAs(ability));
                Assert.That(Get<int>(ability, "m_ringCount"), Is.EqualTo(0), "no ring state");
                InvokeRemove(ability, target);
                Assert.That(target.m_modifierSpeed, Is.Null);

                StateOf(manager, CollectableType.Ring, 7);
                InvokeAdd(ability, target);
                Assert.That(Get<int>(ability, "m_ringCount"), Is.EqualTo(7), "the collected count, not the total");
                InvokeRemove(ability, target);
            });
        }


        // 0x74C300: acceleration, minimum speed, maximum speed and deceleration are multiplied by their own curve sampled
        // at the held ring count.
        [Test]
        public void RingSpeedScalesEachSpeedByItsOwnCurveAtTheRingCount()
        {
            WithRingSpeed((owned, manager, target, ability, definition) =>
            {
                definition.AccelerationMultiplier = AnimationCurve.Constant(0f, 10f, 2f);
                definition.SpeedMinMultiplier = AnimationCurve.Constant(0f, 10f, 3f);
                definition.SpeedMaxMultiplier = AnimationCurve.Constant(0f, 10f, 5f);
                definition.DecelerationMultiplier = AnimationCurve.Constant(0f, 10f, 7f);
                StateOf(manager, CollectableType.Ring, 4);
                InvokeAdd(ability, target);
                float acceleration = 1f, speedMin = 10f, speedMax = 100f, deceleration = 1000f;
                Apply(target, ref acceleration, ref speedMin, ref speedMax, ref deceleration);
                Assert.That((acceleration, speedMin, speedMax, deceleration), Is.EqualTo((2f, 30f, 500f, 7000f)));
                InvokeRemove(ability, target);
            });
        }

        // 0x74C300 also samples the curves at the ring count as a float: a linear curve gives its value at that count.
        [Test]
        public void RingSpeedSamplesTheCurvesAtTheCurrentRingCount()
        {
            WithRingSpeed((owned, manager, target, ability, definition) =>
            {
                definition.AccelerationMultiplier = AnimationCurve.Linear(0f, 0f, 10f, 5f);
                CollectableState state = StateOf(manager, CollectableType.Ring, 0);
                InvokeAdd(ability, target);
                state.AdjustCollected(6, false);
                float acceleration = 2f, speedMin = 1f, speedMax = 1f, deceleration = 1f;
                Apply(target, ref acceleration, ref speedMin, ref speedMax, ref deceleration);
                Assert.That(acceleration, Is.EqualTo(2f * 3f).Within(1e-5f), "0.5 per ring times six rings");
                InvokeRemove(ability, target);
            });
        }

        // 0x74C4B0 onwards: the tiers are walked in order; the first tier that the count has newly reached (count at or
        // above its ring count, previous count below it) plays its animation and ends the walk. A tier that was
        // reached before is skipped for a later one, and falling below the first tier's count leaves the animation.
        [Test]
        public void RingSpeedPlaysTheFirstNewlyReachedTierAndLeavesBelowTheLowestTier()
        {
            WithRingSpeed((owned, manager, target, ability, definition) =>
            {
                ActorAnimationDefinition low = owned.Track(ScriptableObject.CreateInstance<ActorAnimationDefinition>());
                ActorAnimationDefinition high = owned.Track(ScriptableObject.CreateInstance<ActorAnimationDefinition>());
                definition.TierDefinitions.Add(CharacterModifierDefinitionTests.Tier(5, low));
                definition.TierDefinitions.Add(CharacterModifierDefinitionTests.Tier(10, high));
                CollectableState state = StateOf(manager, CollectableType.Ring, 0);
                InvokeAdd(ability, target);
                ActorAnimationDefinition Playing() => Get<ActorAnimationDefinition>(ability, "m_animationDefinition");
                float a = 1f, b = 1f, c = 1f, d = 1f;

                state.AdjustCollected(7 - state.Collected, false);
                Apply(target, ref a, ref b, ref c, ref d);
                Assert.That(Playing(), Is.SameAs(low), "7 reaches the tier at 5");
                Assert.That(Get<int>(ability, "m_ringCount"), Is.EqualTo(7));

                Apply(target, ref a, ref b, ref c, ref d);
                Assert.That(Playing(), Is.SameAs(low), "nothing new at the same count");

                state.AdjustCollected(12 - state.Collected, false);
                Apply(target, ref a, ref b, ref c, ref d);
                Assert.That(Playing(), Is.SameAs(high), "12 newly reaches the tier at 10; the tier at 5 was reached before");
                Assert.That(Get<int>(ability, "m_ringCount"), Is.EqualTo(12));

                state.AdjustCollected(5 - state.Collected, false);
                Apply(target, ref a, ref b, ref c, ref d);
                Assert.That(Playing(), Is.SameAs(high), "exactly the lowest tier's count does not leave");

                state.AdjustCollected(4 - state.Collected, false);
                Apply(target, ref a, ref b, ref c, ref d);
                Assert.That(Playing() == null, Is.True, "below the lowest tier leaves the animation");
                InvokeRemove(ability, target);
            });
        }

        // 0x74C4CC: a tier whose ring count the previous count already reached is not entered again; both comparisons are
        // inclusive on the new count (0x74C4C7 jl) and exclusive on the old (0x74C4CF jge).
        [Test]
        public void RingSpeedTierBoundariesAreInclusiveOnTheNewCountAndExclusiveOnTheOld()
        {
            WithRingSpeed((owned, manager, target, ability, definition) =>
            {
                ActorAnimationDefinition tier = owned.Track(ScriptableObject.CreateInstance<ActorAnimationDefinition>());
                definition.TierDefinitions.Add(CharacterModifierDefinitionTests.Tier(5, tier));
                CollectableState state = StateOf(manager, CollectableType.Ring, 5);
                InvokeAdd(ability, target);
                float a = 1f, b = 1f, c = 1f, d = 1f;
                Apply(target, ref a, ref b, ref c, ref d);
                Assert.That(Get<ActorAnimationDefinition>(ability, "m_animationDefinition") == null, Is.True, "already at 5 when added");
                state.AdjustCollected(-1, false);
                Apply(target, ref a, ref b, ref c, ref d);
                state.AdjustCollected(1, false);
                Apply(target, ref a, ref b, ref c, ref d);
                Assert.That(Get<ActorAnimationDefinition>(ability, "m_animationDefinition"), Is.SameAs(tier), "back up to exactly 5");
                InvokeRemove(ability, target);
            });
        }

        // ---- CharacterAbility_ModifierSwitchAvailable (020002f1) ----

        // 0x74C7B0 and 0x74C7C0 store true and false in the modified ability's switch override (+0x90).
        [Test]
        public void SwitchAvailableSetsAndClearsTheTargetsSwitchOverride()
        {
            WithAbility<CharacterAbility_MovementGroundBoost, CharacterAbilityDefinition_MovementGroundBoost>((owned, character, target, targetDefinition) =>
            {
                var ability = new CharacterAbility_ModifierSwitchAvailable();
                targetDefinition.SwitchAvailable = false;
                Assert.That(target.SwitchAvailable, Is.False);
                InvokeAdd(ability, target);
                Assert.That(target.SwitchAvailable, Is.True);
                InvokeRemove(ability, target);
                Assert.That(target.SwitchAvailable, Is.False);
                targetDefinition.SwitchAvailable = true;
                InvokeRemove(ability, target);
                Assert.That(target.SwitchAvailable, Is.True, "the target definition's own flag still holds");
            });
        }

        // ---- Declaration shape (not behaviour): bases, constraints, members and options ----

        [Test]
        public void DeclarationShapeModifierTypesKeepTheirBasesAndMembers()
        {
            Type modifier = typeof(CharacterAbility_Modifier<>);
            Assert.That(modifier.IsAbstract && modifier.IsPublic, Is.True);
            Assert.That(modifier.BaseType, Is.EqualTo(typeof(ActorAbility)));
            Type argument = modifier.GetGenericArguments()[0];
            Assert.That(argument.GetGenericParameterConstraints(), Is.EqualTo(new[] { typeof(CharacterAbilityDefinition_Modifier) }));
            AssertIl2CppOptions(modifier, 2, 1);
            Assert.That(modifier.GetFields(DeclaredMembers).OrderBy(f => f.MetadataToken).Select(f => (f.Name, f.IsFamily, f.IsPrivate)).ToArray(),
                Is.EqualTo(new[] { ("m_character", true, false), ("<Definition>k__BackingField", false, true) }));
            Assert.That(modifier.GetMethods(DeclaredMembers).OrderBy(m => m.MetadataToken).Select(m => m.Name).ToArray(), Is.EqualTo(new[]
            {
                "get_Definition", "set_Definition", "get_DefinitionBase", "get_ModifiesAbilities", "Initialise", "DoOnEnter", "DoOnLeave", "AddModifiers",
                "RemoveModifiers", "ForEachModifyAbility", "AddModifier", "RemoveModifier", "<AddModifiers>b__12_0",
            }));

            Type effect = typeof(CharacterAbility_ModifierEffect<>);
            Assert.That(effect.IsAbstract && effect.IsPublic, Is.True);
            Assert.That(effect.BaseType.GetGenericTypeDefinition(), Is.EqualTo(typeof(CharacterAbility_Modifier<>)));
            AssertIl2CppOptions(effect, 1, 2);
            Assert.That(effect.GetFields(DeclaredMembers).OrderBy(f => f.MetadataToken).Select(f => f.Name).ToArray(),
                Is.EqualTo(new[] { "m_animationDefinition", "m_effectHandleInstance" }));
            Assert.That(effect.GetMethods(DeclaredMembers).OrderBy(m => m.MetadataToken).Select(m => m.Name).ToArray(),
                Is.EqualTo(new[] { "AddModifier", "RemoveModifier", "TriggerAnimationEnter", "TriggerAnimationLeave" }));
        }

        [Test]
        public void DeclarationShapeLeafAbilitiesKeepTheirBasesFieldsAndOptions()
        {
            foreach ((Type type, Type definition, Type baseGeneric, int[] options, string[] fields) in new[]
            {
                (typeof(CharacterAbility_ModifierCollectableAttract), typeof(CharacterAbilityDefinition_ModifierCollectableAttract), typeof(CharacterAbility_ModifierEffect<>), new[] { 2, 1 }, new[] { "m_collectableManagerRef" }),
                (typeof(CharacterAbility_ModifierCollectableBoost), typeof(CharacterAbilityDefinition_ModifierCollectableBoost), typeof(CharacterAbility_ModifierEffect<>), new[] { 1, 2 }, new string[0]),
                (typeof(CharacterAbility_ModifierCollectableDamage), typeof(CharacterAbilityDefinition_ModifierCollectableDamage), typeof(CharacterAbility_Modifier<>), new[] { 2, 1 }, new[] { "m_collectableManagerRef" }),
                (typeof(CharacterAbility_ModifierCollectableRevive), typeof(CharacterAbilityDefinition_ModifierCollectableRevive), typeof(CharacterAbility_Modifier<>), new[] { 2, 1 }, new[] { "m_collectableManagerRef" }),
                (typeof(CharacterAbility_ModifierCollectableStart), typeof(CharacterAbilityDefinition_ModifierCollectableStart), typeof(CharacterAbility_Modifier<>), new[] { 1, 2 }, new[] { "m_collectableManagerRef" }),
                (typeof(CharacterAbility_ModifierRingSpeed), typeof(CharacterAbilityDefinition_ModifierRingSpeed), typeof(CharacterAbility_ModifierEffect<>), new[] { 2, 1 }, new[] { "m_collectableManagerRef", "m_ringCount" }),
                (typeof(CharacterAbility_ModifierSwitchAvailable), typeof(CharacterAbilityDefinition_ModifierSwitchAvailable), typeof(CharacterAbility_Modifier<>), new[] { 2, 1 }, new string[0]),
            })
            {
                Assert.That(type.BaseType, Is.EqualTo(baseGeneric.MakeGenericType(definition)), type.Name);
                AssertIl2CppOptions(type, options);
                Assert.That(type.GetFields(DeclaredMembers).OrderBy(f => f.MetadataToken).Select(f => f.Name).ToArray(), Is.EqualTo(fields), type.Name);
            }
        }
    }
}
