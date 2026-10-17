using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Hardlight;
using NUnit.Framework;

namespace ProjectLucid.Tests.Editor
{
    // Declaration-only checks against the shipped StackableData rows. The type is split
    // across partial files, so these also catch emission that follows source-file order.
    public sealed class OriginalStackableDataDeclarationTests
    {
        private const BindingFlags Declared = BindingFlags.DeclaredOnly | BindingFlags.Public |
            BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        // dump.cs TypeDefIndex 8796, DummyDll Hardlight.StackableData 0x02000025 field rows
        // 0x04000056..5f: the const (flags 0x8051), four private readonly instance fields
        // (0x21), four private static readonly maps (0x31), then the [CompilerGenerated]
        // OnDataUpdated event field (0x01).
        [Test]
        public void FieldRowsKeepOriginalInstanceStaticAndEventOrder()
        {
            FieldInfo[] fields = typeof(StackableData).GetFields(Declared).OrderBy(f => f.MetadataToken).ToArray();
            CollectionAssert.AreEqual(new[] { "DefaultRetrievalOperation", "m_dataDictionary", "m_stackableCache",
                "m_baseStackableDataHandle", "m_updatedIds", "LogicalAnds", "LogicalOrs", "Multiplies", "Additions",
                "OnDataUpdated" }, fields.Select(f => f.Name));

            Type cachedData = typeof(StackableData).GetNestedType("StackableCachedData", BindingFlags.NonPublic);
            Assert.That(cachedData, Is.Not.Null);
            CollectionAssert.AreEqual(new[] { typeof(StackableData.RetrievalOperation), typeof(OrderedDictionary),
                typeof(Dictionary<,>).MakeGenericType(typeof(int), cachedData), typeof(StackableDataHandle),
                typeof(List<int>), typeof(Dictionary<Type, Delegate>), typeof(Dictionary<Type, Delegate>),
                typeof(Dictionary<Type, Delegate>), typeof(Dictionary<Type, Delegate>), typeof(Action<int>) },
                fields.Select(f => f.FieldType));
            CollectionAssert.AreEqual(new[] { 0x8051, 0x21, 0x21, 0x21, 0x21, 0x31, 0x31, 0x31, 0x31, 0x01 },
                fields.Select(f => (int)f.Attributes));
            CollectionAssert.AreEqual(new[] { false, false, false, false, false, false, false, false, false, true },
                fields.Select(f => f.IsDefined(typeof(CompilerGeneratedAttribute), false)));
            // dump.cs: DefaultRetrievalOperation = 1 (RetrievalOperation.Latest).
            Assert.That(fields[0].GetRawConstantValue(), Is.EqualTo(1));
        }

        // DummyDll method rows 0x06000102..0x06000126: the static constructor, instance
        // constructor and event accessors lead, and the primitive operations come last.
        [Test]
        public void MethodRowsKeepOriginalOrder()
        {
            IEnumerable<MethodBase> declared = typeof(StackableData).GetConstructors(Declared)
                .Cast<MethodBase>().Concat(typeof(StackableData).GetMethods(Declared));
            CollectionAssert.AreEqual(new[] { ".cctor", ".ctor", "add_OnDataUpdated", "remove_OnDataUpdated",
                "SetBaseValue", "SetBaseValue", "CreateOverride", "AddOverride", "AddOverride", "ClearOverrides",
                "RemoveOverrides", "GetOverride", "SetRetrievalOperation", "HasData", "Get", "TryGet", "GetStack",
                "GetStack", "UpdateCachedValue", "Get", "GetBase", "LogicalAnd", "LogicalOr", "Multiply", "Addition",
                "BoolLogicalAnd", "BoolLogicalOr", "IntMultiply", "IntAddition", "FloatMultiply", "FloatAddition",
                "LongMultiply", "LongAddition", "Vector3Multiply", "Vector3Addition", "DataUpdated",
                "RegisterTypeOperations" }, declared.OrderBy(m => m.MetadataToken).Select(m => m.Name));
        }

        // The same DummyDll rows as (name, parameter count, MethodAttributes) from the
        // metadata section, so a swapped same-name overload pair (SetBaseValue, AddOverride,
        // GetStack, or the public and private Get<T>) cannot pass on names alone.
        [Test]
        public void MethodRowsKeepOriginalOverloadOrderAndFlags()
        {
            IEnumerable<MethodBase> declared = typeof(StackableData).GetConstructors(Declared)
                .Cast<MethodBase>().Concat(typeof(StackableData).GetMethods(Declared));
            CollectionAssert.AreEqual(new[] {
                ".cctor/0/0x1891", ".ctor/0/0x1886", "add_OnDataUpdated/1/0x0886", "remove_OnDataUpdated/1/0x0886",
                "SetBaseValue/2/0x0086", "SetBaseValue/3/0x0086", "CreateOverride/0/0x0086",
                "AddOverride/2/0x0086", "AddOverride/3/0x0086", "ClearOverrides/1/0x0086",
                "RemoveOverrides/1/0x0086", "GetOverride/2/0x0086", "SetRetrievalOperation/3/0x0086",
                "HasData/1/0x0086", "Get/3/0x0086", "TryGet/4/0x0086", "GetStack/3/0x0086", "GetStack/2/0x0086",
                "UpdateCachedValue/2/0x0081", "Get/3/0x0081", "GetBase/2/0x0081", "LogicalAnd/2/0x0091",
                "LogicalOr/2/0x0091", "Multiply/2/0x0091", "Addition/2/0x0091", "BoolLogicalAnd/2/0x0091",
                "BoolLogicalOr/2/0x0091", "IntMultiply/2/0x0091", "IntAddition/2/0x0091",
                "FloatMultiply/2/0x0091", "FloatAddition/2/0x0091", "LongMultiply/2/0x0091",
                "LongAddition/2/0x0091", "Vector3Multiply/2/0x0091", "Vector3Addition/2/0x0091",
                "DataUpdated/1/0x0081", "RegisterTypeOperations/4/0x0096" },
                declared.OrderBy(m => m.MetadataToken)
                    .Select(m => $"{m.Name}/{m.GetParameters().Length}/0x{(int)m.Attributes:X4}"));
        }
    }
}
