using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Hardlight;
using NUnit.Framework;
using UnityEngine;

namespace ProjectLucid.Editor.Tests
{
    // Owned local data only. The HLCrashReport component is never created, and no
    // registry, lifecycle, reporting, bridge or deliberate crash path is invoked.
    public sealed class OriginalHLCrashReportPreservationTests
    {
        [Test]
        public void TrimRemovesOnlyLeadingIgnoredLinesWithInvariantCaseMatching()
        {
            HLCrashReportCurrentFacts.RequireWholeCurrentEmission();
            CultureInfo previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
                string[][] cases = {
                    new[] { null, "" }, new[] { "", "" }, new[] { " \t\r\n", "" },
                    new[] { "Hardlight.HLCrashReport.Call()", "" },
                    new[] { "Hardlight.HLOutput.Call()\nHardlight.FastAction.Call()\nGame.Entry()\nGame.Next()", "Game.Entry()\nGame.Next()" },
                    new[] { "hArDlIgHt.hLcRaShRePoRt.Call()\r\nHARDLIGHT.HLOUTPUT.Call()\nGame.Entry()\r\n", "Game.Entry()\r\n" },
                    new[] { "Hardlight.FastAction.Call()\n", "" },
                    new[] { "Game.Entry()\nHardlight.HLOutput.Call()", "Game.Entry()\nHardlight.HLOutput.Call()" },
                    new[] { " Hardlight.HLOutput.Call()\nGame.Entry()", " Hardlight.HLOutput.Call()\nGame.Entry()" },
                    new[] { "Hardlight.HLOutputExtra.Call()\nGame.Entry()", "Game.Entry()" },
                    new[] { "Game.Entry()\rOnlyCarriageReturn()", "Game.Entry()\rOnlyCarriageReturn()" }
                };
                foreach (string[] item in cases)
                    Assert.That(HLCrashReport.TrimStackTrace(item[0]), Is.EqualTo(item[1]), "literal leading-prefix input: " + item[0]);
            }
            finally { CultureInfo.CurrentCulture = previous; }
        }

        [Test]
        public void FirstFunctionRetainsLiteralFirstLineAndDoesNotTrimPrefixes()
        {
            HLCrashReportCurrentFacts.RequireWholeCurrentEmission();
            string[][] cases = {
                new[] { null, "" }, new[] { " \t\r\n", "" },
                new[] { "Hardlight.HLOutput.Call()\nGame.Entry()", "Hardlight.HLOutput.Call()" },
                new[] { "  Game.Entry()\r\nGame.Next()", "  Game.Entry()\r" },
                new[] { "\nGame.Entry()", "" },
                new[] { "Game.Entry()\rGame.Next()", "Game.Entry()\rGame.Next()" }
            };
            foreach (string[] item in cases)
                Assert.That(HLCrashReport.ExtractFirstFunctionFromStackTrace(item[0]), Is.EqualTo(item[1]));
            MethodInfo helper = typeof(HLCrashReport).GetMethod("StackTraceStartsWithIgnoredLine", BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly);
            Assert.That(helper, Is.Not.Null);
            Assert.That((bool)helper.Invoke(null, new object[] { "Hardlight.FastAction.Call()" }), Is.True);
            TargetInvocationException wrapped = Assert.Throws<TargetInvocationException>(() => helper.Invoke(null, new object[] { null }));
            Assert.That(wrapped.InnerException.GetType(), Is.EqualTo(typeof(NullReferenceException)), "private helper retains its null-input fault");
        }

