using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Hardlight.UI.Binding;
using Hardlight.UI.Binding.Nodes;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
using Shade = OriginalLogicMathNodeCarrier.Shade;

namespace ProjectLucid.Tests
{
    // HLBinders.Runtime logic, math, list and time nodes (0x060000cd..0x0600015e) against
    // the x86-64 retail bodies. Nodes are owned components on an inactive object, so no
    // Awake, OnEnable or listener registration runs; Refresh is called directly on the
    // Editor (uncached) binding path. Expected values restate the native predicates.
    public sealed class OriginalLogicMathNodeTests
    {
        private GameObject owner;
        private OriginalLogicMathNodeCarrier carrier;

        [SetUp]
        public void CreateOwnedObjects()
        {
            Assert.IsFalse(Application.isPlaying, "Owned cases require the Editor (uncached) binding path");
            owner = new GameObject("Owned inactive logic and math nodes");
            owner.SetActive(false);
            carrier = ScriptableObject.CreateInstance<OriginalLogicMathNodeCarrier>();
        }

        [TearDown]
        public void DestroyOwnedObjects()
        {
            try
            {
                if (owner != null) Object.DestroyImmediate(owner);
            }
            finally
            {
                if (carrier != null) Object.DestroyImmediate(carrier);
                owner = null;
                carrier = null;
            }
        }

        private T Node<T>() where T : BinderNode
        {
            return owner.AddComponent<T>();
        }

        private void Bind(BindReference reference, string member)
        {
            reference.SetTarget(carrier, member);
        }

        private void AssertReads(params string[] expected)
        {
            Assert.That(carrier.Reads, Is.EqualTo(expected));
            carrier.Reads.Clear();
        }

