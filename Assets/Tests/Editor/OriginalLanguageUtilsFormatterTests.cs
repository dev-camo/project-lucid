using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Hardlight;
using Hardlight.Localisation;
using NUnit.Framework;
using UnityEngine;

// LanguageUtils.FormatNumber 0x06000028 (x86-64 0x1a351a0) and
// InitialiseNumberFormatter 0x06000029 (x86-64 0x1a353f0) on an owned inactive
// component with no StringTable instance, so every NONE lookup is String.Empty.
public sealed class OriginalLanguageUtilsFormatterTests
{
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    // 0x1a35468..0x1a35485 publishes the cloned invariant formatter at +0x50
    // before the five GetString calls 0x1a354cb..0x1a35511 store the three
    // format caches; NumberGroupSeparator 0x1a3552f then NumberDecimalSeparator
    // 0x1a35547 are assigned last. The empty decimal separator faults in the BCL
    // after the group separator was written. Catches publishing the formatter
    // after the lookups/setters (the second call would initialise and fault
    // again) or assigning the decimal separator first (group stays ",").
    [Test]
    public void InitialiserPublishesFormatterAndCachesBeforeItsDecimalSeparatorFault()
    {
        WithInactiveLanguageUtils(component =>
        {
            Assert.IsTrue(StringTable.IsNull(), "Fixture requires no live StringTable instance");
            Assert.Throws<ArgumentException>(() => component.FormatNumber(1234567.8d, 2, false, false));
            var formatter = (NumberFormatInfo)Field("m_numberFormatter").GetValue(component);
            Assert.IsNotNull(formatter);
            Assert.AreNotSame(CultureInfo.InvariantCulture.NumberFormat, formatter);
            Assert.IsFalse(formatter.IsReadOnly);
            Assert.AreEqual(string.Empty, formatter.NumberGroupSeparator);
            Assert.AreEqual(".", formatter.NumberDecimalSeparator);
            foreach (string name in new[] { "thousandsFormatString", "millionsFormatString", "billionsFormatString" })
                Assert.AreEqual(string.Empty, Field(name).GetValue(component), name);

            // 0x1a351f9 skips initialisation for the published formatter.
            Assert.AreEqual("1234567.80", component.FormatNumber(1234567.8d, 2, false, false));
            Assert.AreSame(formatter, Field("m_numberFormatter").GetValue(component));
        });
    }

    // Abbreviated path: roundsd mode 9 (floor) at 0x1a3531e, then the loop
    // 0x1a35340..0x1a35379 drops a decimal place while (int)(value * factor) % 10
    // is zero. ignoreZeroDecimal (third argument) is never read. Catches
    // Math.Round instead of Floor (1.21M), no trimming (1.20M / 3.00M) and any
    // use of ignoreZeroDecimal. Negatives take the thousands branch (inlined
    // 1e6 > value test 0x1a35225) and floor away from zero; Math.Truncate would
    // give -1.23K and -1.2K (an Editor Mono replay of the native arithmetic agrees).
    [Test]
    public void AbbreviationFloorsThenTrimsTrailingZerosIgnoringZeroDecimalFlag()
    {
        WithInactiveLanguageUtils(component =>
        {
            Assert.IsTrue(StringTable.IsNull(), "Fixture requires no live StringTable instance");
            Assert.Throws<ArgumentException>(() => component.FormatNumber(0d));
            var formatter = (NumberFormatInfo)Field("m_numberFormatter").GetValue(component);
            Assert.IsNotNull(formatter);
            // Owned public formatter and private caches stand in for StringTable
            // content; the separators match the invariant culture.
            formatter.NumberGroupSeparator = ",";
            formatter.NumberDecimalSeparator = ".";
            Field("thousandsFormatString").SetValue(component, "{0}K");
            Field("millionsFormatString").SetValue(component, "{0}M");
            Field("billionsFormatString").SetValue(component, "{0}B");
            foreach (bool ignoreZeroDecimal in new[] { false, true })
            {
                Assert.AreEqual("1.2M", component.FormatNumber(1209000d, 2, ignoreZeroDecimal, true));
                Assert.AreEqual("3M", component.FormatNumber(3000000d, 2, ignoreZeroDecimal, true));
                Assert.AreEqual("1.23M", component.FormatNumber(1239000d, 2, ignoreZeroDecimal, true));
                Assert.AreEqual("12.5K", component.FormatNumber(12550d, 1, ignoreZeroDecimal, true));
                Assert.AreEqual("2.5B", component.FormatNumber(2500000000d, 3, ignoreZeroDecimal, true));
                Assert.AreEqual("-1.24K", component.FormatNumber(-1234.5d, 2, ignoreZeroDecimal, true));
                Assert.AreEqual("-1.21K", component.FormatNumber(-1201d, 2, ignoreZeroDecimal, true));
                Assert.AreEqual("1,234,567.00", component.FormatNumber(1234567d, 2, ignoreZeroDecimal, false));
            }
        });
    }

