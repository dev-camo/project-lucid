using System;
using System.Globalization;
using System.Reflection;
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
    // use of ignoreZeroDecimal.
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
                Assert.AreEqual("1,234,567.00", component.FormatNumber(1234567d, 2, ignoreZeroDecimal, false));
            }
        });
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