        [Test]
        public void ReadonlyLogDataConstructorRetainsEverySuppliedIdentityAndInteger()
        {
            HLCrashReportCurrentFacts.RequireWholeCurrentEmission();
            string kind = new string(new[] { ' ', 'k', '\0', 'x' });
            string message = new string(new[] { '\t', 'm', '\u03bb' });
            string stack = new string(new[] { 's', '\r', '\n' });
            LogErrorData first = new LogErrorData(kind, message, int.MinValue, stack);
            Assert.That(ReferenceEquals(first.ErrorType, kind), Is.True);
            Assert.That(ReferenceEquals(first.ErrorMessage, message), Is.True);
            Assert.That(first.ErrorCode, Is.EqualTo(int.MinValue));
            Assert.That(ReferenceEquals(first.StackTrace, stack), Is.True);
            LogErrorData copied = first;
            Assert.That(ReferenceEquals(copied.ErrorType, kind), Is.True);
            Assert.That(ReferenceEquals(copied.ErrorMessage, message), Is.True);
            Assert.That(copied.ErrorCode, Is.EqualTo(int.MinValue));
            Assert.That(ReferenceEquals(copied.StackTrace, stack), Is.True);
            LogErrorData nulls = new LogErrorData(null, null, int.MaxValue, null);
            Assert.That(nulls.ErrorType, Is.Null); Assert.That(nulls.ErrorMessage, Is.Null);
            Assert.That(nulls.ErrorCode, Is.EqualTo(int.MaxValue)); Assert.That(nulls.StackTrace, Is.Null);
            LogErrorData empty = default;
            Assert.That(empty.ErrorType, Is.Null); Assert.That(empty.ErrorMessage, Is.Null);
            Assert.That(empty.ErrorCode, Is.EqualTo(0)); Assert.That(empty.StackTrace, Is.Null);
        }

        [Test]
        public void GenuineConfigurationCreationKeepsDefaultsAndIndependentOwnedAllocations()
        {
            HLCrashReportCurrentFacts.RequireWholeCurrentEmission();
            WithOwned<HLCrashReportConfigurationAsset>(first => WithOwned<HLCrashReportConfigurationAsset>(second => {
                Assert.That(ReferenceEquals(first, second), Is.False);
                Assert.That(first.UseErrorExclusion, Is.False);
                Assert.That(Read(first, "m_nativeCodePath"), Is.EqualTo("/NativeiOS\\~/"));
                Assert.That(Convert.ToInt32(Read(first, "m_crashReportService")), Is.EqualTo(0));
                Assert.That(Read(first, "m_appCenterConfiguration"), Is.Null);
                Assert.That(Read(first, "m_crashlyticsConfiguration"), Is.Null);
                Assert.That(((string[])Read(first, "m_includedNativeFiles")).Length, Is.EqualTo(0));
                string[] a = (string[])Read(first, "m_requiredNativeFiles"), b = (string[])Read(second, "m_requiredNativeFiles");
                Assert.That(a, Is.EqualTo(new[] { "HLCrashReport.h", "HLCrashReport.mm" }));
                Assert.That(b, Is.EqualTo(new[] { "HLCrashReport.h", "HLCrashReport.mm" }));
                Assert.That(ReferenceEquals(a, b), Is.False, "each normal constructor owns its readonly required-file array");
                Assert.That(first.ErrorExclusionList, Is.Not.Null); Assert.That(second.ErrorExclusionList, Is.Not.Null);
                Assert.That(ReferenceEquals(first.ErrorExclusionList, second.ErrorExclusionList), Is.False);
                Assert.That(first.ErrorExclusionList.Name, Is.Null); Assert.That(first.ErrorExclusionList.Exclusions, Is.Null);
                Assert.Throws<NullReferenceException>(() => first.ErrorExclusionList.IsExcluded("owned"), "original new list starts enabled with its null array");
            }));
            WithOwned<CrashlyticsConfiguration>(owned => {
                Assert.That(Read(owned, "m_firebaseDependenciesXmlPath"), Is.EqualTo(string.Empty));
                Assert.That(Read(owned, "m_uploadSymbolsOnBuild"), Is.EqualTo(true));
                Assert.That(Read(owned, "m_overrideDebugFormat"), Is.EqualTo(false));
                Assert.That(Convert.ToInt32(Read(owned, "m_versionSelectOption")), Is.EqualTo(0));
                foreach (string field in new[] { "m_podVersion", "m_gradleBOMVersion" }) {
                    object version = Read(owned, field);
                    Assert.That(version.GetType(), Is.EqualTo(typeof(Hardlight.Utils.Version)));
                    Assert.That(Read(version, "m_version"), Is.Null);
                    Assert.That(Read(version, "m_numericIdentifiers"), Is.Null);
                    Assert.That(Read(version, "m_isParsed"), Is.EqualTo(false));
                }
            });
        }