    // The runtime checks above see only NONE lookups, which all return the same
    // empty string, so the identifier-to-destination mapping is read from the IL.
    // Native 0x1a354ac..0x1a35547 with metadata offsets separatorID 0x0400001c
    // +0x20, decimalID 0x0400001d +0x24, thousandsID 0x0400001e +0x28, millionsID
    // 0x0400001f +0x2c, billionsID 0x04000020 +0x30, thousandsFormatString
    // 0x04000021 +0x38, millionsFormatString 0x04000022 +0x40, billionsFormatString
    // 0x04000023 +0x48: +0x20 (kept in r14) feeds set_NumberGroupSeparator, +0x24
    // (r15) set_NumberDecimalSeparator, and +0x28/+0x2c/+0x30 store to
    // +0x38/+0x40/+0x48, in that lookup order. Catches swapped identifiers,
    // swapped cache stores or swapped separator setters.
    [Test]
    public void InitialiserMapsEachIdentifierToItsShippedDestination()
    {
        MethodInfo method = typeof(LanguageUtils).GetMethod("InitialiseNumberFormatter", Instance);
        Assert.IsNotNull(method);
        Module module = method.Module;
        List<IlInstruction> body = ReadIl(method);
        MethodInfo getString = typeof(StringTable).GetMethod("GetString", new[] { typeof(Hardlight.Enums.Strings) });
        Assert.IsNotNull(getString);
        var mapping = new List<string>();
        for (int i = 0; i < body.Count; i++)
        {
            if (body[i].Code != OpCodes.Call || module.ResolveMethod(body[i].Token) != getString) continue;
            Assert.AreEqual(OpCodes.Ldfld, body[i - 1].Code, "Lookup argument");
            string id = module.ResolveField(body[i - 1].Token).Name;
            IlInstruction store = body[i + 1];
            if (store.Code == OpCodes.Stfld)
            {
                mapping.Add(id + "->" + module.ResolveField(store.Token).Name);
                continue;
            }
            // A local separator flows to the setter that later reloads it.
            int local = LocalIndex(store, "stloc");
            int load = body.FindIndex(i + 2, x => (x.Code.Name == "ldloc" || x.Code.Name.StartsWith("ldloc.")) && LocalIndex(x, "ldloc") == local);
            Assert.GreaterOrEqual(load, 0, id);
            Assert.AreEqual(OpCodes.Callvirt, body[load + 1].Code, id);
            mapping.Add(id + "->" + module.ResolveMethod(body[load + 1].Token).Name);
        }
        CollectionAssert.AreEqual(new[]
        {
            "separatorID->set_NumberGroupSeparator",
            "decimalID->set_NumberDecimalSeparator",
            "thousandsID->thousandsFormatString",
            "millionsID->millionsFormatString",
            "billionsID->billionsFormatString",
        }, mapping);
    }

    private struct IlInstruction
    {
        public OpCode Code;
        public int Token;
    }

    private static readonly Dictionary<short, OpCode> OpCodesByValue = typeof(OpCodes)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Select(field => (OpCode)field.GetValue(null))
        .ToDictionary(code => code.Value);

    // Minimal ECMA-335 decoder keeping opcodes and 32-bit tokens or local indices.
    private static List<IlInstruction> ReadIl(MethodInfo method)
    {
        byte[] il = method.GetMethodBody().GetILAsByteArray();
        var result = new List<IlInstruction>();
        int i = 0;
        while (i < il.Length)
        {
            short value = il[i++];
            if (value == 0xfe) value = unchecked((short)(0xfe00 | il[i++]));
            Assert.IsTrue(OpCodesByValue.TryGetValue(value, out OpCode code), "Unknown IL opcode " + value);
            var instruction = new IlInstruction { Code = code };
            switch (code.OperandType)
            {
                case OperandType.InlineNone: break;
                case OperandType.ShortInlineBrTarget:
                case OperandType.ShortInlineI:
                case OperandType.ShortInlineVar: instruction.Token = il[i]; i += 1; break;
                case OperandType.InlineVar: instruction.Token = BitConverter.ToUInt16(il, i); i += 2; break;
                case OperandType.InlineI8:
                case OperandType.InlineR: i += 8; break;
                case OperandType.InlineSwitch: i += 4 + 4 * BitConverter.ToInt32(il, i); break;
                default: instruction.Token = BitConverter.ToInt32(il, i); i += 4; break;
            }
            result.Add(instruction);
        }
        return result;
    }

    private static int LocalIndex(IlInstruction instruction, string prefix)
    {
        string name = instruction.Code.Name;
        Assert.IsTrue(name == prefix || name.StartsWith(prefix + "."), "Expected " + prefix + ", found " + name);
        string suffix = name == prefix ? "s" : name.Substring(prefix.Length + 1);
        return suffix == "s" ? instruction.Token : int.Parse(suffix);
    }

    private static FieldInfo Field(string name)
    {
        FieldInfo field = typeof(LanguageUtils).GetField(name, Instance);
        Assert.IsNotNull(field, name);
        return field;
    }

    private static void WithInactiveLanguageUtils(Action<LanguageUtils> action)
    {
        LanguageUtils baseline = MonoSingleton<LanguageUtils>.Instance;
        GameObject root = null;
        try
        {
            root = new GameObject("Owned inactive LanguageUtils formatter");
            root.SetActive(false);
            LanguageUtils component = root.AddComponent<LanguageUtils>();
            Assert.IsFalse(root.activeInHierarchy);
            Assert.AreSame(baseline, MonoSingleton<LanguageUtils>.Instance);
            action(component);
            Assert.IsFalse(root.activeInHierarchy);
            Assert.AreSame(baseline, MonoSingleton<LanguageUtils>.Instance);
        }
        finally
        {
            try { if (!ReferenceEquals(root, null)) UnityEngine.Object.DestroyImmediate(root); }
            finally { Assert.AreSame(baseline, MonoSingleton<LanguageUtils>.Instance); }
        }
    }
}
