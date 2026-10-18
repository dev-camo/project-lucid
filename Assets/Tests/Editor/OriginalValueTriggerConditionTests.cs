using System;
using System.Collections;
using System.Linq;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.TriggerConditionTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // Restored Game.Runtime TriggerCondition_CharacterFSMValue`1 (TypeDef 0x02000750, read from its
    // <bool>, <int> and <float> instantiations 0xC81D40..0xC821F0) with the Bool, Float and Int
    // conditions (0x02000751..53), and TriggerCondition_Value`1 (0x02000758, 0xC82290..0xC8257A) with
    // the Bool, Float, Integer and Time conditions (0x02000759..5C, x86-64 0x5CDD40..0x5CE30D). The
    // conditions' Awake is not sent in Edit Mode and is called directly. The rows are the
    // ComparisonTable values (value above, equal to and below the threshold).
    public sealed class OriginalValueTriggerConditionTests
    {
        private static void AssertType(Type type, int typeFlags, Type baseType, int[] options,
            (string name, int flags)[] fields, (string name, int flags)[] methods)
        {
            AssertMembers(type, typeFlags, fields, methods);
            AssertIl2CppOptions(type, options);
            Assert.That(type.BaseType, Is.EqualTo(baseType), type.FullName);
        }

        private static T Add<T>(Owned owned) where T : Component => owned.Host("Lucid value condition", false).AddComponent<T>();

        private static void AssertUnconstrained(Type generic)
        {
            Type parameter = generic.GetGenericArguments().Single();
            Assert.That((int)parameter.GenericParameterAttributes, Is.Zero, generic.Name);
            Assert.That(parameter.GetGenericParameterConstraints(), Is.Empty, generic.Name);
        }

        // Metadata of the nine TypeDefs and the generic parameter of both bases.
        [Test]
        public void DeclarationsKeepTheOriginalFlagsFieldsAndMethods()
        {
            var fsmFields = new[] { ("m_valueName", 0x0001), ("m_valueThreshold", 0x0004), ("m_comparisonType", 0x0004), ("m_fsmVarName", 0x0001) };
            AssertType(typeof(TriggerCondition_CharacterFSMValue<>), 0x00100081, typeof(TriggerCondition), new[] { 2, 1 }, fsmFields,
                new[] { ("Awake", 0x0081), ("GetValue", 0x0084), ("GetValueDefault", 0x05c4), (".ctor", 0x1884) });
            AssertUnconstrained(typeof(TriggerCondition_CharacterFSMValue<>));
            var fsmMethods = new[] { ("DoEvaluate", 0x00c4), ("GetValueDefault", 0x00c4), (".ctor", 0x1886) };
            AssertType(typeof(TriggerCondition_CharacterFSMValueBool), 0x00100001, typeof(TriggerCondition_CharacterFSMValue<bool>), new[] { 2, 1 }, new (string, int)[0], fsmMethods);
            AssertType(typeof(TriggerCondition_CharacterFSMValueFloat), 0x00100001, typeof(TriggerCondition_CharacterFSMValue<float>), new[] { 2, 1 }, new (string, int)[0], fsmMethods);
            AssertType(typeof(TriggerCondition_CharacterFSMValueInt), 0x00100001, typeof(TriggerCondition_CharacterFSMValue<int>), new[] { 1, 2 }, new (string, int)[0], fsmMethods);

            AssertType(typeof(TriggerCondition_Value<>), 0x00100081, typeof(TriggerCondition), new[] { 1, 2 },
                new[] { ("m_valueStart", 0x0001), ("m_valueThreshold", 0x0004), ("m_comparisonType", 0x0004), ("m_value", 0x0004) },
                new[] { ("Awake", 0x0081), ("Reset", 0x0086), ("IncrementValue", 0x05c6), (".ctor", 0x1884) });
            AssertUnconstrained(typeof(TriggerCondition_Value<>));
            var valueMethods = new[] { ("DoEvaluate", 0x00c4), ("IncrementValue", 0x00c6), (".ctor", 0x1886) };
            AssertType(typeof(TriggerCondition_ValueBool), 0x00100001, typeof(TriggerCondition_Value<bool>), new[] { 2, 1 }, new (string, int)[0], valueMethods);
            AssertType(typeof(TriggerCondition_ValueFloat), 0x00100001, typeof(TriggerCondition_Value<float>), new[] { 1, 2 }, new (string, int)[0], valueMethods);
            AssertType(typeof(TriggerCondition_ValueInteger), 0x00100001, typeof(TriggerCondition_Value<int>), new[] { 1, 2 }, new (string, int)[0], valueMethods);
            AssertType(typeof(TriggerCondition_ValueTime), 0x00100001, typeof(TriggerCondition_ValueFloat), new[] { 2, 1 },
                new[] { ("timeCategory", 0x0001), ("s_timeManagerRef", 0x0031), ("m_waitForFixedUpdate", 0x0021) },
                new[] { ("OnEnable", 0x0084), ("OnDisable", 0x0084), ("UpdateCoroutine", 0x0081), (".ctor", 0x1886), (".cctor", 0x1891) });
            Type iterator = typeof(TriggerCondition_ValueTime).GetNestedType("<UpdateCoroutine>d__5", Declared);
            Assert.That(iterator, Is.Not.Null, "Roslyn's iterator ordinal 5 follows the three fields and two hooks");
            Assert.That((int)iterator.Attributes, Is.EqualTo(0x00100103));
        }

        // The constructors 0xC81DE0 (CharacterFSMValue) and 0xC822B0 (Value) store ComparisonType.Equal
        // (0x260028B8) in the comparison type; the key is built from the value name by Awake 0xC81D40.
        // Int's default is -1 (0x5CD790), Float's 0 and Bool's false, used when nothing is stored; a
        // stored value is compared as the first operand, the threshold as the second.
        [Test]
        public void CharacterFsmIntComparesTheStoredValueOrMinusOneWithTheThreshold()
        {
            Run((owned, scene) =>
            {
                var condition = Add<TriggerCondition_CharacterFSMValueInt>(owned);
                Assert.That(Get<ComparisonType>(condition, "m_comparisonType"), Is.EqualTo(ComparisonType.Equal));
                Set(condition, "m_valueName", "LucidFsmInt");
                Call(condition, "Awake");
                var key = Get<GraphStorageKey>(condition, "m_fsmVarName");
                Assert.That(key, Is.EqualTo(new GraphStorageKey("LucidFsmInt")));

                Set(condition, "m_valueThreshold", -1);
                Assert.That(condition.Evaluate(scene.Character), Is.True, "nothing stored reads as -1");
                Set(condition, "m_valueThreshold", 0);
                Assert.That(condition.Evaluate(scene.Character), Is.False, "the default is -1, not 0");

                Set(condition, "m_valueThreshold", 3);
                foreach (var row in ComparisonTable)
                {
                    Set(condition, "m_comparisonType", row.type);
                    foreach (var sample in new[] { (stored: 5, expected: row.above), (stored: 3, expected: row.equal), (stored: 1, expected: row.below) })
                    {
                        scene.Storage.SetValue(key, sample.stored);
                        Assert.That(condition.Evaluate(scene.Character), Is.EqualTo(sample.expected), row.type + " " + sample.stored);
                    }
                }
            });
        }

        [Test]
        public void CharacterFsmFloatComparesTheStoredValueOrZeroWithTheThreshold()
        {
            Run((owned, scene) =>
            {
                var condition = Add<TriggerCondition_CharacterFSMValueFloat>(owned);
                Assert.That(Get<ComparisonType>(condition, "m_comparisonType"), Is.EqualTo(ComparisonType.Equal));
                Set(condition, "m_valueName", "LucidFsmFloat");
                Call(condition, "Awake");
                var key = Get<GraphStorageKey>(condition, "m_fsmVarName");

                Set(condition, "m_valueThreshold", 0f);
                Assert.That(condition.Evaluate(scene.Character), Is.True, "nothing stored reads as 0");
                Set(condition, "m_valueThreshold", -1f);
                Assert.That(condition.Evaluate(scene.Character), Is.False, "the float default is 0, not -1");

                Set(condition, "m_valueThreshold", 3f);
                foreach (var row in ComparisonTable)
                {
                    Set(condition, "m_comparisonType", row.type);
                    foreach (var sample in new[] { (stored: 5f, expected: row.above), (stored: 3f, expected: row.equal), (stored: 1f, expected: row.below) })
                    {
                        scene.Storage.SetValue(key, sample.stored);
                        Assert.That(condition.Evaluate(scene.Character), Is.EqualTo(sample.expected), row.type + " " + sample.stored);
                    }
                }
            });
        }

        // The bool conditions compare through the integer overload (0x6AE870) with false as 0 and true
        // as 1, so the ordered rows tell which way a boolean maps: false < true.
        [Test]
        public void CharacterFsmBoolComparesTheStoredBooleanOrFalseAsZeroAndOne()
        {
            Run((owned, scene) =>
            {
                var condition = Add<TriggerCondition_CharacterFSMValueBool>(owned);
                Assert.That(Get<ComparisonType>(condition, "m_comparisonType"), Is.EqualTo(ComparisonType.Equal));
                Set(condition, "m_valueName", "LucidFsmBool");
                Call(condition, "Awake");
                var key = Get<GraphStorageKey>(condition, "m_fsmVarName");

                Assert.That(condition.Evaluate(scene.Character), Is.True, "nothing stored reads as false, equal to the false threshold");
                Set(condition, "m_valueThreshold", true);
                Assert.That(condition.Evaluate(scene.Character), Is.False);

                foreach (var row in new[]
                {
                    (stored: true, threshold: true, type: ComparisonType.Equal, expected: true),
                    (stored: true, threshold: false, type: ComparisonType.Equal, expected: false),
                    (stored: true, threshold: false, type: ComparisonType.NotEqual, expected: true),
                    (stored: true, threshold: false, type: ComparisonType.GreaterThan, expected: true),
                    (stored: false, threshold: true, type: ComparisonType.GreaterThan, expected: false),
                    (stored: false, threshold: true, type: ComparisonType.LessThan, expected: true),
                    (stored: true, threshold: false, type: ComparisonType.LessThanEqual, expected: false),
                    (stored: false, threshold: false, type: ComparisonType.GreaterThanEqual, expected: true),
                })
                {
                    scene.Storage.SetValue(key, row.stored);
                    Set(condition, "m_valueThreshold", row.threshold);
                    Set(condition, "m_comparisonType", row.type);
                    Assert.That(condition.Evaluate(scene.Character), Is.EqualTo(row.expected), row.ToString());
                }
            });
        }

        // Awake 0xC82310 and Reset 0xC82320 copy m_valueStart into m_value; IncrementValue 0x5CDE40
        // adds the delta. DoEvaluate 0x5CDE20 compares m_value first and m_valueThreshold second.
        [Test]
        public void IntegerValueStartsFromItsStartValueAndAccumulates()
        {
            Run((owned, scene) =>
            {
                var condition = Add<TriggerCondition_ValueInteger>(owned);
                Assert.That(Get<ComparisonType>(condition, "m_comparisonType"), Is.EqualTo(ComparisonType.Equal));
                Set(condition, "m_valueStart", 5);
                Assert.That(Get<int>(condition, "m_value"), Is.Zero, "nothing copies the start before Awake");
                Call(condition, "Awake");
                Assert.That(Get<int>(condition, "m_value"), Is.EqualTo(5));
                condition.IncrementValue(3);
                condition.IncrementValue(-10);
                Assert.That(Get<int>(condition, "m_value"), Is.EqualTo(-2));
                condition.Reset();
                Assert.That(Get<int>(condition, "m_value"), Is.EqualTo(5));

                Set(condition, "m_valueThreshold", 5);
                foreach (var row in ComparisonTable)
                {
                    Set(condition, "m_comparisonType", row.type);
                    foreach (var sample in new[] { (value: 7, expected: row.above), (value: 5, expected: row.equal), (value: 3, expected: row.below) })
                    {
                        Set(condition, "m_value", sample.value);
                        Assert.That(condition.Evaluate(scene.Character), Is.EqualTo(sample.expected), row.type + " " + sample.value);
                    }
                }
            });
        }

        // IncrementValue 0x5CDDD0 adds the delta to the float value (+0x30); DoEvaluate 0x5CDDB0 passes
        // the value first and the threshold second to the float Evaluate.
        [Test]
        public void FloatValueStartsFromItsStartValueAndAccumulates()
        {
            Run((owned, scene) =>
            {
                var condition = Add<TriggerCondition_ValueFloat>(owned);
                Assert.That(Get<ComparisonType>(condition, "m_comparisonType"), Is.EqualTo(ComparisonType.Equal));
                Set(condition, "m_valueStart", 1.5f);
                Call(condition, "Awake");
                Assert.That(Get<float>(condition, "m_value"), Is.EqualTo(1.5f));
                condition.IncrementValue(0.25f);
                condition.IncrementValue(2f);
                Assert.That(Get<float>(condition, "m_value"), Is.EqualTo(3.75f));
                condition.Reset();
                Assert.That(Get<float>(condition, "m_value"), Is.EqualTo(1.5f));

                Set(condition, "m_valueThreshold", 5f);
                foreach (var row in ComparisonTable)
                {
                    Set(condition, "m_comparisonType", row.type);
                    foreach (var sample in new[] { (value: 7f, expected: row.above), (value: 5f, expected: row.equal), (value: 3f, expected: row.below) })
                    {
                        Set(condition, "m_value", sample.value);
                        Assert.That(condition.Evaluate(scene.Character), Is.EqualTo(sample.expected), row.type + " " + sample.value);
                    }
                }
            });
        }

        // IncrementValue 0x5CDD60 stores its argument: a boolean value is replaced, not accumulated.
        // DoEvaluate 0x5CDD40 compares the value as 0 or 1 first and the threshold second.
        [Test]
        public void BooleanValueIsReplacedByIncrementValue()
        {
            Run((owned, scene) =>
            {
                var condition = Add<TriggerCondition_ValueBool>(owned);
                Assert.That(Get<ComparisonType>(condition, "m_comparisonType"), Is.EqualTo(ComparisonType.Equal));
                Set(condition, "m_valueStart", true);
                Call(condition, "Awake");
                Assert.That(Get<bool>(condition, "m_value"), Is.True);
                condition.IncrementValue(false);
                Assert.That(Get<bool>(condition, "m_value"), Is.False);
                condition.IncrementValue(true);
                Assert.That(Get<bool>(condition, "m_value"), Is.True);
                condition.IncrementValue(true);
                Assert.That(Get<bool>(condition, "m_value"), Is.True, "a second true does not toggle it");
                condition.Reset();
                Assert.That(Get<bool>(condition, "m_value"), Is.True);

                foreach (var row in new[]
                {
                    (value: true, threshold: true, type: ComparisonType.Equal, expected: true),
                    (value: true, threshold: false, type: ComparisonType.Equal, expected: false),
                    (value: true, threshold: false, type: ComparisonType.GreaterThan, expected: true),
                    (value: false, threshold: true, type: ComparisonType.GreaterThan, expected: false),
                    (value: false, threshold: true, type: ComparisonType.LessThan, expected: true),
                    (value: false, threshold: true, type: ComparisonType.NotEqual, expected: true),
                })
                {
                    Set(condition, "m_value", row.value);
                    Set(condition, "m_valueThreshold", row.threshold);
                    Set(condition, "m_comparisonType", row.type);
                    Assert.That(condition.Evaluate(scene.Character), Is.EqualTo(row.expected), row.ToString());
                }
            });
        }

        // TriggerCondition_ValueTime: the constructor 0x5CE000 stores the PlayerPhysics category
        // (0x67B16BA4) and a new WaitForFixedUpdate; the iterator 0x5CE130 ends once the TimeManager
        // reference is null (IsNull, 0x5CE1BA) before it adds any time. A present TimeManager would
        // need the TimeCategoryLookup configuration and is not set up here.
        [Test]
        public void TimeValueKeepsItsCategoryAndStopsWithoutATimeManager()
        {
            // Reading the static reference may run the type initialiser, which creates the TimeManager row.
            Owned.Run(owned =>
            {
                owned.LeaseRow<TimeManager>();
                var first = Add<TriggerCondition_ValueTime>(owned);
                var second = Add<TriggerCondition_ValueTime>(owned);
                Assert.That(Get<TimeCategory>(first, "timeCategory"), Is.EqualTo(TimeCategory.PlayerPhysics));
                Assert.That(Get<WaitForFixedUpdate>(first, "m_waitForFixedUpdate"), Is.Not.Null.And.Not.SameAs(Get<WaitForFixedUpdate>(second, "m_waitForFixedUpdate")));

                Set(first, "m_value", 1f);
                var update = (IEnumerator)Call(first, "UpdateCoroutine");
                Assert.That(update.MoveNext(), Is.False);
                Assert.That(Get<float>(first, "m_value"), Is.EqualTo(1f), "no time is added");
                Assert.That(update.MoveNext(), Is.False, "a finished iterator stays finished");
            });
        }
    }
}
