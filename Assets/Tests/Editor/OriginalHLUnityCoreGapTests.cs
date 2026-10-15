using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Hardlight;
using NUnit.Framework;

namespace ProjectLucid.Tests
{
    public sealed class OriginalHLUnityCoreGapTests
    {
        // CollectionUtils.BestMatch (0x060000d7, shared x86-64 0x8eda70) starts from
        // 0x7fffffff (0x8edbda) and replaces only on an unsigned `jb` (0x8edbfe), after a
        // neg/cmovs magnitude. Ties keep the first item and scores at Int32.MaxValue never
        // match. Fails under `<=` (picks the last tie) or a UInt32.MaxValue start (matches).
        [Test]
        public void BestMatchKeepsTheFirstTieAndRejectsScoresAtInt32MaxValue()
        {
            Assert.That(CollectionUtils.BestMatch(new[] { "a", "b" }, item => 3), Is.EqualTo("a"));
            Assert.That(CollectionUtils.BestMatch(new[] { "a", "b" }, item => -3), Is.EqualTo("a"), "negative scores tie by magnitude");
            Assert.That(CollectionUtils.BestMatch(new[] { "a", "b" }, item => int.MaxValue), Is.Null);
            Assert.That(CollectionUtils.BestMatch(new[] { 7, 8 }, item => int.MaxValue), Is.EqualTo(0), "value types fall back to default");
            Assert.That(CollectionUtils.BestMatch(new[] { "far", "near", "tie" }, item => item == "far" ? 5 : item == "near" ? -2 : 2), Is.EqualTo("near"));
            Assert.That(CollectionUtils.BestMatch(new string[0], item => 0), Is.Null);
        }

        // StackableData..cctor (0x06000102, x86-64 0x1a9ec60) creates the four registries and
        // makes exactly five RegisterTypeOperations calls: bool (AND, OR), then int, float,
        // long and Vector3 (multiply, addition) at 0x1a9eecf..0x1a9f043. The live registries
        // legitimately gain later registrations (TimeManager, UIManager, camera settings) and
        // a re-run initializer could be shadowed by JIT-embedded readonly references, so the
        // compiled initializer body is read instead of mutable state. Fails if a type or
        // operation is added, dropped, reordered or moved to another argument slot.
        [Test]
        public void ClassInitializerRegistersOnlyTheOriginalPrimitiveOperations()
        {
            ConstructorInfo initializer = typeof(StackableData).TypeInitializer;
            Assert.That(initializer, Is.Not.Null);
            var registrations = new List<string>();
            var arguments = new List<string>();
            foreach (KeyValuePair<OpCode, object> instruction in OriginalMethodBodyReader.Instructions(initializer))
            {
                if (instruction.Key == OpCodes.Ldnull) arguments.Add("-");
                else if (instruction.Key == OpCodes.Ldftn)
                {
                    // A static method group is `ldnull; ldftn; newobj`; the ldnull is its target.
                    arguments[arguments.Count - 1] = ((MethodInfo)instruction.Value).Name;
                }
                else if (instruction.Value is MethodInfo method && method.Name == "RegisterTypeOperations")
                {
                    registrations.Add(method.GetGenericArguments()[0].Name + "(" + string.Join(",", arguments) + ")");
                    arguments.Clear();
                }
            }
            Assert.That(registrations, Is.EqualTo(new[]
            {
                "Boolean(BoolLogicalAnd,BoolLogicalOr,-,-)",
                "Int32(-,-,IntMultiply,IntAddition)",
                "Single(-,-,FloatMultiply,FloatAddition)",
                "Int64(-,-,LongMultiply,LongAddition)",
                "Vector3(-,-,Vector3Multiply,Vector3Addition)",
            }));
            Assert.That(arguments, Is.Empty, "no trailing registration arguments");
        }
    }
}
