using System;
using System.Collections.Generic;
using System.Globalization;
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
    // The four character transitions of this cluster: the controller axis check, the controller movement
    // magnitude, the burst attack exit and the form check. Expected values come from the 1.10.1 x86-64 bodies
    // cited per test; each transition is built from its JSON through the node factory and updated against a
    // real Character on an owned inactive host.
    public sealed class CharacterControllerFormTransitionTests
    {
        private static string Name() => "Lucid-transition-" + Guid.NewGuid().ToString("N");

        private static IFSMTransition Build(string node, string json) =>
            FSMClassFactory.ConstructTransition(new FiniteStateMachine(Name(), skipAddToManager: true), node, new FSMIdentifier(Name()), json);

        private static bool Update(IFSMTransition transition, Character character) =>
            transition.Update(character, new FSMUpdateContext(0.02f, FSMUpdateType.FixedUpdate));

        private static void WithCharacter(Action<Owned, CharacterGravityVectorProbe> body) => Owned.Run(owned =>
        {
            owned.LeaseRow<GameplayIslandManager>();
            body(owned, NewCharacter(owned));
        });

        private static string Comparison(ComparisonType type) => ((int)type).ToString();

        // ---- CharacterTransition_ControllerAxisCheck (020003d0) ----

        private static string AxisJson(ComparisonType x, float xValue, bool absX, ComparisonType y, float yValue, bool absY, bool raw) =>
            "{\"XAxisValue\":" + xValue.ToString("R", CultureInfo.InvariantCulture) + ",\"XAxisComparisonType\":" + Comparison(x)
            + ",\"AbsoluteXValue\":" + (absX ? "true" : "false")
            + ",\"YAxisValue\":" + yValue.ToString("R", CultureInfo.InvariantCulture) + ",\"YAxisComparisonType\":" + Comparison(y)
            + ",\"AbsoluteYValue\":" + (absY ? "true" : "false") + ",\"UseRawInput\":" + (raw ? "true" : "false") + "}";

        private static void SetInput(Character character, Vector2 applied, Vector2 raw)
        {
            SetProperty(character, "ControllerMovement", applied);
            SetProperty(character, "RawControllerMovement", raw);
        }

        // 0x78FDB0: the transition fires when the base fires, else when both axis comparisons hold. Each axis
        // value is the controller component (absolute when its flag is set, 0x78FE25 and 0x78FE3B) against its
        // threshold through ComparisonTypeExtensions.Evaluate (value first, threshold second).
        [Test]
        public void BothAxesMustSatisfyTheirComparisons()
        {
            WithCharacter((owned, character) =>
            {
                SetInput(character, new Vector2(0.5f, -0.25f), new Vector2(0.1f, 0.9f));
                Func<ComparisonType, float, bool, ComparisonType, float, bool, bool> check = (x, xValue, absX, y, yValue, absY) =>
                    Update(Build(nameof(CharacterTransition_ControllerAxisCheck), AxisJson(x, xValue, absX, y, yValue, absY, false)), character);

                Assert.That(check(ComparisonType.GreaterThan, 0.4f, false, ComparisonType.GreaterThan, 0.2f, true), Is.True, "0.5 > 0.4 and |-0.25| > 0.2");
                Assert.That(check(ComparisonType.GreaterThan, 0.4f, false, ComparisonType.GreaterThan, 0.2f, false), Is.False, "-0.25 > 0.2 fails without the absolute value");
                Assert.That(check(ComparisonType.GreaterThan, 0.6f, false, ComparisonType.GreaterThan, 0.2f, true), Is.False, "0.5 > 0.6 fails");
                Assert.That(check(ComparisonType.LessThan, 0f, false, ComparisonType.LessThan, 0f, false), Is.False, "0.5 < 0 fails");
                Assert.That(check(ComparisonType.GreaterThanEqual, 0.5f, false, ComparisonType.LessThanEqual, -0.25f, false), Is.True, "both inclusive limits are met exactly");
                Assert.That(check(ComparisonType.None, 99f, false, ComparisonType.None, 99f, false), Is.True, "None always holds");
            });
        }

        [Test]
        public void TheAbsoluteFlagsApplyToTheirOwnAxis()
        {
            WithCharacter((owned, character) =>
            {
                SetInput(character, new Vector2(-0.5f, -0.25f), Vector2.zero);
                bool Check(bool absX, bool absY) => Update(Build(nameof(CharacterTransition_ControllerAxisCheck),
                    AxisJson(ComparisonType.GreaterThan, 0.4f, absX, ComparisonType.GreaterThan, 0.2f, absY, false)), character);
                Assert.That(Check(true, true), Is.True);
                Assert.That(Check(false, true), Is.False, "x stays -0.5");
                Assert.That(Check(true, false), Is.False, "y stays -0.25");
                Assert.That(Check(false, false), Is.False);
            });
        }

        // 0x78FE19 selects the raw controller movement (+0x290) when the flag is set and the applied one (+0x2A8) otherwise.
        [Test]
        public void TheRawFlagSelectsTheRawControllerMovement()
        {
            WithCharacter((owned, character) =>
            {
                SetInput(character, new Vector2(0.5f, 0.5f), new Vector2(0.1f, 0.1f));
                string json(bool raw) => AxisJson(ComparisonType.GreaterThan, 0.4f, false, ComparisonType.GreaterThan, 0.4f, false, raw);
                Assert.That(Update(Build(nameof(CharacterTransition_ControllerAxisCheck), json(false)), character), Is.True);
                Assert.That(Update(Build(nameof(CharacterTransition_ControllerAxisCheck), json(true)), character), Is.False);
            });
        }

        // The two comparisons are combined with a non-short-circuit AND (0x78FE71 and al, r14b): the y comparison is
        // evaluated even when x already failed, so an unsupported comparison type on y still faults.
        [Test]
        public void TheYComparisonIsEvaluatedEvenAfterTheXComparisonFails()
        {
            WithCharacter((owned, character) =>
            {
                SetInput(character, new Vector2(0.5f, 0.5f), Vector2.zero);
                IFSMTransition transition = Build(nameof(CharacterTransition_ControllerAxisCheck),
                    AxisJson(ComparisonType.GreaterThan, 10f, false, (ComparisonType)99, 0f, false, false));
                Assert.That(() => Update(transition, character), Throws.Exception);
                IFSMTransition ok = Build(nameof(CharacterTransition_ControllerAxisCheck),
                    AxisJson(ComparisonType.GreaterThan, 10f, false, ComparisonType.GreaterThan, 0f, false, false));
                Assert.That(Update(ok, character), Is.False);
            });
        }

        [Test]
        public void ANaNInputFailsAnOrderedComparison()
        {
            WithCharacter((owned, character) =>
            {
                SetInput(character, new Vector2(float.NaN, 0.5f), Vector2.zero);
                Assert.That(Update(Build(nameof(CharacterTransition_ControllerAxisCheck),
                    AxisJson(ComparisonType.GreaterThan, 0f, false, ComparisonType.GreaterThan, 0f, false, false)), character), Is.False);
            });
        }

        // ---- CharacterTransition_ControllerMovementMagnitude (020003d2) ----

        private static string MagnitudeJson(ComparisonType type, float threshold, bool raw) =>
            "{\"MagnitudeThreshold\":" + threshold.ToString("R", CultureInfo.InvariantCulture) + ",\"ComparisonType\":" + Comparison(type)
            + ",\"UseRawInput\":" + (raw ? "true" : "false") + "}";

        // 0x78FF80: the raw magnitude (+0x298) when the flag is set, the applied one (+0x2B0) otherwise, then
        // Evaluate(magnitude, threshold) tail-called with the comparison type.
        [Test]
        public void MagnitudeIsComparedWithTheSelectedMagnitude()
        {
            WithCharacter((owned, character) =>
            {
                SetProperty(character, "RawControllerMovementMagnitude", 0.875f);
                SetProperty(character, "ControllerMovementMagnitude", 0.125f);
                bool Check(ComparisonType type, float threshold, bool raw) => Update(Build(nameof(CharacterTransition_ControllerMovementMagnitude), MagnitudeJson(type, threshold, raw)), character);

                Assert.That(Check(ComparisonType.GreaterThan, 0.5f, true), Is.True);
                Assert.That(Check(ComparisonType.GreaterThan, 0.5f, false), Is.False);
                Assert.That(Check(ComparisonType.LessThan, 0.5f, false), Is.True, "the value is the left operand");
                Assert.That(Check(ComparisonType.GreaterThanEqual, 0.125f, false), Is.True, "equal passes an inclusive comparison");
                Assert.That(Check(ComparisonType.GreaterThan, 0.125f, false), Is.False);
                Assert.That(Check(ComparisonType.None, 100f, false), Is.True);
            });
        }

        [Test]
        public void ANaNMagnitudeFailsOrderedComparisons()
        {
            WithCharacter((owned, character) =>
            {
                SetProperty(character, "ControllerMovementMagnitude", float.NaN);
                foreach (ComparisonType type in new[] { ComparisonType.GreaterThan, ComparisonType.GreaterThanEqual, ComparisonType.LessThan, ComparisonType.LessThanEqual })
                    Assert.That(Update(Build(nameof(CharacterTransition_ControllerMovementMagnitude), MagnitudeJson(type, 0f, false)), character), Is.False, type.ToString());
            });
        }

        // ---- CharacterTransition_ExitBurstAttack (020003d4) ----

        private sealed class ProbeAbility : ActorAbility
        {
            private readonly AbilityDefinition m_definition;
            public ProbeAbility(AbilityDefinition definition) { m_definition = definition; }
            public override AbilityDefinition DefinitionBase => m_definition;
        }

        private sealed class OtherDefinition : AbilityDefinition
        {
            public override Type ScriptType => typeof(ProbeAbility);
        }

        private static string ExitJson => "{\"AbilityType\":" + (int)ActorAbilityType.Character_BurstAttackGround + "}";

        private static void Register(Character character, ActorAbility ability) =>
            ((Dictionary<ActorAbilityType, ActorAbility>)Get<object>(character, "m_abilities"))[ActorAbilityType.Character_BurstAttackGround] = ability;

        // 0x790100: no ability of the configured type, or one whose definition is not a burst attack definition, is false.
        // Otherwise the stored burst duration timer must have reached the definition's duration (+0xDC): ucomiss /
        // setae, so equality passes and an unordered NaN fails.
        [Test]
        public void ExitFiresWhenTheBurstTimerHasReachedTheDuration()
        {
            WithCharacter((owned, character) =>
            {
                IFSMTransition transition = Build(nameof(CharacterTransition_ExitBurstAttack), ExitJson);
                Assert.That(Update(transition, character), Is.False, "no ability");

                Register(character, new ProbeAbility(owned.Track(ScriptableObject.CreateInstance<OtherDefinition>())));
                character.Storage.SetValue(ActorFSMKeys.BurstAttackDurationTimer, 99f);
                Assert.That(Update(transition, character), Is.False, "a definition of another kind");

                var definition = owned.Track(ScriptableObject.CreateInstance<CharacterAbilityDefinition_BurstAttackGround>());
                definition.DurationSeconds = 2f;
                Register(character, new ProbeAbility(definition));
                foreach ((float timer, bool expected) in new[] { (0f, false), (1.9f, false), (2f, true), (2.1f, true), (float.NaN, false) })
                {
                    character.Storage.SetValue(ActorFSMKeys.BurstAttackDurationTimer, timer);
                    Assert.That(Update(transition, character), Is.EqualTo(expected), "timer " + timer);
                }

                character.Storage.RemoveValue<float>(ActorFSMKeys.BurstAttackDurationTimer);
                definition.DurationSeconds = 0f;
                Assert.That(Update(transition, character), Is.True, "a missing timer reads zero, which reaches a zero duration");
                Assert.That(character.Storage.TryGetValue(ActorFSMKeys.BurstAttackDurationTimer, out float stored) && stored == 0f, Is.True, "and stores its default");
            });
        }

        // ---- CharacterTransition_Form (020003d6) ----

        private static string FormJson(ActorFormType type, bool inverse) =>
            "{\"ActiveFormType\":" + (int)type + ",\"Inverse\":" + (inverse ? "true" : "false") + "}";

        // 0x7904E0: the character's current form type against the configured one; the inverse flag (+0x20) flips it.
        [Test]
        public void TheFormMatchesOrDiffersAccordingToTheInverseFlag()
        {
            WithCharacter((owned, character) =>
            {
                var handler = owned.Host("Lucid form handler", false).AddComponent<ActorFormHandler>();
                Set(character, "m_formHandler", handler);
                foreach (ActorFormType current in new[] { ActorFormType.Ball, ActorFormType.Default })
                {
                    SetProperty(handler, "CurrentFormType", current);
                    foreach (ActorFormType configured in new[] { ActorFormType.Ball, ActorFormType.Default, ActorFormType.None })
                    {
                        Assert.That(Update(Build(nameof(CharacterTransition_Form), FormJson(configured, false)), character), Is.EqualTo(current == configured),
                            "form " + current + " against " + configured);
                        Assert.That(Update(Build(nameof(CharacterTransition_Form), FormJson(configured, true)), character), Is.EqualTo(current != configured),
                            "form " + current + " against inverse " + configured);
                    }
                }
            });
        }

        // ---- Registration and graph authoring metadata ----

        private static void AssertNode(Type node, string defaultName, string menu)
        {
            Assert.That(node.GetCustomAttribute<GraphNodeDefaultNameAttribute>(false).DefaultName, Is.EqualTo(defaultName), node.Name);
            Assert.That(node.GetCustomAttribute<GraphNodeMenuFormatAttribute>(false).Format, Is.EqualTo(menu), node.Name);
        }

        // CAD blobs of the four transitions and the two states: default graph names and the Character menu.
        [Test]
        public void NodesCarryTheirOriginalGraphAuthoringAttributes()
        {
            AssertNode(typeof(CharacterTransition_ControllerAxisCheck), "ControllerAxisCheck", "Character/{0}");
            AssertNode(typeof(CharacterTransition_ControllerMovementMagnitude), "ControllerMovementMagnitudeCheck", "Character/{0}");
            AssertNode(typeof(CharacterTransition_ExitBurstAttack), "ExitBurstAttack", "Character/{0}");
            AssertNode(typeof(CharacterTransition_Form), "FormIsActive", "Character/{0}");
            AssertNode(typeof(CharacterState_HalfPipeAir), "HalfPipeAir", "Character/{0}");
        }

        private static void AssertRegistered(IReadOnlyDictionary<string, JSONFactoryClass> lookup, Type node, string argsName, Type argsOwner)
        {
            Assert.That(lookup.TryGetValue(node.Name, out JSONFactoryClass factory), Is.True, node.Name);
            Assert.That(factory.ClassType, Is.EqualTo(node), node.Name);
            Assert.That(factory.ConstructInstanceMethod.DeclaringType, Is.EqualTo(node), node.Name);
            Assert.That(factory.JSONCtorArgsType.Name, Is.EqualTo(argsName), node.Name);
            Assert.That(factory.JSONCtorArgsType.DeclaringType, Is.EqualTo(argsOwner), node.Name);
        }

        // The class factory finds each node by its static ConstructInstance and its argument type as the nested type
        // named JSONCtorArgs (or carrying the FSM argument attribute); the burst exit shares the ability argument type
        // of Transition_Ability and the half pipe air state has its own attributed argument class.
        [Test]
        public void ClassFactoryRegistersTheNodesWithTheirArgumentTypes()
        {
            AssertRegistered(FSMClassFactory.Transitions, typeof(CharacterTransition_ControllerAxisCheck), "JSONCtorArgs", typeof(CharacterTransition_ControllerAxisCheck));
            AssertRegistered(FSMClassFactory.Transitions, typeof(CharacterTransition_ControllerMovementMagnitude), "JSONCtorArgs", typeof(CharacterTransition_ControllerMovementMagnitude));
            AssertRegistered(FSMClassFactory.Transitions, typeof(CharacterTransition_ExitBurstAttack), "AbilityJSONCtorArgs", typeof(Transition_Ability));
            AssertRegistered(FSMClassFactory.Transitions, typeof(CharacterTransition_Form), "JSONCtorArgs", typeof(CharacterTransition_Form));
            AssertRegistered(FSMClassFactory.States, typeof(CharacterState_HalfPipeAir), "HalfPipeAirJSONCtorArgs", typeof(CharacterState_HalfPipeAir));
        }

        // The transitions' constructors store each argument field in declaration order, and their JSON argument classes are
        // serialisable (the axis and magnitude classes carry no FSM argument attribute, the form class does).
        [Test]
        public void DeclarationShapeTransitionsKeepTheirFieldsAndArgumentClasses()
        {
            const BindingFlags declared = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            Assert.That(typeof(CharacterTransition_ControllerAxisCheck).GetFields(declared).OrderBy(f => f.MetadataToken).Select(f => f.Name), Is.EqualTo(new[]
            {
                "m_xAxisValue", "m_xAxisComparisonType", "m_absoluteXValue", "m_yAxisValue", "m_yAxisComparisonType", "m_absoluteYValue", "m_useRawInput",
            }));
            Assert.That(typeof(CharacterTransition_ControllerMovementMagnitude).GetFields(declared).OrderBy(f => f.MetadataToken).Select(f => f.Name),
                Is.EqualTo(new[] { "m_magnitudeThreshold", "m_comparisonType", "m_useRawInput" }));
            Assert.That(typeof(CharacterTransition_Form).GetFields(declared).OrderBy(f => f.MetadataToken).Select(f => f.Name), Is.EqualTo(new[] { "m_activeFormType", "m_inverse" }));
            Assert.That(typeof(CharacterTransition_ExitBurstAttack).BaseType, Is.EqualTo(typeof(Transition_Ability)));
            foreach ((Type node, bool attributed) in new[]
            {
                (typeof(CharacterTransition_ControllerAxisCheck), false), (typeof(CharacterTransition_ControllerMovementMagnitude), false), (typeof(CharacterTransition_Form), true),
            })
            {
                Type args = node.GetNestedType("JSONCtorArgs", BindingFlags.NonPublic);
                Assert.That(args.IsNestedFamily && args.IsSerializable, Is.True, node.Name);
                Assert.That(args.IsDefined(typeof(FiniteStateMachineJsonCtorArgsAttribute), false), Is.EqualTo(attributed), node.Name);
            }
        }
    }
}