        [Test]
        public void RequiredNativeFilesDeduplicateWithoutEditingOwnedInputArrays()
        {
            HLCrashReportCurrentFacts.RequireWholeCurrentEmission();
            WithOwned<HLCrashReportConfigurationAsset>(owned => {
                string[] included = { "HLCrashReport.h", "extra.mm", "extra.mm", null, "HLCrashReport.mm", "Extra.mm" };
                string[] before = (string[])included.Clone();
                WriteOwned(owned, "m_includedNativeFiles", included);
                IEnumerable<string> first = owned.GetIncludedNativeFiles();
                Assert.That(new HashSet<string>(first).SetEquals(new[] { "HLCrashReport.h", "HLCrashReport.mm", "extra.mm", null, "Extra.mm" }), Is.True);
                Assert.That(first.Count(), Is.EqualTo(5), "duplicates are eliminated, case and null entries remain distinct");
                Assert.That(ReferenceEquals(Read(owned, "m_includedNativeFiles"), included), Is.True);
                Assert.That(included, Is.EqualTo(before));
                included[1] = "later.mm";
                Assert.That(first.Contains("later.mm"), Is.False, "returned materialized set does not follow later source-array mutation");
                IEnumerable<string> second = owned.GetIncludedNativeFiles();
                Assert.That(ReferenceEquals(first, second), Is.False);
                Assert.That(second.Contains("later.mm"), Is.True); Assert.That(second.Contains("extra.mm"), Is.True);
                Assert.That(((string[])Read(owned, "m_requiredNativeFiles")), Is.EqualTo(new[] { "HLCrashReport.h", "HLCrashReport.mm" }));
            });
        }