        // Each SetUpBindReferenceFields body (for example 0x19FCCB0, 0x19FEE20, 0x1A078C0)
        // builds a new list from the input fields in declaration order; LogicalAnyOf stops
        // at Sixth (+0x80) and leaves Seventh (+0x88) out. The constructors give every
        // input its own BindReference and every output its own Bindable.
        [Test]
        public void EachNodeListsItsOriginalInputsAndConstructsDistinctReferences()
        {
            var expected = new Dictionary<Type, string[]>
            {
                { typeof(Equality), new[] { "First", "Second" } },
                { typeof(EnumIntEquality), new[] { "First", "Second" } },
                { typeof(IsNull), new[] { "Input" } },
                { typeof(IsStringNullOrEmpty), new[] { "Input" } },
                { typeof(LogicalAnd), new[] { "First", "Second" } },
                { typeof(LogicalAndMultiple), new[] { "First", "Second", "Third", "Fourth", "Fifth", "Sixth" } },
                { typeof(LogicalAnyOf), new[] { "First", "Second", "Third", "Fourth", "Fifth", "Sixth" } },
                { typeof(LogicalAnyOf3), new[] { "First", "Second", "Third" } },
                { typeof(LogicalGreaterOrEqual), new[] { "First", "Second" } },
                { typeof(LogicalGreaterThan), new[] { "First", "Second" } },
                { typeof(LogicalInSet), new[] { "Item", "SetItem1", "SetItem2", "SetItem3", "SetItem4" } },
                { typeof(LogicalLessThan), new[] { "First", "Second" } },
                { typeof(LogicalNot), new[] { "Input" } },
                { typeof(LogicalOr), new[] { "First", "Second" } },
                { typeof(MathAbs), new[] { "Input" } },
                { typeof(MathAdd), new[] { "First", "Second" } },
                { typeof(MathAddFloat), new[] { "First", "Second" } },
                { typeof(MathAddLong), new[] { "First", "Second" } },
                { typeof(MathDivide), new[] { "Numerator", "Denominator" } },
                { typeof(MathDivideLong), new[] { "Numerator", "Denominator" } },
                { typeof(MathFloor), new[] { "First" } },
                { typeof(MathMax), new[] { "First", "Second" } },
                { typeof(MathMaxInt), new[] { "First", "Second" } },
                { typeof(MathMin), new[] { "First", "Second" } },
                { typeof(MathMultiply), new[] { "First", "Second" } },
                { typeof(MathMultiplyFloat), new[] { "First", "Second" } },
                { typeof(MathSubtract), new[] { "First", "Second" } },
                { typeof(MathSubtractFloat), new[] { "First", "Second" } },
                { typeof(MathSubtractLong), new[] { "First", "Second" } },
                { typeof(ListLength), new[] { "InputList" } },
                { typeof(Progress), new[] { "StartTime", "EndTime", "CurrentTime" } },
            };
            const BindingFlags instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            MethodInfo setUp = typeof(BinderNode).GetMethod("SetUpBindReferenceFields", instance);
            FieldInfo list = typeof(BinderNode).GetField("bindReferences", instance);
            foreach (KeyValuePair<Type, string[]> entry in expected)
            {
                BinderNode node = (BinderNode)owner.AddComponent(entry.Key);
                var seen = new List<object>();
                foreach (FieldInfo field in entry.Key.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
                {
                    object value = field.GetValue(node);
                    Assert.IsNotNull(value, entry.Key.Name + "." + field.Name);
                    Assert.IsFalse(seen.Exists(other => ReferenceEquals(other, value)), entry.Key.Name + "." + field.Name + " is shared");
                    seen.Add(value);
                }
                Assert.IsNull(list.GetValue(node), entry.Key.Name + " inactive node has no list yet");
                setUp.Invoke(node, null);
                var actual = (List<BindReference>)list.GetValue(node);
                var wanted = new List<BindReference>();
                foreach (string name in entry.Value) wanted.Add((BindReference)entry.Key.GetField(name).GetValue(node));
                // BindReference equality compares target and member, so identity is checked per slot.
                Assert.AreEqual(wanted.Count, actual.Count, entry.Key.Name);
                for (int i = 0; i < wanted.Count; i++) Assert.AreSame(wanted[i], actual[i], entry.Key.Name + " " + entry.Value[i]);
                Object.DestroyImmediate(node);
            }
            LogAssert.NoUnexpectedReceived();
        }

        // Equality.Refresh 0x19FCC00: both values are read (0x19fcc3c, 0x19fcc50); a non-null
        // first value decides through its own virtual Equals (0x19fcc6e) with no reference
        // shortcut, and a null first value matches only a null second (0x19fcc9d).
        [Test]
        public void EqualityUsesTheFirstValuesEqualsAndANullFirstMatchesOnlyNull()
        {
            Equality node = Node<Equality>();
            Bind(node.First, "Object1");
            Bind(node.Second, "Object2");

            carrier.Object1 = carrier.Recording<object>("First", null);
            carrier.Object2 = carrier.Recording<object>("Second", null);
            node.Refresh();
            Assert.IsTrue(node.Output.Value, "null, null");
            AssertReads("First", "Second");

            carrier.Object2 = carrier.Recording<object>("Second", 1);
            node.Refresh();
            Assert.IsFalse(node.Output.Value, "null, 1");
            AssertReads("First", "Second");

            carrier.Object1 = carrier.Recording<object>("First", 1);
            carrier.Object2 = carrier.Recording<object>("Second", null);
            node.Refresh();
            Assert.IsFalse(node.Output.Value, "1, null");

            carrier.Object2 = carrier.Recording<object>("Second", 1);
            node.Refresh();
            Assert.IsTrue(node.Output.Value, "separately boxed equal ints are equal by value");

            carrier.Object2 = carrier.Recording<object>("Second", 1L);
            node.Refresh();
            Assert.IsFalse(node.Output.Value, "Int32.Equals rejects a boxed long");

            var never = new NeverEqual();
            carrier.Object1 = carrier.Recording<object>("First", never);
            carrier.Object2 = carrier.Recording<object>("Second", never);
            node.Refresh();
            Assert.IsFalse(node.Output.Value, "the same instance still goes through Equals (no ReferenceEquals shortcut)");

            carrier.Object1 = carrier.Recording<object>("First", new AlwaysEqual());
            carrier.Object2 = carrier.Recording<object>("Second", null);
            node.Refresh();
            Assert.IsTrue(node.Output.Value, "a non-null first value is asked even about a null second");
        }

        // EnumIntEquality.Refresh 0x19FC260: both inputs are read as int (0x19fc29c, 0x19fc2b0)
        // and compared with sete (0x19fc2c3), so an enum matches its underlying value.
        [Test]
        public void EnumIntEqualityComparesTheEnumAsItsIntegerValue()
        {
            EnumIntEquality node = Node<EnumIntEquality>();
            Bind(node.First, "Colour");
            Bind(node.Second, "Int1");
            carrier.Colour = carrier.Recording("First", Shade.Blue);
            carrier.Int1 = carrier.Recording("Second", 2);
            node.Refresh();
            Assert.IsTrue(node.Output.Value);
            AssertReads("First", "Second");

            carrier.Int1 = carrier.Recording("Second", 1);
            node.Refresh();
            Assert.IsFalse(node.Output.Value);
        }

        // IsNull.Refresh 0x19FD840 tests the object read for null (0x19fd88b); an unset input
        // reads as null. IsStringNullOrEmpty.Refresh 0x19FDB00 passes the string read to
        // String.IsNullOrEmpty (0x19fdb46); an unset input reads as the empty string.
        [Test]
        public void IsNullAndIsStringNullOrEmptyTestTheValueRead()
        {
            IsNull isNull = Node<IsNull>();
            isNull.Refresh();
            Assert.IsTrue(isNull.Output.Value, "unset input");
            Bind(isNull.Input, "Object1");
            carrier.Object1 = carrier.Recording<object>("Input", null);
            isNull.Refresh();
            Assert.IsTrue(isNull.Output.Value, "null value");
            AssertReads("Input");
            Bind(isNull.Input, "Int1");
            carrier.Int1 = carrier.Recording("Input", 0);
            isNull.Refresh();
            Assert.IsFalse(isNull.Output.Value, "a boxed zero is not null");

            IsStringNullOrEmpty isEmpty = Node<IsStringNullOrEmpty>();
            isEmpty.Refresh();
            Assert.IsTrue(isEmpty.Output.Value, "unset input");
            Bind(isEmpty.Input, "Text");
            string[] values = { null, "", " ", "a" };
            bool[] results = { true, true, false, false };
            for (int i = 0; i < values.Length; i++)
            {
                carrier.Text = carrier.Recording("Input", values[i]);
                isEmpty.Refresh();
                Assert.AreEqual(results[i], isEmpty.Output.Value, "value " + (values[i] ?? "null"));
            }

            // The read is GetValue<string> (0x19fdb2e), so a non-string source arrives as its
            // ToString() and a null one as the empty string (BindReference.GetValue<T>).
            Bind(isEmpty.Input, "Int1");
            carrier.Int1 = carrier.Recording("Input", 0);
            isEmpty.Refresh();
            Assert.IsFalse(isEmpty.Output.Value, "a boxed zero reads as \"0\"");
            Bind(isEmpty.Input, "Object1");
            carrier.Object1 = carrier.Recording<object>("Input", null);
            isEmpty.Refresh();
            Assert.IsTrue(isEmpty.Output.Value, "a null object reads as the empty string");
        }

        // LogicalAnd.Refresh 0x19FE110 skips Second when First is false (0x19fe157);
        // LogicalOr.Refresh 0x1A01010 skips Second when First is true (0x1a0105b);
        // LogicalNot.Refresh 0x1A00D50 inverts its input (0x1a00d96).
        [Test]
        public void AndOrAndNotFollowTheirTruthTablesAndShortCircuitOnFirst()
        {
            LogicalAnd andNode = Node<LogicalAnd>();
            LogicalOr orNode = Node<LogicalOr>();
            Bind(andNode.First, "Bool1");
            Bind(andNode.Second, "Bool2");
            Bind(orNode.First, "Bool1");
            Bind(orNode.Second, "Bool2");
            bool[] firsts = { false, false, true, true };
            bool[] seconds = { false, true, false, true };
            bool[] andResults = { false, false, false, true };
            bool[] orResults = { false, true, true, true };
            for (int i = 0; i < 4; i++)
            {
                carrier.Bool1 = carrier.Recording("First", firsts[i]);
                carrier.Bool2 = carrier.Recording("Second", seconds[i]);
                andNode.Refresh();
                Assert.AreEqual(andResults[i], andNode.Output.Value, "andNode " + i);
                if (firsts[i]) AssertReads("First", "Second");
                else AssertReads("First");
                orNode.Refresh();
                Assert.AreEqual(orResults[i], orNode.Output.Value, "orNode " + i);
                if (firsts[i]) AssertReads("First");
                else AssertReads("First", "Second");
            }

            LogicalNot notNode = Node<LogicalNot>();
            Bind(notNode.Input, "Bool1");
            carrier.Bool1 = carrier.Recording("Input", false);
            notNode.Refresh();
            Assert.IsTrue(notNode.Output.Value);
            carrier.Bool1 = carrier.Recording("Input", true);
            notNode.Refresh();
            Assert.IsFalse(notNode.Output.Value);
            notNode.Input.SetTarget(null, string.Empty);
            notNode.Refresh();
            Assert.IsTrue(notNode.Output.Value, "an unset input reads false");
        }

        // LogicalAndMultiple.Refresh 0x19FE4C0: the result starts true (0x19fe4f8), each input
        // is consulted only when IsSet (0x19fe4f3, 0x19fe52b, ...), and once the result is
        // false a later set input is not read (0x19fe537, 0x19fe575, ...).
        [Test]
        public void AllOfSkipsUnsetInputsAndStopsReadingOnceFalse()
        {
            LogicalAndMultiple node = Node<LogicalAndMultiple>();
            node.Refresh();
            Assert.IsTrue(node.Output.Value, "no input set");

            carrier.Bool1 = carrier.Recording("First", true);
            carrier.Bool2 = carrier.Recording("Second", false);
            carrier.Bool3 = carrier.Recording("Third", true);
            carrier.Bool4 = carrier.Recording("Fourth", true);
            carrier.Bool6 = carrier.Recording("Sixth", true);
            Bind(node.First, "Bool1");
            Bind(node.Fourth, "Bool4");
            node.Refresh();
            Assert.IsTrue(node.Output.Value, "set inputs all true");
            AssertReads("First", "Fourth");

            Bind(node.Second, "Bool2");
            Bind(node.Third, "Bool3");
            Bind(node.Sixth, "Bool6");
            node.Refresh();
            Assert.IsFalse(node.Output.Value, "one set input false");
            AssertReads("First", "Second");

            carrier.Bool1 = carrier.Recording("First", false);
            node.Refresh();
            Assert.IsFalse(node.Output.Value);
            AssertReads("First");
        }

        // LogicalAnyOf.Refresh 0x19FED20 reads First..Sixth (+0x58..+0x80) left to right and
        // stops at the first true; Seventh (+0x88) is never read. LogicalAnyOf3.Refresh
        // 0x19FF4D0 does the same over three inputs.
        [Test]
        public void AnyOfReadsOnlyTheFirstSixInputsAndStopsAtTheFirstTrue()
        {
            LogicalAnyOf node = Node<LogicalAnyOf>();
            node.Refresh();
            Assert.IsFalse(node.Output.Value, "no input set");

            string[] names = { "First", "Second", "Third", "Fourth", "Fifth", "Sixth", "Seventh" };
            BindReference[] inputs = { node.First, node.Second, node.Third, node.Fourth, node.Fifth, node.Sixth, node.Seventh };
            for (int i = 0; i < 7; i++) Bind(inputs[i], "Bool" + (i + 1));
            carrier.Bool1 = carrier.Recording("First", false);
            carrier.Bool2 = carrier.Recording("Second", false);
            carrier.Bool3 = carrier.Recording("Third", false);
            carrier.Bool4 = carrier.Recording("Fourth", false);
            carrier.Bool5 = carrier.Recording("Fifth", false);
            carrier.Bool6 = carrier.Recording("Sixth", false);
            carrier.Bool7 = carrier.Recording("Seventh", true);
            node.Refresh();
            Assert.IsFalse(node.Output.Value, "a true Seventh is ignored");
            AssertReads(names[0], names[1], names[2], names[3], names[4], names[5]);

            carrier.Bool4 = carrier.Recording("Fourth", true);
            node.Refresh();
            Assert.IsTrue(node.Output.Value);
            AssertReads(names[0], names[1], names[2], names[3]);

            carrier.Bool4 = carrier.Recording("Fourth", false);
            carrier.Bool6 = carrier.Recording("Sixth", true);
            node.Refresh();
            Assert.IsTrue(node.Output.Value, "Sixth is still an input");
            AssertReads(names[0], names[1], names[2], names[3], names[4], names[5]);

            LogicalAnyOf3 three = Node<LogicalAnyOf3>();
            Bind(three.First, "Bool1");
            Bind(three.Second, "Bool2");
            Bind(three.Third, "Bool3");
            three.Refresh();
            Assert.IsFalse(three.Output.Value);
            AssertReads("First", "Second", "Third");
            carrier.Bool2 = carrier.Recording("Second", true);
            three.Refresh();
            Assert.IsTrue(three.Output.Value);
            AssertReads("First", "Second");
        }

        // LogicalGreaterOrEqual 0x19FF960, LogicalGreaterThan 0x19FFDC0, LogicalLessThan
        // 0x1A008F0: First is read as object and cast to IComparable (IsInst 0x19ff9c4); a
        // non-comparable or null first gives false without reading Second (0x19ffa3b).
        // Otherwise CompareTo(Second) is tested with setns / setg / shr 0x1f.
        [Test]
        public void ComparisonsNeedAComparableFirstValueAndTestTheCompareToSign()
        {
            LogicalGreaterOrEqual ge = Node<LogicalGreaterOrEqual>();
            LogicalGreaterThan gt = Node<LogicalGreaterThan>();
            LogicalLessThan lt = Node<LogicalLessThan>();
            BinderNode[] nodes = { ge, gt, lt };
            BindReference[] firsts = { ge.First, gt.First, lt.First };
            BindReference[] seconds = { ge.Second, gt.Second, lt.Second };
            Func<bool>[] outputs = { () => ge.Output.Value, () => gt.Output.Value, () => lt.Output.Value };
            for (int n = 0; n < 3; n++)
            {
                Bind(firsts[n], "Int1");
                Bind(seconds[n], "Int2");
            }
            int[] lefts = { 1, 2, 3 };
            bool[][] expected =
            {
                new[] { false, true, true },
                new[] { false, false, true },
                new[] { true, false, false },
            };
            for (int i = 0; i < 3; i++)
            {
                carrier.Int1 = carrier.Recording("First", lefts[i]);
                carrier.Int2 = carrier.Recording("Second", 2);
                for (int n = 0; n < 3; n++)
                {
                    nodes[n].Refresh();
                    Assert.AreEqual(expected[n][i], outputs[n](), nodes[n].GetType().Name + " " + lefts[i] + " vs 2");
                    AssertReads("First", "Second");
                }
            }

            for (int n = 0; n < 3; n++)
            {
                Bind(firsts[n], "Text");
                Bind(seconds[n], "Object2");
            }
            carrier.Text = carrier.Recording("First", "a");
            carrier.Object2 = carrier.Recording<object>("Second", "b");
            bool[] stringExpected = { false, false, true };
            for (int n = 0; n < 3; n++)
            {
                nodes[n].Refresh();
                Assert.AreEqual(stringExpected[n], outputs[n](), nodes[n].GetType().Name + " a vs b");
            }

            for (int n = 0; n < 3; n++) Bind(firsts[n], "Object1");
            object[] incomparable = { new object(), null };
            foreach (object first in incomparable)
            {
                carrier.Object1 = carrier.Recording("First", first);
                for (int n = 0; n < 3; n++)
                {
                    carrier.Reads.Clear();
                    nodes[n].Refresh();
                    Assert.IsFalse(outputs[n](), nodes[n].GetType().Name + " with a non-comparable first");
                    AssertReads("First");
                }
            }
        }

        // LogicalInSet.Refresh 0x1A00220: a null Item gives false without reading the set
        // (0x1a00274); otherwise Item.Equals is asked of SetItem1..4 in order (0x1a002a3,
        // 0x1a002da, 0x1a0030a, 0x1a00336), stopping at the first match.
        [Test]
        public void InSetMatchesTheItemThroughEqualsAndStopsAtTheFirstMatch()
        {
            LogicalInSet node = Node<LogicalInSet>();
            Bind(node.Item, "Object1");
            Bind(node.SetItem1, "Object2");
            Bind(node.SetItem2, "Object3");
            Bind(node.SetItem3, "Object4");
            Bind(node.SetItem4, "Object5");
            carrier.Object1 = carrier.Recording<object>("Item", 2);
            carrier.Object2 = carrier.Recording<object>("SetItem1", 1);
            carrier.Object3 = carrier.Recording<object>("SetItem2", 2L);
            carrier.Object4 = carrier.Recording<object>("SetItem3", 2);
            carrier.Object5 = carrier.Recording<object>("SetItem4", 2);
            node.Refresh();
            Assert.IsTrue(node.Output.Value);
            AssertReads("Item", "SetItem1", "SetItem2", "SetItem3");

            carrier.Object4 = carrier.Recording<object>("SetItem3", 3);
            carrier.Object5 = carrier.Recording<object>("SetItem4", null);
            node.Refresh();
            Assert.IsFalse(node.Output.Value, "no member equal by Int32.Equals");
            AssertReads("Item", "SetItem1", "SetItem2", "SetItem3", "SetItem4");

            carrier.Object1 = carrier.Recording<object>("Item", null);
            carrier.Object2 = carrier.Recording<object>("SetItem1", null);
            node.Refresh();
            Assert.IsFalse(node.Output.Value, "a null item is never in the set, even beside a null member");
            AssertReads("Item");

            // Each test is the item's own virtual Equals (vtable slot 0, 0x138): no reference
            // shortcut, and a null member is still offered to the item.
            var never = new NeverEqual();
            carrier.Object1 = carrier.Recording<object>("Item", never);
            carrier.Object2 = carrier.Recording<object>("SetItem1", never);
            carrier.Object3 = carrier.Recording<object>("SetItem2", never);
            carrier.Object4 = carrier.Recording<object>("SetItem3", never);
            carrier.Object5 = carrier.Recording<object>("SetItem4", never);
            node.Refresh();
            Assert.IsFalse(node.Output.Value, "the same instance is not in the set when its Equals says no");
            AssertReads("Item", "SetItem1", "SetItem2", "SetItem3", "SetItem4");

            carrier.Object1 = carrier.Recording<object>("Item", new AlwaysEqual());
            carrier.Object2 = carrier.Recording<object>("SetItem1", null);
            node.Refresh();
            Assert.IsTrue(node.Output.Value, "a null member matches when the item's Equals says yes");
            AssertReads("Item", "SetItem1");
        }

        // Unchecked integer arithmetic on First and Second in that order: MathAdd add
        // 0x1a02fb1, MathSubtract sub 0x1a05861 (First - Second), MathMultiply imul 0x1a050f1,
        // MathAddLong add 0x1a03721, MathSubtractLong sub 0x1a05fd1, all on 32/64-bit registers.
        [Test]
        public void IntegerAndLongArithmeticWrapsAndKeepsOperandOrder()
        {
            MathAdd add = Node<MathAdd>();
            MathSubtract subtract = Node<MathSubtract>();
            MathMultiply multiply = Node<MathMultiply>();
            BinderNode[] intNodes = { add, subtract, multiply };
            Bind(add.First, "Int1");
            Bind(add.Second, "Int2");
            Bind(subtract.First, "Int1");
            Bind(subtract.Second, "Int2");
            Bind(multiply.First, "Int1");
            Bind(multiply.Second, "Int2");
            carrier.Int1 = carrier.Recording("First", 3);
            carrier.Int2 = carrier.Recording("Second", 10);
            foreach (BinderNode node in intNodes)
            {
                node.Refresh();
                AssertReads("First", "Second");
            }
            Assert.AreEqual(13, add.Output.Value);
            Assert.AreEqual(-7, subtract.Output.Value);
            Assert.AreEqual(30, multiply.Output.Value);

            carrier.Int1 = carrier.Recording("First", int.MaxValue);
            carrier.Int2 = carrier.Recording("Second", 1);
            add.Refresh();
            Assert.AreEqual(int.MinValue, add.Output.Value);
            carrier.Int1 = carrier.Recording("First", int.MinValue);
            subtract.Refresh();
            Assert.AreEqual(int.MaxValue, subtract.Output.Value);
            carrier.Int1 = carrier.Recording("First", 65536);
            carrier.Int2 = carrier.Recording("Second", 65536);
            multiply.Refresh();
            Assert.AreEqual(0, multiply.Output.Value);

            MathAddLong addLong = Node<MathAddLong>();
            MathSubtractLong subtractLong = Node<MathSubtractLong>();
            Bind(addLong.First, "Long1");
            Bind(addLong.Second, "Long2");
            Bind(subtractLong.First, "Long1");
            Bind(subtractLong.Second, "Long2");
            carrier.Long1 = carrier.Recording("First", (long)int.MaxValue);
            carrier.Long2 = carrier.Recording("Second", 1L << 40);
            addLong.Refresh();
            AssertReads("First", "Second");
            subtractLong.Refresh();
            AssertReads("First", "Second");
            Assert.AreEqual(2147483647L + 1099511627776L, addLong.Output.Value);
            Assert.AreEqual(2147483647L - 1099511627776L, subtractLong.Output.Value);
            carrier.Long1 = carrier.Recording("First", long.MaxValue);
            carrier.Long2 = carrier.Recording("Second", 1L);
            addLong.Refresh();
            Assert.AreEqual(long.MinValue, addLong.Output.Value);
        }

        // Single-precision First op Second: MathAddFloat addss 0x1a03365, MathSubtractFloat
        // subss 0x1a05c15, MathMultiplyFloat mulss 0x1a054a5.
        [Test]
        public void FloatArithmeticKeepsOperandOrder()
        {
            MathAddFloat add = Node<MathAddFloat>();
            MathSubtractFloat subtract = Node<MathSubtractFloat>();
            MathMultiplyFloat multiply = Node<MathMultiplyFloat>();
            Bind(add.First, "Float1");
            Bind(add.Second, "Float2");
            Bind(subtract.First, "Float1");
            Bind(subtract.Second, "Float2");
            Bind(multiply.First, "Float1");
            Bind(multiply.Second, "Float2");
            carrier.Float1 = carrier.Recording("First", 1.5f);
            carrier.Float2 = carrier.Recording("Second", 0.25f);
            add.Refresh();
            AssertReads("First", "Second");
            subtract.Refresh();
            AssertReads("First", "Second");
            multiply.Refresh();
            AssertReads("First", "Second");
            Assert.AreEqual(1.75f, add.Output.Value);
            Assert.AreEqual(1.25f, subtract.Output.Value);
            Assert.AreEqual(0.375f, multiply.Output.Value);
        }

        // MathDivide.Refresh 0x1A03A70 converts both ints to float and divides (0x1a03ad1..
        // 0x1a03ada) with no zero guard. MathDivideLong.Refresh 0x1A03E30 reads the
        // Denominator first (0x1a03e6e); zero stores 0 without reading the Numerator
        // (0x1a03ea6), otherwise (float)Numerator / (float)Denominator.
        [Test]
        public void DivideUsesFloatDivisionAndOnlyTheLongFormGuardsZero()
        {
            MathDivide divide = Node<MathDivide>();
            Bind(divide.Numerator, "Int1");
            Bind(divide.Denominator, "Int2");
            carrier.Int1 = carrier.Recording("Numerator", 1);
            carrier.Int2 = carrier.Recording("Denominator", 2);
            divide.Refresh();
            Assert.AreEqual(0.5f, divide.Output.Value, "not integer division");
            AssertReads("Numerator", "Denominator");
            carrier.Int2 = carrier.Recording("Denominator", 0);
            divide.Refresh();
            Assert.IsTrue(float.IsPositiveInfinity(divide.Output.Value));
            carrier.Int1 = carrier.Recording("Numerator", 0);
            divide.Refresh();
            Assert.IsTrue(float.IsNaN(divide.Output.Value));

            MathDivideLong divideLong = Node<MathDivideLong>();
            Bind(divideLong.Numerator, "Long1");
            Bind(divideLong.Denominator, "Long2");
            carrier.Long1 = carrier.Recording("Numerator", 1L << 40);
            carrier.Long2 = carrier.Recording("Denominator", 1L << 20);
            divideLong.Refresh();
            Assert.AreEqual(1048576f, divideLong.Output.Value);
            AssertReads("Denominator", "Numerator");
            carrier.Long1 = carrier.Recording("Numerator", 1L);
            carrier.Long2 = carrier.Recording("Denominator", 4L);
            divideLong.Refresh();
            Assert.AreEqual(0.25f, divideLong.Output.Value);
            carrier.Reads.Clear();
            carrier.Long2 = carrier.Recording("Denominator", 0L);
            divideLong.Refresh();
            Assert.AreEqual(0f, divideLong.Output.Value);
            AssertReads("Denominator");
        }

        // MathAbs 0x1A02C30 inlines Mathf.Abs(int) (init flag Mathf_Abs_..._inline at 0x319EAD6;
        // neg/cmovs). int.MinValue is left out: IL2CPP returns it unchanged, Editor Mono throws.
        // MathFloor 0x1A04200 inlines Mathf.FloorToInt (init flag Mathf_FloorToInt_..._inline at
        // 0x319EA98; roundss 0x9, cvttss2si), flooring rather than truncating. MathMax maxss
        // 0x1a04595 and MathMin minss 0x1a04d35 have Mathf.Max/Min semantics (the presumed
        // source; no inline marker proves the call): a > b ? a : b / a < b ? a : b, so a NaN
        // First yields Second and a NaN Second yields NaN (System.Math would return NaN both
        // ways). MathMaxInt calls System.Math.Max(int, int) (0x1a0497b).
        [Test]
        public void AbsFloorMaxAndMinFollowTheirUnityAndSystemForms()
        {
            MathAbs abs = Node<MathAbs>();
            Bind(abs.Input, "Int1");
            int[] absIn = { -5, 7, 0 };
            int[] absOut = { 5, 7, 0 };
            for (int i = 0; i < absIn.Length; i++)
            {
                carrier.Int1 = carrier.Recording("Input", absIn[i]);
                abs.Refresh();
                Assert.AreEqual(absOut[i], abs.Output.Value);
            }

            MathFloor floor = Node<MathFloor>();
            Bind(floor.First, "Float1");
            float[] floorIn = { -1.5f, 2.7f, 3f, -0.25f };
            int[] floorOut = { -2, 2, 3, -1 };
            for (int i = 0; i < floorIn.Length; i++)
            {
                carrier.Float1 = carrier.Recording("First", floorIn[i]);
                floor.Refresh();
                Assert.AreEqual(floorOut[i], floor.Output.Value, "floor " + floorIn[i]);
            }

            MathMax max = Node<MathMax>();
            MathMin min = Node<MathMin>();
            Bind(max.First, "Float1");
            Bind(max.Second, "Float2");
            Bind(min.First, "Float1");
            Bind(min.Second, "Float2");
            carrier.Float1 = carrier.Recording("First", 1f);
            carrier.Float2 = carrier.Recording("Second", 2f);
            max.Refresh();
            min.Refresh();
            Assert.AreEqual(2f, max.Output.Value);
            Assert.AreEqual(1f, min.Output.Value);
            carrier.Float1 = carrier.Recording("First", float.NaN);
            carrier.Float2 = carrier.Recording("Second", 1f);
            max.Refresh();
            min.Refresh();
            Assert.AreEqual(1f, max.Output.Value, "NaN first");
            Assert.AreEqual(1f, min.Output.Value, "NaN first");
            carrier.Float1 = carrier.Recording("First", 1f);
            carrier.Float2 = carrier.Recording("Second", float.NaN);
            max.Refresh();
            min.Refresh();
            Assert.IsTrue(float.IsNaN(max.Output.Value), "NaN second");
            Assert.IsTrue(float.IsNaN(min.Output.Value), "NaN second");

            MathMaxInt maxInt = Node<MathMaxInt>();
            Bind(maxInt.First, "Int1");
            Bind(maxInt.Second, "Int2");
            carrier.Int1 = carrier.Recording("First", -3);
            carrier.Int2 = carrier.Recording("Second", 2);
            maxInt.Refresh();
            Assert.AreEqual(2, maxInt.Output.Value);
            carrier.Int1 = carrier.Recording("First", 5);
            maxInt.Refresh();
            Assert.AreEqual(5, maxInt.Output.Value);
        }

        // ListLength.Refresh 0x19FDDD0 writes ICollection.Count (0x19fde90) only when the list
        // read is non-null (0x19fde21); otherwise Length keeps its previous value.
        [Test]
        public void ListLengthCountsTheListAndKeepsThePreviousLengthWithoutOne()
        {
            ListLength node = Node<ListLength>();
            node.Length.Value = 9;
            node.Refresh();
            Assert.AreEqual(9, node.Length.Value, "unset input");
            Bind(node.InputList, "List");
            carrier.List = carrier.Recording<IList>("InputList", new List<int> { 1, 2, 3 });
            node.Refresh();
            Assert.AreEqual(3, node.Length.Value);
            AssertReads("InputList");
            carrier.List = carrier.Recording<IList>("InputList", new string[5]);
            node.Refresh();
            Assert.AreEqual(5, node.Length.Value);
            carrier.List = carrier.Recording<IList>("InputList", null);
            node.Refresh();
            Assert.AreEqual(5, node.Length.Value, "a null list leaves Length alone");
        }

        private static readonly DateTime Origin = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        // Progress.Refresh 0x1A07610 reads CurrentTime then EndTime; a default end becomes the
        // current time (op_Equality 0x1a076c7) and a current time past the end is clamped to it
        // (op_GreaterThan 0x1a076ec). RemainingTime = end - current (0x1a07734). With StartTime
        // set, ElapsedTime = current - start and ProgressAmount = (float)(elapsed / (end - start))
        // in seconds (0x1a07812); there is no clamp below the start.
        [Test]
        public void ProgressWithAStartTimeClampsOnlyAtTheEnd()
        {
            Progress node = Node<Progress>();
            Bind(node.StartTime, "Start");
            Bind(node.EndTime, "End");
            Bind(node.CurrentTime, "Current");
            carrier.Start = carrier.Recording("Start", Origin);
            carrier.End = carrier.Recording("End", Origin.AddHours(10));

            carrier.Current = carrier.Recording("Current", Origin.AddHours(4));
            node.Refresh();
            AssertReads("Current", "End", "Start");
            Assert.AreEqual(TimeSpan.FromHours(6), node.RemainingTime.Value);
            Assert.AreEqual(TimeSpan.FromHours(4), node.ElapsedTime.Value);
            Assert.AreEqual(0.4f, node.ProgressAmount.Value);

            carrier.Current = carrier.Recording("Current", Origin.AddHours(12));
            node.Refresh();
            Assert.AreEqual(TimeSpan.Zero, node.RemainingTime.Value);
            Assert.AreEqual(TimeSpan.FromHours(10), node.ElapsedTime.Value, "clamped to the end");
            Assert.AreEqual(1f, node.ProgressAmount.Value);

            carrier.Current = carrier.Recording("Current", Origin.AddHours(-2));
            node.Refresh();
            Assert.AreEqual(TimeSpan.FromHours(12), node.RemainingTime.Value);
            Assert.AreEqual(TimeSpan.FromHours(-2), node.ElapsedTime.Value, "not clamped at the start");
            Assert.AreEqual(-0.2f, node.ProgressAmount.Value);

            // The two TotalSeconds doubles are divided (divsd 0x1a07812) and narrowed once
            // (cvtsd2ss 0x1a07819): (float)(16777217.0 / 16777219.0) has bits 0x3F7FFFFE, while
            // dividing the narrowed operands (16777216f / 16777220f) would give 0x3F7FFFFC.
            carrier.End = carrier.Recording("End", Origin.AddSeconds(16777219));
            carrier.Current = carrier.Recording("Current", Origin.AddSeconds(16777217));
            node.Refresh();
            Assert.AreEqual(TimeSpan.FromSeconds(2), node.RemainingTime.Value);
            Assert.AreEqual(TimeSpan.FromSeconds(16777217), node.ElapsedTime.Value);
            Assert.AreEqual(0x3F7FFFFE, BitConverter.ToInt32(BitConverter.GetBytes(node.ProgressAmount.Value), 0),
                "the ratio is narrowed after a double division");

            carrier.End = carrier.Recording("End", default(DateTime));
            carrier.Current = carrier.Recording("Current", Origin.AddHours(4));
            node.Refresh();
            Assert.AreEqual(TimeSpan.Zero, node.RemainingTime.Value, "a default end collapses onto the current time");
            Assert.AreEqual(TimeSpan.FromHours(4), node.ElapsedTime.Value);
            Assert.AreEqual(1f, node.ProgressAmount.Value);
        }

        // Progress.Refresh 0x1A07610 without StartTime: with EndTime set (0x1a0782c) the amount
        // is 0 while RemainingTime.TotalSeconds > 0 and 1.0f (constant 0x29a6be0) otherwise
        // (0x1a07885), and ElapsedTime is untouched; with neither set only RemainingTime is written.
        [Test]
        public void ProgressWithoutAStartTimeReportsOnlyFinishedOrNot()
        {
            Progress node = Node<Progress>();
            node.ElapsedTime.Value = TimeSpan.FromSeconds(99);
            node.ProgressAmount.Value = 0.5f;
            node.RemainingTime.Value = TimeSpan.FromSeconds(42);
            Bind(node.CurrentTime, "Current");
            carrier.Current = carrier.Recording("Current", Origin.AddHours(4));
            node.Refresh();
            Assert.AreEqual(TimeSpan.Zero, node.RemainingTime.Value);
            Assert.AreEqual(0.5f, node.ProgressAmount.Value, "neither start nor end set");
            Assert.AreEqual(TimeSpan.FromSeconds(99), node.ElapsedTime.Value);
            AssertReads("Current");

            Bind(node.EndTime, "End");
            carrier.End = carrier.Recording("End", Origin.AddHours(10));
            node.Refresh();
            AssertReads("Current", "End");
            Assert.AreEqual(TimeSpan.FromHours(6), node.RemainingTime.Value);
            Assert.AreEqual(0f, node.ProgressAmount.Value, "time remaining");
            Assert.AreEqual(TimeSpan.FromSeconds(99), node.ElapsedTime.Value);

            carrier.Current = carrier.Recording("Current", Origin.AddHours(10));
            node.Refresh();
            Assert.AreEqual(TimeSpan.Zero, node.RemainingTime.Value);
            Assert.AreEqual(1f, node.ProgressAmount.Value, "no time remaining");
            Assert.AreEqual(TimeSpan.FromSeconds(99), node.ElapsedTime.Value);
        }

        private sealed class NeverEqual
        {
            public override bool Equals(object obj) => false;
            public override int GetHashCode() => 0;
        }

        private sealed class AlwaysEqual
        {
            public override bool Equals(object obj) => true;
            public override int GetHashCode() => 0;
        }
    }
}
