using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using ProjectLucid.Tests.WorldTriggersShared;
using UnityEngine;
using UnityEngine.TestTools;
using static ProjectLucid.Tests.TriggerConditionTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // Restored Game.Runtime TriggerCondition (TypeDef 0x02000746, x86-64 0x5CC780..0x5CC9FD) and the
    // conditions that read the current character: PersistentLevelObjectBoolean (0x02000756),
    // AbilityInUse (0x02000747), ActionState (0x02000748), CharacterArchetype (0x0200074e),
    // CharacterControlsEnabled (0x0200074f), CharacterId (0x02000754), ScriptableObject (0x02000757)
    // and VelocityMagnitude (0x0200075e). Characters come from AddComponent<Character> (Edit Mode
    // runs no Awake) with a real FSMStorage; the CharacterManager is built by its constructor behind a
    // reference installed over the conditions' static one for the case (TriggerConditionTestSupport).
    public sealed class OriginalTriggerConditionTests
    {
        private static void AssertType(Type type, int typeFlags, Type baseType, int[] options,
            (string name, int flags)[] fields, (string name, int flags)[] methods)
        {
            AssertMembers(type, typeFlags, fields, methods);
            AssertIl2CppOptions(type, options);
            Assert.That(type.BaseType, Is.EqualTo(baseType), type.FullName);
        }

        private static T Add<T>(Owned owned) where T : Component => owned.Host("Lucid trigger condition", false).AddComponent<T>();

        // Metadata of the nine TypeDefs: flags, field and method order and the Il2Cpp option order.
        [Test]
        public void DeclarationsKeepTheOriginalFlagsFieldsAndMethods()
        {
            AssertType(typeof(TriggerCondition), 0x00100081, typeof(MonoBehaviour), new[] { 1, 2 },
                new[] { ("m_invert", 0x0001), ("m_characterManagerRef", 0x0011) },
                new[] { ("DoEvaluate", 0x05c4), ("Evaluate", 0x0086), ("DoEvaluate", 0x01c4), ("Evaluate", 0x0086),
                        ("TryGetCharacter", 0x0091), (".ctor", 0x1884), (".cctor", 0x1891) });
            AssertType(typeof(TriggerCondition_PersistentLevelObjectBoolean), 0x00100001, typeof(TriggerCondition), new[] { 1, 2 },
                new[] { ("m_persistentLevelObject", 0x0001) },
                new[] { ("get_PersistentLevelObject", 0x0886), ("DoEvaluate", 0x00c4), ("DoEvaluate", 0x00c4), (".ctor", 0x1886) });
            AssertType(typeof(TriggerCondition_AbilityInUse), 0x00100001, typeof(TriggerCondition), new[] { 1, 2 },
                new[] { ("m_useAbilityGroup", 0x0001), ("m_abilityTypeGroup", 0x0001), ("m_abilityType", 0x0001), ("m_usedWithinSeconds", 0x0001) },
                new[] { ("DoEvaluate", 0x00c4), (".ctor", 0x1886) });
            AssertType(typeof(TriggerCondition_ActionState), 0x00100001, typeof(TriggerCondition), new[] { 1, 2 },
                new[] { ("m_action", 0x0001) }, new[] { ("DoEvaluate", 0x00c4), (".ctor", 0x1886) });
            AssertType(typeof(TriggerCondition_CharacterArchetype), 0x00100001, typeof(TriggerCondition), new[] { 2, 1 },
                new[] { ("m_requiredCharacterArchetypes", 0x0001) }, new[] { ("DoEvaluate", 0x00c4), (".ctor", 0x1886) });
            AssertType(typeof(TriggerCondition_CharacterControlsEnabled), 0x00100001, typeof(TriggerCondition), new[] { 2, 1 },
                new (string, int)[0], new[] { ("DoEvaluate", 0x00c4), (".ctor", 0x1886) });
            AssertType(typeof(TriggerCondition_CharacterId), 0x00100001, typeof(TriggerCondition), new[] { 2, 1 },
                new[] { ("m_requiredCharacterIds", 0x0001) }, new[] { ("DoEvaluate", 0x00c4), (".ctor", 0x1886) });
            AssertType(typeof(TriggerCondition_ScriptableObject), 0x00100001, typeof(TriggerCondition), new[] { 1, 2 },
                new[] { ("m_condition", 0x0001) }, new[] { ("DoEvaluate", 0x00c4), (".ctor", 0x1886) });
            AssertType(typeof(TriggerCondition_VelocityMagnitude), 0x00100001, typeof(TriggerCondition), new[] { 1, 2 },
                new[] { ("m_velocityMagnitudeThreshold", 0x0001), ("m_comparisonType", 0x0001) },
                new[] { ("DoEvaluate", 0x00c4), (".ctor", 0x1886) });

            var serialised = new[] { "SerializeField" };
            Assert.That(AttributeTypes(Field(typeof(TriggerCondition), "m_invert")).Select(type => type.Name), Is.EqualTo(serialised));
            Assert.That(AttributeTypes(Field(typeof(TriggerCondition_AbilityInUse), "m_abilityTypeGroup")).Select(type => type.Name),
                Is.EquivalentTo(new[] { "ShowIfAttribute", "SerializeField" }));
            Assert.That(AttributeTypes(Field(typeof(TriggerCondition_AbilityInUse), "m_abilityType")).Select(type => type.Name),
                Is.EquivalentTo(new[] { "SerializeField", "HideIfAttribute" }));
            Assert.That(AttributeTypes(Field(typeof(TriggerCondition_AbilityInUse), "m_usedWithinSeconds")).Select(type => type.Name),
                Is.EquivalentTo(new[] { "HideIfAttribute", "SerializeField", "TooltipAttribute" }));
            Assert.That(Tooltip(Field(typeof(TriggerCondition_AbilityInUse), "m_usedWithinSeconds")),
                Is.EqualTo("Will return true if ability is in use or was last used this many seconds ago."));
        }

        // Evaluate() 0x5CC780 xors DoEvaluate() with m_invert (+0x20). The base DoEvaluate() 0x5CC7B0
        // is false without calling the abstract overload unless TryGetCurrentCharacter succeeds, then
        // passes that character on. The table is the full (valid, answer, invert) product.
        [Test]
        public void ParameterlessEvaluateNeedsAValidCurrentCharacterAndXorsTheInversion()
        {
            Run((owned, scene) =>
            {
                var probe = Add<TriggerConditionProbe>(owned);
                probe.UseBase = true;
                var table = new[]
                {
                    (valid: true, answer: true, invert: false, expected: true),
                    (valid: true, answer: true, invert: true, expected: false),
                    (valid: true, answer: false, invert: false, expected: false),
                    (valid: true, answer: false, invert: true, expected: true),
                    (valid: false, answer: true, invert: false, expected: false),
                    (valid: false, answer: true, invert: true, expected: true),
                    (valid: false, answer: false, invert: false, expected: false),
                    (valid: false, answer: false, invert: true, expected: true),
                };
                foreach (var row in table)
                {
                    scene.SetCurrent(scene.Character, row.valid);
                    probe.CharacterResult = row.answer;
                    Set(probe, "m_invert", row.invert);
                    probe.CharacterEvaluations = 0;
                    probe.LastCharacter = null;
                    string label = row.ToString();
                    Assert.That(probe.Evaluate(), Is.EqualTo(row.expected), label);
                    Assert.That(probe.CharacterEvaluations, Is.EqualTo(row.valid ? 1 : 0), label);
                    if (row.valid) Assert.That(probe.LastCharacter, Is.SameAs(scene.Character), label);
                    else Assert.That(probe.LastCharacter, Is.Null, label);
                }
            });
        }

        // Evaluate(Character) 0x5CC930 xors DoEvaluate(character) with m_invert and never asks the
        // CharacterManager or runs the parameterless overload.
        [Test]
        public void CharacterEvaluateXorsTheInversionForTheGivenCharacterOnly()
        {
            Run((owned, scene) =>
            {
                var probe = Add<TriggerConditionProbe>(owned);
                Character other = NewCharacter(owned, new FSMStorage(0));
                scene.SetCurrent(null, false);
                foreach (var row in new[]
                {
                    (answer: true, invert: false, expected: true),
                    (answer: true, invert: true, expected: false),
                    (answer: false, invert: false, expected: false),
                    (answer: false, invert: true, expected: true),
                })
                {
                    probe.CharacterResult = row.answer;
                    Set(probe, "m_invert", row.invert);
                    probe.LastCharacter = null;
                    Assert.That(probe.Evaluate(other), Is.EqualTo(row.expected), row.ToString());
                    Assert.That(probe.LastCharacter, Is.SameAs(other), row.ToString());
                }
                Assert.That(probe.Evaluations, Is.Zero, "the parameterless DoEvaluate never ran");
            });
        }

        // TriggerCondition_PersistentLevelObjectBoolean: DoEvaluate() 0x5CDC20 tail-calls the object's
        // virtual GetCachedValue, and DoEvaluate(Character) 0x5CDC40 ignores the character and returns
        // the parameterless Evaluate, which has already xored m_invert. Evaluate() therefore inverts
        // once and Evaluate(Character), which xors again, ignores the inversion.
        [Test]
        public void PersistentBooleanConditionInvertsOnlyOnTheParameterlessPath()
        {
            Run((owned, scene) =>
            {
                owned.LeaseRow<MissionManager>();
                GameObject host = owned.Host("Lucid persistent condition", false);
                var persistent = host.AddComponent<PersistentLevelObjectBoolean>();
                var condition = host.AddComponent<TriggerCondition_PersistentLevelObjectBoolean>();
                Set(condition, "m_persistentLevelObject", persistent);
                Assert.That(condition.PersistentLevelObject, Is.SameAs(persistent));
                foreach (var row in new[]
                {
                    (value: true, invert: false, parameterless: true, withCharacter: true),
                    (value: true, invert: true, parameterless: false, withCharacter: true),
                    (value: false, invert: false, parameterless: false, withCharacter: false),
                    (value: false, invert: true, parameterless: true, withCharacter: false),
                })
                {
                    Set(persistent, "m_currentValue", row.value);
                    Set(condition, "m_invert", row.invert);
                    Assert.That(condition.Evaluate(), Is.EqualTo(row.parameterless), row.ToString());
                    Assert.That(condition.Evaluate(scene.Character), Is.EqualTo(row.withCharacter), row.ToString());
                    Assert.That(condition.Evaluate(null), Is.EqualTo(row.withCharacter), row + " (the character is not read)");
                }
                Set(condition, "m_persistentLevelObject", null);
                Assert.Throws<NullReferenceException>(() => condition.Evaluate());
            });
        }

        // TriggerCondition_CharacterId.DoEvaluate 0x5CD7E0 compares the character's IdType (+0x35C)
        // with every listed id: first, middle and last entries match, and an empty list matches none.
        [Test]
        public void CharacterIdMatchesAnyListedId()
        {
            Run((owned, scene) =>
            {
                var condition = Add<TriggerCondition_CharacterId>(owned);
                Set(condition, "m_requiredCharacterIds", new[] { CharacterId.Sonic, CharacterId.Tails, CharacterId.Amy });
                foreach (var row in new[]
                {
                    (id: CharacterId.Sonic, expected: true),
                    (id: CharacterId.Tails, expected: true),
                    (id: CharacterId.Amy, expected: true),
                    (id: CharacterId.Knuckles, expected: false),
                    (id: CharacterId.None, expected: false),
                })
                {
                    Set(scene.Character, "<IdType>k__BackingField", row.id);
                    Assert.That(condition.Evaluate(scene.Character), Is.EqualTo(row.expected), row.ToString());
                    Assert.That(condition.Evaluate(), Is.EqualTo(row.expected), row + " through the current character");
                }
                Set(condition, "m_requiredCharacterIds", new CharacterId[0]);
                Set(scene.Character, "<IdType>k__BackingField", CharacterId.Sonic);
                Assert.That(condition.Evaluate(scene.Character), Is.False, "an empty list matches nothing");
                Set(condition, "m_invert", true);
                Assert.That(condition.Evaluate(scene.Character), Is.True);
                Set(condition, "m_requiredCharacterIds", null);
                Assert.Throws<NullReferenceException>(() => condition.Evaluate(scene.Character));
            });
        }

        // TriggerCondition_CharacterArchetype.DoEvaluate 0x5CD4C0 compares the archetype of the
        // character's definition (Definition +0x400, archetype +0x5C) with every listed archetype.
        [Test]
        public void CharacterArchetypeMatchesAnyListedArchetype()
        {
            Run((owned, scene) =>
            {
                // CreateInstance sends Awake, whose UpdateCachedValues (0x7A8060) calls
                // Settings.UpdateCachedValues on the unassigned Settings (+0x28) with no null test.
                LogAssert.Expect(LogType.Exception, new Regex("^NullReferenceException: Object reference not set to an instance of an object$"));
                var definition = owned.Track(ScriptableObject.CreateInstance<CharacterDefinition>());
                Set(scene.Character, "<Definition>k__BackingField", definition);
                var condition = Add<TriggerCondition_CharacterArchetype>(owned);
                Set(condition, "m_requiredCharacterArchetypes", new[] { CharacterArchetype.Flying, CharacterArchetype.Climbing });
                foreach (var row in new[]
                {
                    (archetype: CharacterArchetype.Flying, expected: true),
                    (archetype: CharacterArchetype.Climbing, expected: true),
                    (archetype: CharacterArchetype.Dashing, expected: false),
                    (archetype: CharacterArchetype.None, expected: false),
                })
                {
                    Set(definition, "m_archetype", row.archetype);
                    Assert.That(condition.Evaluate(scene.Character), Is.EqualTo(row.expected), row.ToString());
                }
                Set(condition, "m_requiredCharacterArchetypes", new CharacterArchetype[0]);
                Set(definition, "m_archetype", CharacterArchetype.Flying);
                Assert.That(condition.Evaluate(scene.Character), Is.False, "an empty list matches nothing");
            });
        }

        // TriggerCondition_CharacterControlsEnabled.DoEvaluate 0x5CD570 tail-calls
        // Character.AreControlsEnabled (0x72FF70), which reads the ControlsEnabled modifier.
        [Test]
        public void ControlsEnabledFollowsTheCharactersControlsModifier()
        {
            Run((owned, scene) =>
            {
                var modifiers = new StackableData();
                modifiers.SetRetrievalOperation<bool>((int)GameplayModifierType.ControlsEnabled, StackableData.RetrievalOperation.Latest, false);
                Set(scene.Character, "m_modifiers", modifiers);
                var condition = Add<TriggerCondition_CharacterControlsEnabled>(owned);
                foreach (bool enabled in new[] { true, false })
                {
                    modifiers.SetBaseValue((int)GameplayModifierType.ControlsEnabled, enabled);
                    Assert.That(scene.Character.AreControlsEnabled(), Is.EqualTo(enabled), "the modifier is set");
                    Assert.That(condition.Evaluate(scene.Character), Is.EqualTo(enabled));
                    Assert.That(condition.Evaluate(), Is.EqualTo(enabled));
                    Set(condition, "m_invert", true);
                    Assert.That(condition.Evaluate(scene.Character), Is.EqualTo(!enabled));
                    Set(condition, "m_invert", false);
                }
            });
        }

        private sealed class AppliedBrain : CharacterBrain
        {
            public override void Close()
            {
            }
        }

        // TriggerCondition_ActionState.DoEvaluate 0x5CCBF0 tail-calls Character.GetBrainAppliedState
        // (0x7351A0) with m_action (+0x24), so only the configured action reports.
        [Test]
        public void ActionStateReportsTheConfiguredActionsAppliedState()
        {
            Run((owned, scene) =>
            {
                CharacterActionFlags.Initialise();
                var applied = new CharacterActionFlags();
                applied.SetValue(GameAction.CharacterJump, true);
                var brain = new AppliedBrain();
                brain.ApplyActions(applied, 1f);
                Set(scene.Character, "m_brain", brain);
                var condition = Add<TriggerCondition_ActionState>(owned);
                foreach (var row in new[]
                {
                    (action: GameAction.CharacterJump, expected: true),
                    (action: GameAction.CharacterRoll, expected: false),
                })
                {
                    Set(condition, "m_action", row.action);
                    Assert.That(condition.Evaluate(scene.Character), Is.EqualTo(row.expected), row.ToString());
                }
                Set(condition, "m_action", GameAction.CharacterJump);
                Set(condition, "m_invert", true);
                Assert.That(condition.Evaluate(scene.Character), Is.False);
            });
        }

        // TriggerCondition_VelocityMagnitude.DoEvaluate 0x5CE2B0 passes the character's world velocity
        // magnitude (+0xD0) first and m_velocityMagnitudeThreshold (+0x24) second to the float
        // ComparisonTypeExtensions.Evaluate (0x6AE630), with m_comparisonType (+0x28) as the type.
        // The default type is None (the constructor stores nothing), which always passes.
        [Test]
        public void VelocityMagnitudeComparesTheMagnitudeWithTheThresholdInOrder()
        {
            Run((owned, scene) =>
            {
                var condition = Add<TriggerCondition_VelocityMagnitude>(owned);
                Set(condition, "m_velocityMagnitudeThreshold", 2f);
                Set(scene.Character, "<WorldVelocityMagnitude>k__BackingField", 1f);
                Assert.That(Get<ComparisonType>(condition, "m_comparisonType"), Is.EqualTo(ComparisonType.None));
                Assert.That(condition.Evaluate(scene.Character), Is.True, "the default type passes whatever the magnitude");
                foreach (var row in ComparisonTable)
                {
                    Set(condition, "m_comparisonType", row.type);
                    foreach (var sample in new[] { (magnitude: 3f, expected: row.above), (magnitude: 2f, expected: row.equal), (magnitude: 1f, expected: row.below) })
                    {
                        Set(scene.Character, "<WorldVelocityMagnitude>k__BackingField", sample.magnitude);
                        Assert.That(condition.Evaluate(scene.Character), Is.EqualTo(sample.expected), row.type + " " + sample.magnitude);
                    }
                }
            });
        }

        private static Dictionary<ActorAbilityType, ActorAbilityInUseData> InUse(params ActorAbilityType[] abilities)
        {
            var data = new Dictionary<ActorAbilityType, ActorAbilityInUseData>(HardlightProject.HardlightEnumComparers.ActorAbilityTypeComparer);
            foreach (ActorAbilityType ability in abilities) data[ability] = new ActorAbilityInUseData { InUse = true };
            return data;
        }

        // An ability that was last used at a given fixed time and is no longer in use.
        private static Dictionary<ActorAbilityType, ActorAbilityInUseData> LastUsed(ActorAbilityType ability, float time)
        {
            var data = new Dictionary<ActorAbilityType, ActorAbilityInUseData>(HardlightProject.HardlightEnumComparers.ActorAbilityTypeComparer);
            data[ability] = new ActorAbilityInUseData { InUse = false, TimeLastUsedSeconds = time };
            return data;
        }

        private static CharacterAbilityTypeGroup Group(Owned owned, params ActorAbilityType[] abilities)
        {
            var group = owned.Track(ScriptableObject.CreateInstance<CharacterAbilityTypeGroup>());
            foreach (ActorAbilityType ability in abilities)
            {
                var definition = new CharacterAbilityTypeGroup.AbilityTypeDefinition();
                Set(definition, "m_type", ability);
                group.Abilities.Add(definition);
            }
            return group;
        }

        // TriggerCondition_AbilityInUse.DoEvaluate 0x5CCA00: without the group flag (+0x21) it asks
        // ActorAbilityUtilities.IsAbilityInUse about m_abilityType (+0x30); with it, about each
        // ability of m_abilityTypeGroup (+0x28) in order, stopping at the first one in use, and the
        // single ability is not read. The constructor 0x5CCB90 stores 0.2f at +0x34.
        [Test]
        public void AbilityInUseAsksAboutTheSingleAbilityOrAnyOfTheGroup()
        {
            Run((owned, scene) =>
            {
                var condition = Add<TriggerCondition_AbilityInUse>(owned);
                Assert.That(Get<float>(condition, "m_usedWithinSeconds"), Is.EqualTo(0.2f));
                ActorAbilityType first = ActorAbilityType.Boss_AttackArea;
                ActorAbilityType second = ActorAbilityType.Boss_AttackMelee;
                Set(condition, "m_abilityType", first);
                Set(condition, "m_abilityTypeGroup", Group(owned, second, first));

                Assert.That(condition.Evaluate(scene.Character), Is.False, "no ability data is stored");
                scene.Storage.SetValue(ActorFSMKeys.AbilitiesInUse, InUse(first));
                Assert.That(condition.Evaluate(scene.Character), Is.True, "the single ability is in use");
                scene.Storage.SetValue(ActorFSMKeys.AbilitiesInUse, InUse(second));
                Assert.That(condition.Evaluate(scene.Character), Is.False, "only the group's other ability is in use");

                Set(condition, "m_useAbilityGroup", true);
                Assert.That(condition.Evaluate(scene.Character), Is.True, "the group's first ability is in use");
                scene.Storage.SetValue(ActorFSMKeys.AbilitiesInUse, InUse(first));
                Assert.That(condition.Evaluate(scene.Character), Is.True, "the group's last ability is in use");
                scene.Storage.SetValue(ActorFSMKeys.AbilitiesInUse, InUse(ActorAbilityType.Boss_BodySlam));
                Assert.That(condition.Evaluate(scene.Character), Is.False, "an ability outside the group is not read");
                Set(condition, "m_abilityType", ActorAbilityType.Boss_BodySlam);
                Assert.That(condition.Evaluate(scene.Character), Is.False, "the single ability is ignored in group mode");
                Set(condition, "m_abilityTypeGroup", Group(owned));
                Assert.That(condition.Evaluate(scene.Character), Is.False, "an empty group is never in use");
                Set(condition, "m_invert", true);
                Assert.That(condition.Evaluate(scene.Character), Is.True);
            });
        }

        // 0x5CCABA loads each AbilityTypeDefinition.GracePeriod (+0x14) as the time argument in group
        // mode, while the single ability uses m_usedWithinSeconds (+0x34). The ability was last used
        // 0.5s before the character's fixed time (10), so IsAbilityInUse is true only for a grace of
        // at least 0.5: the group's own 1.0 passes where the component's 0.05 would not, and the
        // single ability fails on its 0.05. The loop stops at the first ability in use, so a null
        // entry after it is never read.
        [Test]
        public void AbilityInUseGroupUsesEachGracePeriodAndStopsAtTheFirstAbilityInUse()
        {
            Run((owned, scene) =>
            {
                Set(scene.Character, "m_totalFixedTime", 10f);
                var condition = Add<TriggerCondition_AbilityInUse>(owned);
                ActorAbilityType ability = ActorAbilityType.Boss_AttackArea;
                CharacterAbilityTypeGroup group = Group(owned, ability);
                Set(group.Abilities[0], "m_gracePeriod", 1.0f);
                Set(condition, "m_abilityType", ability);
                Set(condition, "m_abilityTypeGroup", group);
                Set(condition, "m_usedWithinSeconds", 0.05f);
                scene.Storage.SetValue(ActorFSMKeys.AbilitiesInUse, LastUsed(ability, 9.5f));

                Assert.That(condition.Evaluate(scene.Character), Is.False, "the single ability uses m_usedWithinSeconds");
                Set(condition, "m_useAbilityGroup", true);
                Assert.That(condition.Evaluate(scene.Character), Is.True, "the group uses the definition's grace period");
                Set(group.Abilities[0], "m_gracePeriod", 0.05f);
                Set(condition, "m_usedWithinSeconds", 1.0f);
                Assert.That(condition.Evaluate(scene.Character), Is.False, "the component's period is not used in group mode");

                Set(group.Abilities[0], "m_gracePeriod", 1.0f);
                group.Abilities.Add(null);
                Assert.That(condition.Evaluate(scene.Character), Is.True, "the entry after the match is never read");
                group.Abilities.Insert(0, null);
                Assert.Throws<NullReferenceException>(() => condition.Evaluate(scene.Character), "an entry before the match is read");
            });
        }

        // TriggerCondition_ScriptableObject.DoEvaluate 0x5CDCC0 calls the asset's DoEvaluate(Character)
        // and xors the asset's own m_invert (0x5CDCDD), then Evaluate xors this component's. The
        // asset's answer and both inversions make the eight rows below.
        [Test]
        public void ScriptableObjectConditionAppliesTheAssetsInversionBeforeItsOwn()
        {
            Run((owned, scene) =>
            {
                var asset = owned.Track(ScriptableObject.CreateInstance<TriggerConditionCachedProbe>());
                var condition = Add<TriggerCondition_ScriptableObject>(owned);
                Set(condition, "m_condition", asset);
                foreach (var row in new[]
                {
                    (answer: true, assetInvert: false, componentInvert: false, expected: true),
                    (answer: true, assetInvert: true, componentInvert: false, expected: false),
                    (answer: true, assetInvert: false, componentInvert: true, expected: false),
                    (answer: true, assetInvert: true, componentInvert: true, expected: true),
                    (answer: false, assetInvert: false, componentInvert: false, expected: false),
                    (answer: false, assetInvert: true, componentInvert: false, expected: true),
                    (answer: false, assetInvert: false, componentInvert: true, expected: true),
                    (answer: false, assetInvert: true, componentInvert: true, expected: false),
                })
                {
                    asset.CharacterResult = row.answer;
                    Set(asset, "m_invert", row.assetInvert);
                    Set(condition, "m_invert", row.componentInvert);
                    asset.LastCharacter = null;
                    Assert.That(condition.Evaluate(scene.Character), Is.EqualTo(row.expected), row.ToString());
                    Assert.That(asset.LastCharacter, Is.SameAs(scene.Character), row.ToString());
                    Assert.That(condition.Evaluate(), Is.EqualTo(row.expected), row + " through the current character");
                }
                Set(condition, "m_condition", null);
                Assert.Throws<NullReferenceException>(() => condition.Evaluate(scene.Character));
            });
        }
    }
}