        [Test]
        public void NativePathsKeepAllThreeLiteralSuffixesAndUnsupportedValueFaults()
        {
            HLCrashReportCurrentFacts.RequireWholeCurrentEmission();
            WithOwned<HLCrashReportConfigurationAsset>(owned => {
                string[] suffix = { "/AppCenter/", "/Crashlytics/", "/Stub/" };
                for (int i = 0; i < 3; i++) {
                    WriteOwnedEnum(owned, "m_crashReportService", i);
                    Assert.That(owned.GetNativeCodePath(), Is.EqualTo("/NativeiOS\\~/" + suffix[i]));
                }
                WriteOwned(owned, "m_nativeCodePath", "owned-root");
                for (int i = 0; i < 3; i++) {
                    WriteOwnedEnum(owned, "m_crashReportService", i);
                    Assert.That(owned.GetNativeCodePath(), Is.EqualTo("owned-root" + suffix[i]));
                }
                foreach (int invalid in new[] { -1, 3, int.MaxValue }) {
                    WriteOwnedEnum(owned, "m_crashReportService", invalid);
                    InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => owned.GetNativeCodePath());
                    Assert.That(error.Message, Is.EqualTo("Unsupported CrashReportService"));
                }
            });
        }

        [Test]
        public void InitialiseWritesTheSameOwnedExclusionListAndRetainsNullFaults()
        {
            HLCrashReportCurrentFacts.RequireWholeCurrentEmission();
            WithOwned<HLCrashReportConfigurationAsset>(owned => {
                ExclusionList list = owned.ErrorExclusionList;
                owned.Initialise();
                Assert.That(ReferenceEquals(owned.ErrorExclusionList, list), Is.True);
                Assert.That(list.IsExcluded(null), Is.False, "default false disables before reading null array or message");
                list.Exclusions = new[] { "needle" };
                WriteOwned(owned, "m_useErrorExclusion", true); owned.Initialise();
                Assert.That(list.IsExcluded("has needle here"), Is.True);
                Assert.That(list.IsExcluded("has NEEDLE here"), Is.False);
                list.Exclusions = Array.Empty<string>(); Assert.That(list.IsExcluded(null), Is.False);
                list.Exclusions = new[] { "needle" }; Assert.Throws<NullReferenceException>(() => list.IsExcluded(null));
                list.Exclusions = new string[] { null }; Assert.Throws<ArgumentNullException>(() => list.IsExcluded("owned"));
                list.Exclusions = new[] { "" }; Assert.That(list.IsExcluded("owned"), Is.True);
                list.Exclusions = null;
                WriteOwned(owned, "m_useErrorExclusion", false); owned.Initialise();
                Assert.That(list.IsExcluded(null), Is.False);
                WriteOwned(owned, "m_errorExclusionList", null);
                Assert.Throws<NullReferenceException>(() => owned.Initialise(), "original Initialise does not synthesize a replacement list");
                Assert.That(owned.ErrorExclusionList, Is.Null);
            });
        }

        [Test]
        public void GenuineOwnedAppKeysRetainSuppliedDummyConstantForEverySerializedKey()
        {
            HLCrashReportCurrentFacts.RequireWholeCurrentEmission();
            WithOwned<HLCrashReportAppKeys>(owned => {
                string[] fields = { "m_iOSAppSecretProd", "m_iOSAppSecretQA", "m_googleAppSecretProd", "m_googleAppSecretQA", "m_amazonAppSecretProd", "m_amazonAppSecretQA", "m_samsungAppSecretProd", "m_samsungAppSecretQA" };
                foreach (string field in fields) Assert.That(Read(owned, field), Is.Null);
                Assert.That(owned.GetCrashReportAppKey(), Is.EqualTo("dummyAppKey"));
                for (int i = 0; i < fields.Length; i++) WriteOwned(owned, fields[i], "owned-secret-" + i);
                Assert.That(owned.GetCrashReportAppKey(), Is.EqualTo("dummyAppKey"));
                for (int i = 0; i < fields.Length; i++) Assert.That(Read(owned, fields[i]), Is.EqualTo("owned-secret-" + i));
            });
        }

        static void WithOwned<T>(Action<T> action) where T : ScriptableObject
        {
            T owned = null;
            try {
                owned = ScriptableObject.CreateInstance<T>();
                Assert.That(owned != null, Is.True); Assert.That(owned.GetType(), Is.EqualTo(typeof(T)));
                owned.hideFlags = HideFlags.HideAndDontSave;
                action(owned);
            }
            finally { if (!ReferenceEquals(owned, null) && owned != null) UnityEngine.Object.DestroyImmediate(owned); }
        }
        static FieldInfo Field(object owner, string name)
        {
            FieldInfo field = owner.GetType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            Assert.That(field, Is.Not.Null); Assert.That(field.IsStatic, Is.False);
            return field;
        }
        static object Read(object owner, string name) => Field(owner, name).GetValue(owner);
        static void WriteOwned(ScriptableObject owner, string name, object value)
        {
            Assert.That(owner != null, Is.True);
            FieldInfo field = Field(owner, name);
            Assert.That(field.IsInitOnly, Is.False); Assert.That(field.IsLiteral, Is.False);
            Assert.That(field.GetCustomAttributesData().Any(x => x.AttributeType == typeof(SerializeField)), Is.True);
            Assert.That(value == null || field.FieldType.IsInstanceOfType(value), Is.True);
            field.SetValue(owner, value);
        }
        static void WriteOwnedEnum(ScriptableObject owner, string name, int value)
        {
            FieldInfo field = Field(owner, name); Assert.That(field.FieldType.IsEnum, Is.True);
            WriteOwned(owner, name, Enum.ToObject(field.FieldType, value));
        }
    }
}
